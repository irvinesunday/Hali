using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Auth;
using Hali.Application.Notifications;
using Hali.Application.Participation;
using Hali.Domain.Entities.Notifications;
using NSubstitute;
using Xunit;

namespace Hali.Tests.Unit.Notifications;

public class NotificationQueueServiceTests
{
    private static (
        NotificationQueueService svc,
        IFollowRepository follows,
        IAuthRepository auth,
        IParticipationRepository participation,
        INotificationRepository notifications)
        Build()
    {
        var follows = Substitute.For<IFollowRepository>();
        var auth = Substitute.For<IAuthRepository>();
        var participation = Substitute.For<IParticipationRepository>();
        var notifications = Substitute.For<INotificationRepository>();
        var svc = new NotificationQueueService(follows, auth, participation, notifications);
        return (svc, follows, auth, participation, notifications);
    }

    private static IReadOnlyList<Follow> MakeFollowers(int count, Guid? localityId = null)
    {
        var id = localityId ?? Guid.NewGuid();
        return Enumerable.Range(0, count)
            .Select(_ => new Follow
            {
                Id = Guid.NewGuid(),
                AccountId = Guid.NewGuid(),
                LocalityId = id,
                CreatedAt = DateTime.UtcNow
            })
            .ToList();
    }

    // -----------------------------------------------------------------------
    // QueueClusterActivatedAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task QueueClusterActivated_WhenLocalityIdIsNull_DoesNotQueryFollowers()
    {
        var (svc, follows, auth, participation, notifications) = Build();
        var clusterId = Guid.NewGuid();

        await svc.QueueClusterActivatedAsync(clusterId, localityId: null, "Water outage", "No water.", CancellationToken.None);

        await follows.DidNotReceive().GetByLocalityAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await notifications.DidNotReceive().EnqueueAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueueClusterActivated_WhenNoFollowers_DoesNotFetchTokensOrEnqueue()
    {
        var (svc, follows, auth, participation, notifications) = Build();
        var localityId = Guid.NewGuid();
        var clusterId = Guid.NewGuid();

        follows.GetByLocalityAsync(localityId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Follow>)new List<Follow>());

        await svc.QueueClusterActivatedAsync(clusterId, localityId, "Water outage", "No water.");

        await auth.DidNotReceive().GetPushTokensByAccountIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>());
        await notifications.DidNotReceive().EnqueueAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueueClusterActivated_WhenFollowersExist_EnqueuesOneNotificationPerPushToken()
    {
        var (svc, follows, auth, participation, notifications) = Build();
        var localityId = Guid.NewGuid();
        var clusterId = Guid.NewGuid();
        var followers = MakeFollowers(3, localityId);

        follows.GetByLocalityAsync(localityId, Arg.Any<CancellationToken>())
            .Returns(followers);
        auth.GetPushTokensByAccountIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<string>)new List<string> { "ExpoToken[aaa]", "ExpoToken[bbb]", "ExpoToken[ccc]" });

        await svc.QueueClusterActivatedAsync(clusterId, localityId, "Water outage", "No water.");

        await notifications.Received(3).EnqueueAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueueClusterActivated_EnqueuedNotification_HasCorrectChannelAndType()
    {
        var (svc, follows, auth, participation, notifications) = Build();
        var localityId = Guid.NewGuid();
        var clusterId = Guid.NewGuid();
        var followers = MakeFollowers(1, localityId);

        follows.GetByLocalityAsync(localityId, Arg.Any<CancellationToken>())
            .Returns(followers);
        auth.GetPushTokensByAccountIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<string>)new List<string> { "ExpoToken[aaa]" });

        Notification? captured = null;
        await notifications.EnqueueAsync(Arg.Do<Notification>(n => captured = n), Arg.Any<CancellationToken>());

        await svc.QueueClusterActivatedAsync(clusterId, localityId, "Water outage", "No water.");

        Assert.NotNull(captured);
        Assert.Equal("push", captured!.Channel);
        Assert.Equal("cluster_activated", captured.NotificationType);
        Assert.Equal("queued", captured.Status);
    }

    // -----------------------------------------------------------------------
    // QueueRestorationPromptAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task QueueRestorationPrompt_WhenNoAffectedAccounts_DoesNotEnqueue()
    {
        var (svc, follows, auth, participation, notifications) = Build();
        var clusterId = Guid.NewGuid();

        participation.GetAffectedAccountIdsAsync(clusterId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Guid>)new List<Guid>());

        await svc.QueueRestorationPromptAsync(clusterId, "Water outage");

        await auth.DidNotReceive().GetPushTokensByAccountIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>());
        await notifications.DidNotReceive().EnqueueAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueueRestorationPrompt_WhenAffectedAccountsExist_EnqueuesOneNotificationPerToken()
    {
        var (svc, follows, auth, participation, notifications) = Build();
        var clusterId = Guid.NewGuid();
        var accountIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        participation.GetAffectedAccountIdsAsync(clusterId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Guid>)accountIds);
        auth.GetPushTokensByAccountIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<string>)new List<string> { "ExpoToken[aaa]", "ExpoToken[bbb]" });

        await svc.QueueRestorationPromptAsync(clusterId, "Water outage");

        await notifications.Received(2).EnqueueAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueueRestorationPrompt_EnqueuedNotification_HasRestorationPromptType()
    {
        var (svc, follows, auth, participation, notifications) = Build();
        var clusterId = Guid.NewGuid();

        participation.GetAffectedAccountIdsAsync(clusterId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Guid>)new List<Guid> { Guid.NewGuid() });
        auth.GetPushTokensByAccountIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<string>)new List<string> { "ExpoToken[aaa]" });

        Notification? captured = null;
        await notifications.EnqueueAsync(Arg.Do<Notification>(n => captured = n), Arg.Any<CancellationToken>());

        await svc.QueueRestorationPromptAsync(clusterId, "Water outage");

        Assert.NotNull(captured);
        Assert.Equal("restoration_prompt", captured!.NotificationType);
        Assert.Equal("push", captured.Channel);
        Assert.NotNull(captured.DedupeKey);
        Assert.Contains(clusterId.ToString(), captured.DedupeKey);
    }

    // -----------------------------------------------------------------------
    // QueueClusterResolvedAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task QueueClusterResolved_WhenLocalityIdIsNull_DoesNotEnqueue()
    {
        var (svc, follows, auth, participation, notifications) = Build();
        var clusterId = Guid.NewGuid();

        await svc.QueueClusterResolvedAsync(clusterId, localityId: null, "Water outage");

        await follows.DidNotReceive().GetByLocalityAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await notifications.DidNotReceive().EnqueueAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueueClusterResolved_WhenNoFollowers_DoesNotEnqueue()
    {
        var (svc, follows, auth, participation, notifications) = Build();
        var localityId = Guid.NewGuid();
        var clusterId = Guid.NewGuid();

        follows.GetByLocalityAsync(localityId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Follow>)new List<Follow>());

        await svc.QueueClusterResolvedAsync(clusterId, localityId, "Water outage");

        await notifications.DidNotReceive().EnqueueAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueueClusterResolved_WhenFollowersExist_EnqueuesOneNotificationPerToken()
    {
        var (svc, follows, auth, participation, notifications) = Build();
        var localityId = Guid.NewGuid();
        var clusterId = Guid.NewGuid();
        var followers = MakeFollowers(2, localityId);

        follows.GetByLocalityAsync(localityId, Arg.Any<CancellationToken>())
            .Returns(followers);
        auth.GetPushTokensByAccountIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<string>)new List<string> { "ExpoToken[aaa]", "ExpoToken[bbb]" });

        await svc.QueueClusterResolvedAsync(clusterId, localityId, "Water outage");

        await notifications.Received(2).EnqueueAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueueClusterResolved_DeduplicatesFollowerAccountIds_BeforeFetchingTokens()
    {
        // If the same account follows twice (shouldn't normally happen, but guard the service),
        // we only want to fetch tokens once per distinct account.
        var (svc, follows, auth, participation, notifications) = Build();
        var localityId = Guid.NewGuid();
        var clusterId = Guid.NewGuid();
        var sharedAccountId = Guid.NewGuid();

        // Two follow records for the same account
        var followers = new List<Follow>
        {
            new Follow { Id = Guid.NewGuid(), AccountId = sharedAccountId, LocalityId = localityId, CreatedAt = DateTime.UtcNow },
            new Follow { Id = Guid.NewGuid(), AccountId = sharedAccountId, LocalityId = localityId, CreatedAt = DateTime.UtcNow }
        };

        follows.GetByLocalityAsync(localityId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Follow>)followers);

        IEnumerable<Guid>? capturedIds = null;
        auth.GetPushTokensByAccountIdsAsync(
            Arg.Do<IEnumerable<Guid>>(ids => capturedIds = ids.ToList()),
            Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<string>)new List<string> { "ExpoToken[aaa]" });

        await svc.QueueClusterResolvedAsync(clusterId, localityId, "Water outage");

        // Distinct() must reduce two same-account follows to a single account ID in the query
        Assert.NotNull(capturedIds);
        Assert.Single(capturedIds!);
        Assert.Equal(sharedAccountId, capturedIds!.First());
    }
}
