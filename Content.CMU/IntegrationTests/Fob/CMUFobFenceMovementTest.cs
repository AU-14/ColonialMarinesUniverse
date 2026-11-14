#pragma warning disable RA0002 // Shorten reserve timers while exercising real deployment and player movement.

using System.Numerics;
using Content.IntegrationTests.Tests.Movement;
using Content.Server.CMU14.Fob;
using Content.Server.GameTicking.Events;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Sentry;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Fob;
using Content.Shared.Damage.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.Fob;

[TestFixture]
public sealed class CMUFobFenceMovementTest : MovementTest
{
    protected override string PlayerPrototype => "CMMobHuman";
    protected override int Tiles => 5;

    [TestCase("CMUFobControl")]
    [TestCase("CMUFobRelay")]
    public async Task AnchoredDevicesBlockWalking(string prototype)
    {
        await SpawnTarget(prototype);
        await RunSeconds(2);
        await Move(DirectionFlag.East, 1);
        await Server.WaitAssertion(() => Assert.That(Delta(), Is.GreaterThan(0.4f),
            "A deployed terminal or relay must remain a physical obstacle after settling."));

        if (prototype == "CMUFobControl")
        {
            await Server.WaitPost(() => Server.System<SharedTransformSystem>().Unanchor(STarget!.Value));
            await Move(DirectionFlag.East, 1);
            await Server.WaitAssertion(() => Assert.That(Delta(), Is.LessThan(-0.5f),
                "Unanchoring the control must clear its blocking footprint immediately."));
        }
    }

    [TestCase("CMUFobFence", false)]
    [TestCase("CMUFobFence", true)]
    [TestCase("CMUFobIlluminator", false)]
    [TestCase("CMUFobGenerator", false)]
    public async Task DeployedEquipmentBlocksWalkingUntilRecovered(string prototype, bool powered)
    {
        await SpawnTarget(prototype);
        var fence = STarget!.Value;
        var equipment = Server.System<CMUFobEquipmentSystem>();
        var reserve = Server.System<CMUFobReserveSystem>();
        Entity<CMUFobNetworkComponent> network = default;
        var wrench = ToServer(await PlaceInHands("CMWrench"));
        await Server.WaitAssertion(() =>
        {
            Server.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CMUFobReserveEnabled, true);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundStartingEvent(1));
            SEntMan.EnsureComponent<MarineComponent>(SPlayer).Faction = "govfor";
            Server.System<SkillsSystem>().SetSkill(SPlayer, "RMCSkillConstruction", 3);
            Server.System<TagSystem>().AddTag(SPlayer, "InstantDoAfters");
            var control = SEntMan.SpawnAtPosition("CMUFobControl", SEntMan.GetCoordinates(PlayerCoords).Offset(new Vector2(-2, 0)));
            Assert.That(reserve.TryCreateNetwork(control, "govfor", out network), Is.True);
            Assert.That(Server.System<CMUFobInteractionSystem>().TryBindControl(
                (control, SEntMan.GetComponent<CMUFobControlComponent>(control)), out _), Is.True);
            network.Comp.StartupDuration = TimeSpan.FromSeconds(0.2);
            network.Comp.MinimumActivation = TimeSpan.FromSeconds(0.2);
            Assert.That(equipment.TryStartWork((fence, SEntMan.GetComponent<CMUFobEquipmentComponent>(fence)),
                SPlayer, wrench), Is.True);
            if (powered) Assert.That(reserve.TryActivate(network), Is.True);
        });
        // Let the inherited item physics settle before approaching the fence.
        await RunSeconds(2);
        await Move(DirectionFlag.East, 0.4f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Delta(), Is.GreaterThan(0.4f),
                "A deployed fence must physically stop a walking marine even after it becomes stationary.");
            Assert.That(Server.System<DamageableSystem>().GetTotalDamage(SPlayer) > 0, Is.EqualTo(powered),
                "Only powered contact shocks the marine.");
            Assert.That(SEntMan.HasComponent<StunnedComponent>(SPlayer), Is.EqualTo(powered));
        });
        await Client.WaitAssertion(() => Assert.That(CEntMan.HasComponent<StunnedComponent>(ToClient(SPlayer)), Is.EqualTo(powered),
            "The contact stun must reach the marine's client."));
        await Client.WaitAssertion(() => Assert.That(
            CEntMan.System<SharedTransformSystem>().GetWorldPosition(ToClient(fence)).X
            - CEntMan.System<SharedTransformSystem>().GetWorldPosition(CPlayer).X, Is.GreaterThan(0.4f),
            "The client must also collide with the deployed equipment."));
        await RunSeconds(1.2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<StunnedComponent>(SPlayer), Is.False, "Contact stun must expire.");
            if (powered) Assert.That(reserve.TryDeactivate(network), Is.True);
        });

        var cutters = ToServer(await PlaceInHands(prototype == "CMUFobFence" ? "CMWirecutter" : "CMWrench"));
        await Server.WaitAssertion(() => Assert.That(equipment.TryStartWork(
            (fence, SEntMan.GetComponent<CMUFobEquipmentComponent>(fence)), SPlayer, cutters), Is.True));
        await Move(DirectionFlag.East, 1);
        await Server.WaitAssertion(() => Assert.That(Delta(), Is.LessThan(-0.5f),
            "Recovering the fence must clear the physical barrier."));
    }

    [TestCase("CMUFobHeavyModule")]
    [TestCase("CMUFobControlModule")]
    [TestCase("CMUFobPlatformModule")]
    public async Task FoldedCargoDoesNotBlockWalkingAfterStateRefresh(string prototype)
    {
        await SpawnTarget(prototype);
        await Server.WaitPost(() =>
        {
            if (SEntMan.TryGetComponent<SentryComponent>(STarget, out var sentry))
            {
                var system = Server.System<SentrySystem>();
                system.TrySetMode((STarget!.Value, sentry), SentryMode.On);
                system.TrySetMode((STarget.Value, sentry), SentryMode.Off);
            }
        });
        await RunSeconds(2);
        await Move(DirectionFlag.East, 1);
        await Server.WaitAssertion(() => Assert.That(Delta(), Is.LessThan(-0.5f),
            "Folded equipment must stay passable after becoming stationary and receiving a mode refresh."));
    }
}
