using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Hali.Api.Controllers;
using Hali.Application.Advisories;
using Hali.Application.Auth;
using Hali.Application.Clusters;
using Hali.Application.Errors;
using Hali.Application.Observability;
using Hali.Application.Participation;
using Hali.Contracts.Clusters;
using Hali.Domain.Entities.Auth;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Enums;
using Hali.Tests.Unit.Clusters;
using Hali.Tests.Unit.Participation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Hali.Tests.Unit.Observability;

/// <summary>
/// Verifies that the participation surface and the cluster lifecycle
/// transitions emit the two instruments owned by <see cref="ClustersMetrics"/>:
/// <list type="bullet">
///   <item><description><c>participation_actions_total</c> — increments once
///     per non-cancelled call to <c>ClustersController</c> with bounded
///     <c>action_type</c> + <c>outcome</c> tags;</description></item>
///   <item><description><c>cluster_lifecycle_transitions_total</c> — fires
///     at the three transition points
///     (<c>CivisEvaluationService.EvaluateClusterAsync</c>,
///     <c>CivisEvaluationService.ApplyDecayAsync</c>,
///     <c>ParticipationService.EvaluateRestorationAsync</c>) with
///     <c>from_state</c> / <c>to_state</c> drawn from the bounded
///     <see cref="SignalState"/> catalog.</description></item>
/// </list>
///
/// Each test owns an isolated <see cref="ClustersMetrics"/> via
/// <see cref="TestClustersMetrics"/> so the <see cref="MeterListener"/> only
/// observes measurements from that test's meter — keeping the suite
/// parallel-safe.
/// </summary>
public class ClustersMetricsTests
{
    private sealed record LongMeasurement(long Value, Dictionary<string, object?> Tags);

    private sealed class MetricCapture : IDisposable
    {
        private readonly MeterListener _listener = new();
        public List<LongMeasurement> ParticipationMeasurements { get; } = new();
        public List<LongMeasurement> LifecycleMeasurements { get; } = new();

        public MetricCapture(ClustersMetrics metrics)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (ReferenceEquals(instrument, metrics.ParticipationActionsTotal)
                    || ReferenceEquals(instrument, metrics.ClusterLifecycleTransitionsTotal))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };

