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

    [Test]
    public async Task RichWarpResponseRoundTripsEveryForkField()
    {
        await Server.WaitAssertion(() =>
        {
            var serializer = Server.ResolveDependency<IRobustSerializer>();
            var target = SEntMan.Spawn();
            var warp = new GhostWarp(
                SEntMan.GetNetEntity(target),
                "Senior Xenomorph",
                false,
                "Praetorian",
                "Xenos",
                "Tier Three",
                "CMJobIconXenoPraetorian",
                "CMXenoPraetorian",
                "CMXenoPraetorian",
                42,
                3,
                true);
            var response = new GhostWarpsResponseEvent([warp]);

            using var stream = new MemoryStream();
            serializer.Serialize(stream, response);
            stream.Position = 0;
            var roundTripped = serializer.Deserialize<GhostWarpsResponseEvent>(stream);
            var result = roundTripped.Warps.Single();

            Assert.Multiple(() =>
            {
                Assert.That(result.Entity, Is.EqualTo(warp.Entity));
                Assert.That(result.DisplayName, Is.EqualTo("Senior Xenomorph"));
                Assert.That(result.IsWarpPoint, Is.False);
                Assert.That(result.RoleName, Is.EqualTo("Praetorian"));
                Assert.That(result.Tab, Is.EqualTo("Xenos"));
                Assert.That(result.Section, Is.EqualTo("Tier Three"));
                Assert.That(result.IconPrototype, Is.EqualTo("CMJobIconXenoPraetorian"));
                Assert.That(result.EntityPrototype, Is.EqualTo("CMXenoPraetorian"));
                Assert.That(result.JobPrototype, Is.EqualTo("CMXenoPraetorian"));
                Assert.That(result.SortWeight, Is.EqualTo(42));
                Assert.That(result.XenoTier, Is.EqualTo(3));
                Assert.That(result.IsXeno, Is.True);
            });
        });
    }
}
