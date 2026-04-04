# Hali — CIVIS Engine Implementation Guide
**The complete algorithmic specification for CIVIS, clustering, lifecycle transitions, and restoration.**

This file is your contract for all trust, scoring, and state machine logic. Implement these formulas exactly. Do not simplify or approximate. Do not expose any value computed here in a public API response.

---

## Module boundary

`Hali.Modules.Civis` owns:
- WRAB computation
- SDS computation
- MACF computation
- Join score computation
- Device diversity gate
- Burst dampening
- Cluster state transition rules
- Restoration confirmation rules

It must not own: UI ranking, outbound notifications, persistence layer (only reads/writes via domain services).

---

## 1. WRAB — Weighted Rolling Active Baseline

WRAB estimates typical activity pressure for a geography × category × time context.

```csharp
public decimal ComputeWrab(
    Guid localityId,
    CivicCategory category,
    DateTimeOffset evaluationTime,
    CivicConfig config)
{
    var window = evaluationTime.AddDays(-30);  // 30-day rolling window

    var historicalEvents = repository.GetSignalEvents(
        localityId, category,
        from: window,
        to: evaluationTime
    );

    decimal wrab = historicalEvents.Sum(e =>
        ComputeEventWeight(e) *
        ComputeTemporalDecay(e, evaluationTime, config) *
        ComputeSpatialRelevance(e, localityId)
    );

    // Apply floor — prevents single events from over-activating calm areas
    decimal floor = config.GetBaseFloor(category);
    return Math.Max(wrab, floor);
}

// Event weight composition
private decimal ComputeEventWeight(SignalEvent e)
{
    decimal baseWeight              = 1.0m;
    decimal accountMaturityWeight   = GetAccountMaturityWeight(e.AccountId);  // 0.8..1.2
    decimal deviceIntegrityWeight   = GetDeviceIntegrityWeight(e.DeviceId);   // 0.3..1.0
    decimal contributionTypeWeight  = e.IsAffected ? 1.0m : 0.7m;
    decimal textConfidenceWeight    = e.CivisPrecheck.ContainsKey("nlp_confidence")
                                        ? (decimal)e.CivisPrecheck["nlp_confidence"]
                                        : 0.8m;
    return baseWeight
         * accountMaturityWeight
         * deviceIntegrityWeight
         * contributionTypeWeight
         * textConfidenceWeight;
}
```

### Temporal decay kernel

```csharp
private decimal ComputeTemporalDecay(
    SignalEvent e,
    DateTimeOffset evaluationTime,
    CivicConfig config)
{
    var halfLifeHours = config.GetHalfLifeHours(category);
    var lambda = Math.Log(2) / halfLifeHours;
    var deltaHours = (evaluationTime - e.OccurredAt).TotalHours;
    return (decimal)Math.Exp(-lambda * deltaHours);
}
```

**Half-life values (from config — do not hardcode):**

| Category | Half-life |
|---|---|
| transport | 8 hours |
| electricity | 12 hours |
| roads | 18 hours |
| safety | 18 hours |
| water | 24 hours |
| infrastructure | 24 hours |
| governance | 24 hours |
| environment | 36 hours |

---

## 2. SDS — Signal Density Score

SDS measures how unusual current activity is vs the baseline.

```csharp
public decimal ComputeSds(
    Guid clusterId,
    decimal effectiveWrab,
    DateTimeOffset evaluationTime,
    CivicConfig config)
{
    // Evaluation horizon: last 60-180 min depending on category
    var horizonMinutes = config.GetEvaluationHorizonMinutes(category);
    var since = evaluationTime.AddMinutes(-horizonMinutes);

    var activeMass = repository.GetRecentSignalEvents(clusterId, since)
        .Sum(e => ComputeEventWeight(e) * ComputeBurstPenalty(e));

    return activeMass / effectiveWrab;
    // SDS > 1.0 means activity exceeds baseline
    // SDS does NOT automatically trigger activation
}
```

---

## 3. MACF — Minimum Absolute Confirmation Floor

MACF converts relative surprise into an absolute participation requirement.

```csharp
public int ComputeMacf(
    decimal sds,
    CivicCategory category,
    decimal geoUncertainty,
    CivicConfig config)
{
    var catConfig = config.GetCategoryConfig(category);

    decimal rawMacf = catConfig.BaseFloor
                    + catConfig.Alpha * (decimal)Math.Log2((double)(1 + sds))
                    + catConfig.SensitivityUplift
                    + geoUncertainty * 0.5m;  // extra evidence if location is weak

    int macf = (int)Math.Ceiling(rawMacf);
    return Math.Clamp(macf, catConfig.MacfMin, catConfig.MacfMax);
}
// Alpha defaults to 1.0 for MVP across all categories
// SensitivityUplift: 0 for most; 1 for safety category
```

