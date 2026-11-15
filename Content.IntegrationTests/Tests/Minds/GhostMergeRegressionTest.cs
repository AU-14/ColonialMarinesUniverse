using System.IO;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Mind;
using Content.Shared._RMC14.Ghost;
using Content.Shared._RMC14.Mentor.ImaginaryFriend;
using Content.Shared._RMC14.Xenonids.Construction.Nest;
using Content.Shared.Examine;
using Content.Shared.Ghost.Components;
using Content.Shared.Ghost.Systems;
using Content.Shared.Interaction.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using ServerGhostSystem = Content.Server.Ghost.GhostSystem;

namespace Content.IntegrationTests.Tests.Minds;

[TestFixture]
[TestOf(typeof(ServerGhostSystem))]
public sealed class GhostMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: GhostMergeSubject
  components:
  - type: Ghost

- type: entity
  id: GhostMergeInteractionBypass
  components:
  - type: RMCIgnoreGhostInteractionLimits
";

    [Test]
    public async Task InteractionBypassAndImaginaryFriendExamineUseSuccessorComponents()
    {
        await Server.WaitAssertion(() =>
        {
            var ghostSystem = Server.System<ServerGhostSystem>();
            var localization = Server.ResolveDependency<ILocalizationManager>();
            var ghost = SEntMan.Spawn("GhostMergeSubject");
            var plainTarget = SEntMan.Spawn();
            var bypassTarget = SEntMan.Spawn("GhostMergeInteractionBypass");

            var plainAttempt = new InteractionAttemptEvent(ghost, plainTarget);
            SEntMan.EventBus.RaiseLocalEvent(ghost, ref plainAttempt);
            var bypassAttempt = new InteractionAttemptEvent(ghost, bypassTarget);
            SEntMan.EventBus.RaiseLocalEvent(ghost, ref bypassAttempt);
            Assert.Multiple(() =>
            {
                Assert.That(plainAttempt.Cancelled, Is.True,
                    "ordinary targets remain protected from non-interactive ghosts");
                Assert.That(bypassAttempt.Cancelled, Is.False,
                    "RMCIgnoreGhostInteractionLimits bypasses only the ghost interaction restriction");
            });

            var ghostComponent = SEntMan.GetComponent<GhostComponent>(ghost);
            ghostSystem.SetTimeOfDeath((ghost, ghostComponent), SGameTiming.RealTime - TimeSpan.FromMinutes(3));
            var expectedDeathLine = localization.GetString("comp-ghost-examine-time-minutes", ("minutes", 3));

            var normalExamine = new ExaminedEvent(new FormattedMessage(), ghost, plainTarget, true, false);
            SEntMan.EventBus.RaiseLocalEvent(ghost, normalExamine);
            Assert.That(normalExamine.GetTotalMessage().ToMarkup(), Does.Contain(expectedDeathLine));

            SEntMan.EnsureComponent<ImaginaryFriendComponent>(ghost);
            var imaginaryExamine = new ExaminedEvent(new FormattedMessage(), ghost, plainTarget, true, false);
            SEntMan.EventBus.RaiseLocalEvent(ghost, imaginaryExamine);
            Assert.That(imaginaryExamine.GetTotalMessage().ToMarkup(), Does.Not.Contain(expectedDeathLine),
                "an imaginary friend must not reveal ghost time-of-death information");
        });
    }

    [Test]
    public async Task XenoNestedGhostAttemptRecordsMindUserIdEntityLocally()
    {
        var player = Server.PlayerMan.Sessions.Single();
        await Server.WaitAssertion(() =>
        {
            var mindSystem = Server.System<MindSystem>();
            var mind = mindSystem.CreateMind(player.UserId, "Nested victim");
            mindSystem.SetUserId(mind, player.UserId);
            var nested = SEntMan.Spawn();
            var other = SEntMan.Spawn();
            var nestedComponent = SEntMan.EnsureComponent<XenoNestedComponent>(nested);

            var wrongTarget = new GhostAttemptEvent(mind);
            SEntMan.EventBus.RaiseLocalEvent(other, ref wrongTarget);
            Assert.That(nestedComponent.GhostedId, Is.Null,
                "the nested-user bridge must be entity-local");

            var nestedAttempt = new GhostAttemptEvent(mind);
            SEntMan.EventBus.RaiseLocalEvent(nested, ref nestedAttempt);
            Assert.Multiple(() =>
            {
                Assert.That(nestedAttempt.Cancelled, Is.False);
                Assert.That(nestedComponent.GhostedId, Is.EqualTo(player.UserId));
            });
        });
    }

}
