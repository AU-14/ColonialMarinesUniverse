#pragma warning disable RA0002 // Shorten reserve timers while exercising real deployment and player movement.

using System.Numerics;
using Content.IntegrationTests.Tests.Movement;
using Content.Server.CMU14.Fob;
using Content.Server.GameTicking.Events;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Skills;
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

    [TestCase(false)]
    [TestCase(true)]
    public async Task StationaryDeployedFenceBlocksWalkingUntilRecovered(bool powered)
    {
        await SpawnTarget("CMUFobFence");
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
        await RunSeconds(1.2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<StunnedComponent>(SPlayer), Is.False, "Contact stun must expire.");
            if (powered) Assert.That(reserve.TryDeactivate(network), Is.True);
        });

        var cutters = ToServer(await PlaceInHands("CMWirecutter"));
        await Server.WaitAssertion(() => Assert.That(equipment.TryStartWork(
            (fence, SEntMan.GetComponent<CMUFobEquipmentComponent>(fence)), SPlayer, cutters), Is.True));
        await Move(DirectionFlag.East, 1);
        await Server.WaitAssertion(() => Assert.That(Delta(), Is.LessThan(-0.5f),
            "Recovering the fence must clear the physical barrier."));
    }
}