---

## 4. Device diversity gate

Activation requires unique device diversity — not just participation count.

```csharp
public bool PassesDiversityGate(Guid clusterId, CivicConfig config)
{
    var recentParticipations = repository.GetRecentParticipations(clusterId);

    int uniqueDevices   = recentParticipations.Select(p => p.DeviceId).Distinct().Count();
    int uniqueAccounts  = recentParticipations
                            .Where(p => p.AccountId.HasValue)
                            .Select(p => p.AccountId!.Value)
                            .Distinct()
                            .Count();

    // Dominant device share cap: no single device > 50% of weight
    decimal totalWeight = recentParticipations.Sum(p => ComputeEventWeight(p));
    bool dominantDeviceOk = recentParticipations
        .GroupBy(p => p.DeviceId)
        .All(g => g.Sum(p => ComputeEventWeight(p)) / totalWeight <= 0.5m);

    return uniqueDevices >= config.MinUniqueDevices   // ≥ 2
        && dominantDeviceOk;
}
```

---

## 5. Burst dampening

```csharp
private decimal ComputeBurstPenalty(SignalEvent e)
{
    // Check recent rate for this device in this category/cell
    var deviceRate  = GetDeviceRecentRate(e.DeviceId, minutes: 30);
    var subnetRate  = GetSubnetRecentRate(e.IpSubnet, minutes: 30);
    var novelty     = GetPatternNovelty(e);  // 0..1 — lower = more novel = less penalty

    decimal burstPenalty = 1.0m
        * SmoothPenalty(deviceRate, threshold: 5, factor: 0.3m)
        * SmoothPenalty(subnetRate, threshold: 20, factor: 0.5m)
        * novelty;

    return Math.Clamp(burstPenalty, 0.1m, 1.0m);
    // Never zero — even suspicious events contribute a small weight
    // Smooth penalties so real spikes still activate (don't flip harshly)
}

private decimal SmoothPenalty(int rate, int threshold, decimal factor)
{
    if (rate <= threshold) return 1.0m;
    return Math.Max(factor, 1.0m - (decimal)(rate - threshold) / threshold * (1.0m - factor));
}
```

---

## 6. Activation gate

```csharp
public ClusterActivationResult EvaluateActivation(Guid clusterId)
{
    var cluster         = repository.GetCluster(clusterId);
    var effectiveWrab   = ComputeWrab(cluster.LocalityId, cluster.Category, DateTimeOffset.UtcNow, config);
    var sds             = ComputeSds(clusterId, effectiveWrab, DateTimeOffset.UtcNow, config);
    var geoUncertainty  = ComputeGeoUncertainty(cluster);
    var macf            = ComputeMacf(sds, cluster.Category, geoUncertainty, config);
    var activeMassNow   = GetActiveMassNow(clusterId);
    var diversityOk     = PassesDiversityGate(clusterId, config);

    var reasonCodes = new List<string>();
    bool passes = true;

    if (activeMassNow < macf)
    {
        reasonCodes.Add("macf_not_met");
        passes = false;
    }
    if (!diversityOk)
    {
        reasonCodes.Add("low_diversity");
        passes = false;
    }
    if (sds < 1.0m)
    {
        reasonCodes.Add("sds_below_baseline");
        passes = false;
    }

    // Persist decision (INTERNAL — never expose in public API)
    repository.SaveCivisDecision(new CivisDecision
    {
        ClusterId       = clusterId,
        DecisionType    = "activation_evaluation",
        ReasonCodes     = reasonCodes,
        Metrics         = new { wrab = effectiveWrab, sds, macf, activeMassNow,
                                uniqueDeviceCount = GetUniqueDeviceCount(clusterId) }
    });

    return new ClusterActivationResult(passes, reasonCodes, macf, sds);
}
```

---

## 7. Cluster state machine

All transitions must emit an outbox event. The DB trigger on `signal_clusters` handles this automatically (see schema reference), but workers must still write the decision record before the state update.