            _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
            {
                var dict = ToDict(tags);
                if (ReferenceEquals(instrument, metrics.ParticipationActionsTotal))
                {
                    ParticipationMeasurements.Add(new LongMeasurement(measurement, dict));
                }
                else if (ReferenceEquals(instrument, metrics.ClusterLifecycleTransitionsTotal))
                {
                    LifecycleMeasurements.Add(new LongMeasurement(measurement, dict));
                }
            });

            _listener.Start();
        }

        private static Dictionary<string, object?> ToDict(ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var dict = new Dictionary<string, object?>(tags.Length);
            foreach (var tag in tags)
            {
                dict[tag.Key] = tag.Value;
            }
            return dict;
        }

        public void Dispose() => _listener.Dispose();
    }

    // ── ClustersController — participation counter ───────────────────────────

    private static readonly Guid ClusterId = Guid.NewGuid();
    private static readonly Guid DeviceId = Guid.NewGuid();
    private const string DeviceHash = "dev-hash-1";

    private static CivisOptions DefaultOptions() => new CivisOptions
    {
        ContextEditWindowMinutes = 2,
        RestorationRatio = 0.6,
        MinRestorationAffectedVotes = 2
    };

    private static ClustersController CreateController(
        ClustersMetrics metrics,
        IParticipationService? participation = null,
        IParticipationRepository? participationRepo = null,
        IClusterRepository? clusters = null,
        IAuthRepository? auth = null,
        bool authenticated = true)
    {
        auth ??= Substitute.For<IAuthRepository>();
        var controller = new ClustersController(
            participation ?? Substitute.For<IParticipationService>(),
            participationRepo ?? Substitute.For<IParticipationRepository>(),
            clusters ?? Substitute.For<IClusterRepository>(),
            auth,
            Substitute.For<IOfficialPostsService>(),
            Substitute.For<Hali.Application.Institutions.IInstitutionReadRepository>(),
            Options.Create(DefaultOptions()),
            metrics);
        var httpCtx = new DefaultHttpContext();
        if (authenticated)
        {
            httpCtx.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", Guid.NewGuid().ToString())
            }, authenticationType: "test"));
        }
        controller.ControllerContext = new ControllerContext { HttpContext = httpCtx };
        return controller;
    }

    private static IAuthRepository AuthRepoWithDevice()
    {
        var auth = Substitute.For<IAuthRepository>();
        auth.FindDeviceByFingerprintAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new Device { Id = DeviceId, DeviceFingerprintHash = DeviceHash });
        return auth;
    }

    [Fact]
    public async Task RecordParticipation_Affected_Success_Emits_Accepted()
    {
        using var scope = TestClustersMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var controller = CreateController(scope.Metrics, auth: AuthRepoWithDevice());

        var result = await controller.RecordParticipation(
            ClusterId,
            new ParticipationRequestDto("affected", DeviceHash, null),
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var m = Assert.Single(capture.ParticipationMeasurements);
        Assert.Equal(1L, m.Value);
        Assert.Equal(ClustersMetrics.ActionTypeAffected, m.Tags[ClustersMetrics.TagActionType]);
        Assert.Equal(ClustersMetrics.OutcomeAccepted, m.Tags[ClustersMetrics.TagOutcome]);
    }

    [Fact]
    public async Task RecordParticipation_Observing_Success_Emits_Accepted()
    {
        using var scope = TestClustersMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var controller = CreateController(scope.Metrics, auth: AuthRepoWithDevice());

        await controller.RecordParticipation(
            ClusterId,
            new ParticipationRequestDto("observing", DeviceHash, null),
            CancellationToken.None);

        var m = Assert.Single(capture.ParticipationMeasurements);
        Assert.Equal(ClustersMetrics.ActionTypeObserving, m.Tags[ClustersMetrics.TagActionType]);
        Assert.Equal(ClustersMetrics.OutcomeAccepted, m.Tags[ClustersMetrics.TagOutcome]);
    }

    [Fact]
    public async Task RecordParticipation_NoLongerAffected_Success_Emits_Accepted()
    {
        // The controller parses `dto.Type` via Enum.TryParse with
        // ignoreCase=true, so "NoLongerAffected" / "nolongeraffected" are
        // accepted — not the snake_case "no_longer_affected" wire form.
        // The metric tag on the other hand is the canonical snake_case
        // value (via ActionTypeFor); that split is deliberate and
        // documented in ClustersMetrics.
        using var scope = TestClustersMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var controller = CreateController(scope.Metrics, auth: AuthRepoWithDevice());

        await controller.RecordParticipation(
            ClusterId,
            new ParticipationRequestDto("NoLongerAffected", DeviceHash, null),
            CancellationToken.None);

        var m = Assert.Single(capture.ParticipationMeasurements);
        Assert.Equal(ClustersMetrics.ActionTypeNoLongerAffected, m.Tags[ClustersMetrics.TagActionType]);
        Assert.Equal(ClustersMetrics.OutcomeAccepted, m.Tags[ClustersMetrics.TagOutcome]);
    }

    [Fact]
    public async Task RecordParticipation_MissingDeviceHash_Emits_Unknown_RejectedValidation()
    {
        using var scope = TestClustersMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var controller = CreateController(scope.Metrics);

        await Assert.ThrowsAsync<ValidationException>(() =>
            controller.RecordParticipation(
                ClusterId,
                new ParticipationRequestDto("affected", "", null),
                CancellationToken.None));

        var m = Assert.Single(capture.ParticipationMeasurements);
        // action_type bucket is "unknown" because the device_hash validation
        // runs before the type field is parsed. This keeps the bucket honest
        // about what the controller actually knew at the moment of rejection.
        Assert.Equal(ClustersMetrics.ActionTypeUnknown, m.Tags[ClustersMetrics.TagActionType]);
        Assert.Equal(ClustersMetrics.OutcomeRejectedValidation, m.Tags[ClustersMetrics.TagOutcome]);
    }

    [Fact]
    public async Task RecordParticipation_MissingType_Emits_Unknown_RejectedValidation()
    {
        using var scope = TestClustersMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var controller = CreateController(scope.Metrics);

        await Assert.ThrowsAsync<ValidationException>(() =>
            controller.RecordParticipation(
                ClusterId,
                new ParticipationRequestDto("", DeviceHash, null),
                CancellationToken.None));

        var m = Assert.Single(capture.ParticipationMeasurements);
        Assert.Equal(ClustersMetrics.ActionTypeUnknown, m.Tags[ClustersMetrics.TagActionType]);
        Assert.Equal(ClustersMetrics.OutcomeRejectedValidation, m.Tags[ClustersMetrics.TagOutcome]);
    }

    [Fact]
    public async Task RecordParticipation_InvalidType_Emits_Unknown_RejectedValidation()
    {
        using var scope = TestClustersMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var controller = CreateController(scope.Metrics);

        await Assert.ThrowsAsync<ValidationException>(() =>
            controller.RecordParticipation(
                ClusterId,
                new ParticipationRequestDto("bogus", DeviceHash, null),
                CancellationToken.None));

        var m = Assert.Single(capture.ParticipationMeasurements);
        // "bogus" is not a valid ParticipationType. Even though the
        // controller technically parsed the string, the action_type stays
        // "unknown" because the validation failure happens before the
        // parsed value is assigned to the action-type tag.
        Assert.Equal(ClustersMetrics.ActionTypeUnknown, m.Tags[ClustersMetrics.TagActionType]);
        Assert.Equal(ClustersMetrics.OutcomeRejectedValidation, m.Tags[ClustersMetrics.TagOutcome]);
    }

    [Fact]
    public async Task RecordParticipation_RestorationType_Rejected_Emits_Unknown_RejectedValidation()
    {
        // Raw restoration types (values 3–5) can be parsed from the enum but
        // the controller explicitly rejects them because they are persisted
        // via /restoration-response, not /participation. The tag stays
        // "unknown" so operators don't see a phantom "restoration_yes"
        // action_type that the endpoint never accepts.
        using var scope = TestClustersMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var controller = CreateController(scope.Metrics);

        await Assert.ThrowsAsync<ValidationException>(() =>
            controller.RecordParticipation(
                ClusterId,
                new ParticipationRequestDto("RestorationYes", DeviceHash, null),
                CancellationToken.None));

        var m = Assert.Single(capture.ParticipationMeasurements);
        Assert.Equal(ClustersMetrics.ActionTypeUnknown, m.Tags[ClustersMetrics.TagActionType]);
        Assert.Equal(ClustersMetrics.OutcomeRejectedValidation, m.Tags[ClustersMetrics.TagOutcome]);
    }

    [Fact]
    public async Task RecordParticipation_DeviceNotFound_Emits_KnownType_RejectedValidation()
    {
        // Device lookup runs AFTER the type field is successfully parsed, so
        // the action_type tag has already been refined from "unknown" to the
        // real value. The rejection bucket is still rejected_validation.
        using var scope = TestClustersMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var auth = Substitute.For<IAuthRepository>();
        auth.FindDeviceByFingerprintAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Device?)null);
        var controller = CreateController(scope.Metrics, auth: auth);

        await Assert.ThrowsAsync<ValidationException>(() =>
            controller.RecordParticipation(
                ClusterId,
                new ParticipationRequestDto("affected", "unknown-hash", null),
                CancellationToken.None));

        var m = Assert.Single(capture.ParticipationMeasurements);
        Assert.Equal(ClustersMetrics.ActionTypeAffected, m.Tags[ClustersMetrics.TagActionType]);
        Assert.Equal(ClustersMetrics.OutcomeRejectedValidation, m.Tags[ClustersMetrics.TagOutcome]);
    }

    [Fact]
    public async Task RecordParticipation_ServiceThrowsDependency_Emits_KnownType_DependencyError()
    {
        using var scope = TestClustersMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var participation = Substitute.For<IParticipationService>();
        participation.RecordParticipationAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid?>(),
            Arg.Any<ParticipationType>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DependencyException("dependency.db_unavailable", "DB down"));
        var controller = CreateController(scope.Metrics, participation: participation, auth: AuthRepoWithDevice());

        await Assert.ThrowsAsync<DependencyException>(() =>
            controller.RecordParticipation(
                ClusterId,
                new ParticipationRequestDto("affected", DeviceHash, null),
                CancellationToken.None));

        var m = Assert.Single(capture.ParticipationMeasurements);
        Assert.Equal(ClustersMetrics.ActionTypeAffected, m.Tags[ClustersMetrics.TagActionType]);
        Assert.Equal(ClustersMetrics.OutcomeDependencyError, m.Tags[ClustersMetrics.TagOutcome]);
    }

    [Fact]
    public async Task RecordParticipation_ClientCancelled_DoesNotEmit()
    {
        // Caller cancellation (token signaled) is excluded from the outcome
        // taxonomy — client disconnects should not bias any bucket. Mirrors
        // the guard used by SignalsController.
        using var scope = TestClustersMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var participation = Substitute.For<IParticipationService>();
        participation.RecordParticipationAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid?>(),
            Arg.Any<ParticipationType>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException(cts.Token));
        var controller = CreateController(scope.Metrics, participation: participation, auth: AuthRepoWithDevice());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            controller.RecordParticipation(
                ClusterId,
                new ParticipationRequestDto("affected", DeviceHash, null),
                cts.Token));

        Assert.Empty(capture.ParticipationMeasurements);
    }

    [Fact]
    public async Task AddContext_Success_Emits_Context_Accepted()
    {
        using var scope = TestClustersMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var controller = CreateController(scope.Metrics, auth: AuthRepoWithDevice());

        await controller.AddContext(
            ClusterId,
            new ContextRequestDto(DeviceHash, "Big pothole on Lusaka Road"),
            CancellationToken.None);

        var m = Assert.Single(capture.ParticipationMeasurements);
        Assert.Equal(ClustersMetrics.ActionTypeContext, m.Tags[ClustersMetrics.TagActionType]);
        Assert.Equal(ClustersMetrics.OutcomeAccepted, m.Tags[ClustersMetrics.TagOutcome]);
    }

    [Fact]
    public async Task AddContext_MissingText_Emits_Context_RejectedValidation()
    {
        using var scope = TestClustersMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var controller = CreateController(scope.Metrics);

        await Assert.ThrowsAsync<ValidationException>(() =>
            controller.AddContext(ClusterId, new ContextRequestDto(DeviceHash, ""), CancellationToken.None));

        var m = Assert.Single(capture.ParticipationMeasurements);
        Assert.Equal(ClustersMetrics.ActionTypeContext, m.Tags[ClustersMetrics.TagActionType]);
        Assert.Equal(ClustersMetrics.OutcomeRejectedValidation, m.Tags[ClustersMetrics.TagOutcome]);
    }

    [Fact]
    public async Task AddContext_ConflictFromService_Emits_Context_RejectedValidation()
    {
        // ConflictException (context_requires_affected, context_window_expired)
        // is bucketed as rejected_validation because, like a 4xx, it means
        // the request was rejected on user-visible preconditions — not a
        // server/dependency failure.
        using var scope = TestClustersMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var participation = Substitute.For<IParticipationService>();
        participation.AddContextAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConflictException(ErrorCodes.ParticipationContextRequiresAffected, "Requires affected."));
        var controller = CreateController(scope.Metrics, participation: participation, auth: AuthRepoWithDevice());

        await Assert.ThrowsAsync<ConflictException>(() =>
            controller.AddContext(ClusterId, new ContextRequestDto(DeviceHash, "Late"), CancellationToken.None));

        var m = Assert.Single(capture.ParticipationMeasurements);
        Assert.Equal(ClustersMetrics.ActionTypeContext, m.Tags[ClustersMetrics.TagActionType]);
        Assert.Equal(ClustersMetrics.OutcomeRejectedValidation, m.Tags[ClustersMetrics.TagOutcome]);
    }

    [Fact]
    public async Task RecordRestorationResponse_Success_Emits_RestorationResponse_Accepted()
    {
        using var scope = TestClustersMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var controller = CreateController(scope.Metrics, auth: AuthRepoWithDevice());

        await controller.RecordRestorationResponse(
            ClusterId,
            new RestorationResponseRequestDto("restored", DeviceHash),
            CancellationToken.None);

        var m = Assert.Single(capture.ParticipationMeasurements);
        Assert.Equal(ClustersMetrics.ActionTypeRestorationResponse, m.Tags[ClustersMetrics.TagActionType]);
        Assert.Equal(ClustersMetrics.OutcomeAccepted, m.Tags[ClustersMetrics.TagOutcome]);
    }

    [Fact]
    public async Task RecordRestorationResponse_InvalidResponse_Emits_RestorationResponse_RejectedValidation()
    {
        using var scope = TestClustersMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var controller = CreateController(scope.Metrics);

        await Assert.ThrowsAsync<ValidationException>(() =>
            controller.RecordRestorationResponse(
                ClusterId,
                new RestorationResponseRequestDto("bogus", DeviceHash),
                CancellationToken.None));

        var m = Assert.Single(capture.ParticipationMeasurements);
        Assert.Equal(ClustersMetrics.ActionTypeRestorationResponse, m.Tags[ClustersMetrics.TagActionType]);
        Assert.Equal(ClustersMetrics.OutcomeRejectedValidation, m.Tags[ClustersMetrics.TagOutcome]);
    }

    [Fact]
    public async Task RecordRestorationResponse_ConflictFromService_Emits_RestorationResponse_RejectedValidation()
    {
        using var scope = TestClustersMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var participation = Substitute.For<IParticipationService>();
        participation.RecordRestorationResponseAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConflictException(ErrorCodes.ParticipationRestorationRequiresAffected, "Requires affected."));
        var controller = CreateController(scope.Metrics, participation: participation, auth: AuthRepoWithDevice());

        await Assert.ThrowsAsync<ConflictException>(() =>
            controller.RecordRestorationResponse(
                ClusterId,
                new RestorationResponseRequestDto("restored", DeviceHash),
                CancellationToken.None));

        var m = Assert.Single(capture.ParticipationMeasurements);
        Assert.Equal(ClustersMetrics.ActionTypeRestorationResponse, m.Tags[ClustersMetrics.TagActionType]);
        Assert.Equal(ClustersMetrics.OutcomeRejectedValidation, m.Tags[ClustersMetrics.TagOutcome]);
    }

    [Fact]
    public async Task AllThreeParticipationEndpoints_NoLifecycleTransitionEmitted()
    {
        // Sanity guard: the controller layer never touches the lifecycle
        // counter — that instrument belongs to the Application layer.
        // Participation counter is allowed to fire on all three endpoints.
        using var scope = TestClustersMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var controller = CreateController(scope.Metrics, auth: AuthRepoWithDevice());

        await controller.RecordParticipation(
            ClusterId, new ParticipationRequestDto("affected", DeviceHash, null), CancellationToken.None);
        await controller.AddContext(
            ClusterId, new ContextRequestDto(DeviceHash, "ctx"), CancellationToken.None);
        await controller.RecordRestorationResponse(
            ClusterId, new RestorationResponseRequestDto("restored", DeviceHash), CancellationToken.None);

        Assert.Equal(3, capture.ParticipationMeasurements.Count);
        Assert.Empty(capture.LifecycleMeasurements);
    }

    // ── CivisEvaluationService — lifecycle counter ───────────────────────────

    private static SignalCluster UnconfirmedRoadsCluster(int rawCount = 3) => new SignalCluster
    {
        Id = Guid.NewGuid(),
        Category = CivicCategory.Roads,
        State = SignalState.Unconfirmed,
        RawConfirmationCount = rawCount,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        FirstSeenAt = DateTime.UtcNow,
        LastSeenAt = DateTime.UtcNow
    };

    private static SignalCluster ActiveRoadsCluster(int rawCount = 4) => new SignalCluster
    {
        Id = Guid.NewGuid(),
        Category = CivicCategory.Roads,
        State = SignalState.Active,
        RawConfirmationCount = rawCount,
        Wrab = 2m,
        CreatedAt = DateTime.UtcNow.AddHours(-100.0),
        UpdatedAt = DateTime.UtcNow.AddHours(-100.0),
        FirstSeenAt = DateTime.UtcNow.AddHours(-100.0),
        LastSeenAt = DateTime.UtcNow.AddHours(-100.0),
        ActivatedAt = DateTime.UtcNow.AddHours(-100.0)
    };

    private static CivisOptions CivisDefaults() => new CivisOptions
    {
        WrabRollingWindowDays = 30,
        JoinThreshold = 0.65,
        MinUniqueDevices = 2,
        DeactivationThreshold = 0.5,
        ActiveMassHorizonHours = 48,
        TimeScoreMaxAgeHours = 24.0,
        Roads = new CivisCategoryOptions
        {
            BaseFloor = 2,
            HalfLifeHours = 18.0,
            MacfMin = 2,
            MacfMax = 6
        }
    };

    [Fact]
    public async Task EvaluateCluster_UnconfirmedToActive_EmitsLifecycleTransition()
    {
        using var scope = TestClustersMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var cluster = UnconfirmedRoadsCluster(3);
        var repo = new FakeClusterRepo(cluster)
        {
            WrabCount = 3,
            ActiveMass = 3,
            UniqueDevices = 2,
        };
        var svc = new CivisEvaluationService(
            repo,
            Options.Create(CivisDefaults()),
            notificationQueue: null,
            logger: null,
            metrics: null,
            clustersMetrics: scope.Metrics);

        await svc.EvaluateClusterAsync(cluster.Id);

        Assert.Equal(SignalState.Active, cluster.State);
        var m = Assert.Single(capture.LifecycleMeasurements);
        Assert.Equal(1L, m.Value);
        Assert.Equal(ClustersMetrics.StateUnconfirmed, m.Tags[ClustersMetrics.TagFromState]);
        Assert.Equal(ClustersMetrics.StateActive, m.Tags[ClustersMetrics.TagToState]);
    }

    [Fact]
    public async Task EvaluateCluster_WhenMacfNotMet_EmitsNoLifecycleTransition()
    {
        using var scope = TestClustersMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var cluster = UnconfirmedRoadsCluster(rawCount: 1);
        var repo = new FakeClusterRepo(cluster)
        {
            WrabCount = 3,
            ActiveMass = 3,
            UniqueDevices = 2,
        };
        var svc = new CivisEvaluationService(
            repo,
            Options.Create(CivisDefaults()),
            notificationQueue: null,
            logger: null,
            metrics: null,
            clustersMetrics: scope.Metrics);

        await svc.EvaluateClusterAsync(cluster.Id);

        Assert.Equal(SignalState.Unconfirmed, cluster.State);
        Assert.Empty(capture.LifecycleMeasurements);
    }

    [Fact]
    public async Task ApplyDecay_ActiveToPossibleRestoration_EmitsLifecycleTransition()
    {
        using var scope = TestClustersMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var cluster = ActiveRoadsCluster();
        var repo = new FakeClusterRepo(cluster);
        var svc = new CivisEvaluationService(
            repo,
            Options.Create(CivisDefaults()),
            notificationQueue: null,
            logger: null,
            metrics: null,
            clustersMetrics: scope.Metrics);

        await svc.ApplyDecayAsync(cluster.Id);

        Assert.Equal(SignalState.PossibleRestoration, cluster.State);
        var m = Assert.Single(capture.LifecycleMeasurements);
        Assert.Equal(ClustersMetrics.StateActive, m.Tags[ClustersMetrics.TagFromState]);
        Assert.Equal(ClustersMetrics.StatePossibleRestoration, m.Tags[ClustersMetrics.TagToState]);
    }

    [Fact]
    public async Task ApplyDecay_PossibleRestorationToResolved_EmitsLifecycleTransition()
    {
        using var scope = TestClustersMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var cluster = ActiveRoadsCluster();
        cluster.State = SignalState.PossibleRestoration;
        cluster.PossibleRestorationAt = DateTime.UtcNow.AddHours(-100.0);
        var repo = new FakeClusterRepo(cluster);
        var svc = new CivisEvaluationService(
            repo,
            Options.Create(CivisDefaults()),
            notificationQueue: null,
            logger: null,
            metrics: null,
            clustersMetrics: scope.Metrics);

        await svc.ApplyDecayAsync(cluster.Id);

        Assert.Equal(SignalState.Resolved, cluster.State);
        var m = Assert.Single(capture.LifecycleMeasurements);
        Assert.Equal(ClustersMetrics.StatePossibleRestoration, m.Tags[ClustersMetrics.TagFromState]);
        Assert.Equal(ClustersMetrics.StateResolved, m.Tags[ClustersMetrics.TagToState]);
    }

    [Fact]
    public async Task ApplyDecay_WhenLiveMassAboveThreshold_EmitsNoLifecycleTransition()
    {
        using var scope = TestClustersMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var cluster = new SignalCluster
        {
            Id = Guid.NewGuid(),
            Category = CivicCategory.Roads,
            State = SignalState.Active,
            RawConfirmationCount = 100,
            Wrab = 2m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
            ActivatedAt = DateTime.UtcNow
        };
        var repo = new FakeClusterRepo(cluster);
        var svc = new CivisEvaluationService(
            repo,
            Options.Create(CivisDefaults()),
            notificationQueue: null,
            logger: null,
            metrics: null,
            clustersMetrics: scope.Metrics);

        await svc.ApplyDecayAsync(cluster.Id);

        Assert.Equal(SignalState.Active, cluster.State);
        Assert.Empty(capture.LifecycleMeasurements);
    }

    // ── ParticipationService — lifecycle counter ─────────────────────────────

    private static SignalCluster RestorationActiveCluster() => new SignalCluster
    {
        Id = ClusterId,
        Category = CivicCategory.Roads,
        State = SignalState.Active,
        RawConfirmationCount = 3,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        FirstSeenAt = DateTime.UtcNow,
        LastSeenAt = DateTime.UtcNow,
        ActivatedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task EvaluateRestoration_RatioMet_EmitsActiveToPossibleRestorationTransition()
    {
        using var scope = TestClustersMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var cluster = RestorationActiveCluster();
        var pRepo = new FakeParticipationRepo();
        var cRepo = new FakeClusterRepoForParticipation(cluster);
        var svc = new ParticipationService(
            pRepo,
            cRepo,
            Options.Create(DefaultOptions()),
            logger: null,
            metrics: scope.Metrics);

        var deviceA = Guid.NewGuid();
        var deviceB = Guid.NewGuid();

        // Seed both devices as affected first (server-side gating requires it)
        await svc.RecordParticipationAsync(cluster.Id, deviceA, null, ParticipationType.Affected, null, default);
        await svc.RecordParticipationAsync(cluster.Id, deviceB, null, ParticipationType.Affected, null, default);
        await svc.RecordRestorationResponseAsync(cluster.Id, deviceA, null, "restored", default);
        await svc.RecordRestorationResponseAsync(cluster.Id, deviceB, null, "restored", default);

        Assert.Equal(SignalState.PossibleRestoration, cluster.State);
        var m = Assert.Single(capture.LifecycleMeasurements);
        Assert.Equal(ClustersMetrics.StateActive, m.Tags[ClustersMetrics.TagFromState]);
        Assert.Equal(ClustersMetrics.StatePossibleRestoration, m.Tags[ClustersMetrics.TagToState]);
    }

    [Fact]
    public async Task EvaluateRestoration_RatioNotMet_EmitsNoLifecycleTransition()
    {
        using var scope = TestClustersMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var cluster = RestorationActiveCluster();
        var pRepo = new FakeParticipationRepo();
        var cRepo = new FakeClusterRepoForParticipation(cluster);
        var svc = new ParticipationService(
            pRepo,
            cRepo,
            Options.Create(DefaultOptions()),
            logger: null,
            metrics: scope.Metrics);

        var deviceA = Guid.NewGuid();
        var deviceB = Guid.NewGuid();

        // 1 yes + 1 still_affected → ratio 0.5, below 0.6 threshold
        await svc.RecordParticipationAsync(cluster.Id, deviceA, null, ParticipationType.Affected, null, default);
        await svc.RecordParticipationAsync(cluster.Id, deviceB, null, ParticipationType.Affected, null, default);
        await svc.RecordRestorationResponseAsync(cluster.Id, deviceA, null, "restored", default);
        await svc.RecordRestorationResponseAsync(cluster.Id, deviceB, null, "still_affected", default);

        Assert.Equal(SignalState.Active, cluster.State);
        Assert.Empty(capture.LifecycleMeasurements);
    }

    // ── Cross-instrument sanity — no double-counting at the instrument level ─

    [Fact]
    public async Task LifecycleCounter_FiresExactlyOncePerPersistedTransition()
    {
        using var scope = TestClustersMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var cluster = UnconfirmedRoadsCluster(3);
        var repo = new FakeClusterRepo(cluster)
        {
            WrabCount = 3,
            ActiveMass = 3,
            UniqueDevices = 2,
        };
        var svc = new CivisEvaluationService(
            repo,
            Options.Create(CivisDefaults()),
            notificationQueue: null,
            logger: null,
            metrics: null,
            clustersMetrics: scope.Metrics);

        await svc.EvaluateClusterAsync(cluster.Id);
        // Second call: the guard `cluster.State == SignalState.Unconfirmed`
        // rejects the re-evaluation — we must not emit a second time.
        await svc.EvaluateClusterAsync(cluster.Id);

        Assert.Single(capture.LifecycleMeasurements);
        Assert.Single(repo.OutboxEvents);
    }
}