```csharp
public async Task HandleClusterTransitionAsync(Guid clusterId)
{
    var cluster     = await repository.GetClusterAsync(clusterId);
    var assessment  = await repository.GetLatestCivisDecisionAsync(clusterId);

    switch (cluster.State)
    {
        case SignalState.Unconfirmed:
            if (assessment.MeetsActivationGate)
                await TransitionToAsync(clusterId, SignalState.Active, "activation_gate_passed");
            else if (IsExpired(cluster))
                await TransitionToAsync(clusterId, SignalState.Resolved, "expired_insufficient_evidence");
            break;

        case SignalState.Active:
            if (ShouldEnterPossibleRestoration(cluster))
                await TransitionToAsync(clusterId, SignalState.PossibleRestoration, "restoration_triggered");
            else if (ShouldDecayToResolved(cluster))
                await TransitionToAsync(clusterId, SignalState.Resolved, "inactive_and_expired");
            break;

        case SignalState.PossibleRestoration:
            var restoration = await EvaluateRestorationAsync(clusterId);
            if (restoration.IsConfirmed)
                await TransitionToAsync(clusterId, SignalState.Resolved, "restoration_confirmed");
            else if (restoration.IsRejected)
                await TransitionToAsync(clusterId, SignalState.Active, "restoration_rejected");
            else if (restoration.IsTimedOut)
                await TransitionToAsync(clusterId, SignalState.Active, "restoration_timeout");
            break;
    }
}
```

### Decay thresholds

```csharp
private bool ShouldDecayToResolved(SignalCluster cluster)
{
    // Live mass falls below deactivation threshold
    // Deactivation threshold is LOWER than activation to prevent flicker
    var liveMass = ComputeLiveMassNow(cluster.Id);
    var deactivationThreshold = config.GetMacf(cluster.Category) * 0.5m;  // half of MACF
    return liveMass < deactivationThreshold
        && cluster.LastSeenAt < DateTimeOffset.UtcNow.AddHours(-config.GetHalfLifeHours(cluster.Category))
        && !HasRecurringPatternLock(cluster);
}
```

---

## 8. Restoration confirmation rules

```csharp
public RestorationEvaluationResult EvaluateRestoration(Guid clusterId)
{
    var restorationVotes = repository.GetRestorationParticipations(clusterId);

    // Only count from historically affected participants (they have the ground truth)
    var affectedDevices = repository.GetAffectedDeviceIds(clusterId);
    var relevantVotes   = restorationVotes
        .Where(v => affectedDevices.Contains(v.DeviceId))
        .ToList();

    int yesVotes        = relevantVotes.Count(v => v.ParticipationType == ParticipationType.RestorationYes);
    int noVotes         = relevantVotes.Count(v => v.ParticipationType == ParticipationType.RestorationNo);
    int totalVotes      = yesVotes + noVotes;  // unsure excluded from ratio

    if (totalVotes == 0) return RestorationEvaluationResult.Insufficient();

    decimal ratio               = (decimal)yesVotes / totalVotes;
    int distinctAffectedDevices = relevantVotes.Select(v => v.DeviceId).Distinct().Count();

    bool confirmed = ratio >= config.RestorationRatio               // >= 0.60
                  && distinctAffectedDevices >= config.MinRestorationAffectedVotes  // >= 2
                  && IsCooldownSatisfied(clusterId);

    bool rejected  = noVotes > 0
                  && (decimal)noVotes / totalVotes > 0.60m
                  && distinctAffectedDevices >= 2;

    return new RestorationEvaluationResult(confirmed, rejected, ratio, distinctAffectedDevices);
}
```

---

## 9. Cluster compatibility and join scoring

See `docs/arch/03_phase1_backend.md` Gate 4 for the join score formula.

Distance scoring for join:
```csharp
private decimal ComputeDistanceScore(SignalCandidate candidate, SignalCluster cluster)
{
    // Use H3 ring distance as proxy for spatial closeness
    var candidateCell = H3.GeoToH3(candidate.Latitude, candidate.Longitude, resolution: 9);
    var clusterCell   = cluster.SpatialCellId;

    int ringDistance = H3.GridDistance(candidateCell, clusterCell);

    // Score decays by ring: ring 0 = 1.0, ring 1 = 0.85, ring 2 = 0.65, ring 3 = 0.35, ring 4+ = 0
    return ringDistance switch
    {
        0 => 1.0m,
        1 => 0.85m,
        2 => 0.65m,
        3 => 0.35m,
        _ => 0.0m
    };
}
```

Time scoring for join:
```csharp
private decimal ComputeTimeScore(SignalCandidate candidate, SignalCluster cluster)
{
    var deltaHours = Math.Abs((candidate.OccurredAt - cluster.LastSeenAt).TotalHours);
    var halfLifeHours = config.GetHalfLifeHours(candidate.Category);
    return (decimal)Math.Exp(-Math.Log(2) / halfLifeHours * deltaHours);
}
```

---

## 10. What CIVIS must never do

- Never expose any computed score, weight, or reason code in a public API response
- Never make activation decisions based on institution pressure or admin override (integrity policy path only)
- Never activate a cluster solely because one user submitted many times (diversity gate prevents this)
- Never keep a cluster permanently active without supporting evidence (decay rules apply even to recurring)
- Never emit a reason code like "low_trust_user" — reason codes are structural (macf_not_met, low_diversity), not identity-based
