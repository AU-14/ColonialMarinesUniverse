#pragma warning disable RA0002 // Shorten fixture durations; exercise access and transitions through real interactions.

using System.Numerics;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.CMU14.Fob;
using Content.Server.GameTicking.Events;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Fob;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Fob;

[TestFixture]
[TestOf(typeof(CMUFobInteractionSystem))]
public sealed class CMUFobInteractionTest : InteractionTest
{
    private Entity<CMUFobNetworkComponent> _network;
    private EntityUid _otherRelay;
    private CMUFobReserveSystem Reserve => Server.System<CMUFobReserveSystem>();
    private CMUFobInteractionSystem Devices => Server.System<CMUFobInteractionSystem>();

    private void ConfigureMarine(EntityUid user, int construction = 3)
    {
        SEntMan.EnsureComponent<MarineComponent>(user).Faction = "govfor";
        SEntMan.EnsureComponent<MobStateComponent>(user);
        Server.System<MobStateSystem>().ChangeMobState(user, MobState.Alive);
        Server.System<SkillsSystem>().SetSkill(user, "RMCSkillConstruction", construction);
        Server.System<SkillsSystem>().SetSkill(user, "RMCSkillEngineer", 0);
    }

    private void ShortRepair(EntityUid device)
    {
        var repair = SEntMan.GetComponent<CMUFobRepairableComponent>(device);
        repair.TrainedDuration = TimeSpan.FromSeconds(1);
        repair.UntrainedDuration = TimeSpan.FromSeconds(2);
    }

    private async Task SetupEquipment(string prototype = "CMUFobRelay", bool enabled = true)
    {
        await SetTile(Plating, TargetCoords);
        await SpawnTarget(prototype);
        await Server.WaitAssertion(() =>
        {
            ConfigureMarine(SPlayer);
            SEntMan.EnsureComponent<DamageableComponent>(SPlayer);
            SEntMan.EnsureComponent<InjurableComponent>(SPlayer);
            Server.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CMUFobReserveEnabled, enabled);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundStartingEvent(1));
            ShortRepair(STarget!.Value);
            if (!enabled)
                return;

            Assert.That(Reserve.TryCreateNetwork(STarget.Value, "govfor", out _network), Is.True);
            _network.Comp.StoredReserve = TimeSpan.FromSeconds(40);
            _network.Comp.Capacity = TimeSpan.FromSeconds(60);
            _network.Comp.RelayReward = TimeSpan.FromSeconds(30);
            _network.Comp.StartupDuration = TimeSpan.FromSeconds(0.2);
            _network.Comp.MinimumActivation = TimeSpan.FromSeconds(2);
            _network.Comp.RestartDelay = TimeSpan.FromSeconds(1);
            if (prototype != "CMUFobRelay")
                return;

            _otherRelay = SEntMan.SpawnAtPosition("CMUFobRelay",
                SEntMan.GetCoordinates(TargetCoords).Offset(new Vector2(0, 1)));
            ShortRepair(_otherRelay);
            Assert.That(Reserve.TryOfferRelays(_network, STarget.Value, _otherRelay, out _), Is.True);
        });
    }

    private void Hurt(EntityUid uid, int amount = 1)
    {
        Server.System<DamageableSystem>().TryChangeDamage(uid,
            new DamageSpecifier { DamageDict = new() { ["Blunt"] = amount } }, ignoreResistances: true);
    }

    [TestCase("CMScrewdriver")]
    [TestCase("RMCWelderAdmin")]
    public async Task PhysicalRelayRequiresHeldToolAndTransfersOneCappedReward(string repairTool)
    {
        await SetupEquipment();
        await PlaceInHands("CMCrowbar");
        await Interact(awaitDoAfters: false);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<CMUFobRepairableComponent>(STarget!.Value).User, Is.Null));
        await PlaceInHands(repairTool);
        await Interact(awaitDoAfters: false);
        await Server.WaitAssertion(() =>
        {
            var repair = SEntMan.GetComponent<CMUFobRepairableComponent>(STarget!.Value);
            Assert.That(DoAfterSys.IsRunning(repair.DoAfter), Is.True);
            Assert.That(Reserve.GetRemaining(_network), Is.EqualTo(TimeSpan.FromSeconds(40)));
        });
        await RunSeconds(1.3f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Reserve.GetRemaining(_network), Is.EqualTo(TimeSpan.FromSeconds(60)));
            Assert.That(SEntMan.GetComponent<CMUFobRepairableComponent>(STarget!.Value).User, Is.Null);
            Assert.That(Reserve.TryGetRelayJob(_otherRelay, out _, out _), Is.False);
        });
        await Interact(awaitDoAfters: false);
        await RunSeconds(1.3f);
        await Server.WaitAssertion(() =>
            Assert.That(Reserve.GetRemaining(_network), Is.EqualTo(TimeSpan.FromSeconds(60))));
    }

    [Test]
    public async Task InterruptedRepairsNeverAwardAndReleaseTheOperatorLock()
    {
        await SetupEquipment();
        foreach (var interruption in new[] { "tool", "movement", "relay damage", "operator damage", "faction", "critical" })
        {
            var tool = SEntMan.GetEntity(await PlaceInHands("CMScrewdriver"));
            await Server.WaitAssertion(() =>
            {
                ConfigureMarine(SPlayer);
                Transform.SetCoordinates(SPlayer, SEntMan.GetCoordinates(PlayerCoords));
                Assert.That(Devices.TryStartRepair((STarget!.Value,
                    SEntMan.GetComponent<CMUFobRepairableComponent>(STarget.Value)), SPlayer, tool), Is.True,
                    $"The previous {interruption} attempt must not leave a lock.");
            });
            await RunSeconds(0.15f);
            await Client.WaitAssertion(() =>
            {
                Assert.That(Client.System<SharedAppearanceSystem>().TryGetData<CMUFobPresentationStatus>(
                    CTarget!.Value, CMUFobPresentationVisuals.Status, out var status), Is.True);
                Assert.That(status, Is.EqualTo(CMUFobPresentationStatus.Working),
                    "Observers must see the in-progress repair before it is interrupted.");
            });
            if (interruption == "tool")
                await Drop();
            else
            {
                await Server.WaitPost(() =>
                {
                    switch (interruption)
                    {
                        case "movement":
                            Transform.SetCoordinates(SPlayer,
                                SEntMan.GetCoordinates(PlayerCoords).Offset(new Vector2(5, 0)));
                            break;
                        case "relay damage": Hurt(STarget!.Value); break;
                        case "operator damage": Hurt(SPlayer); break;
                        case "faction": SEntMan.GetComponent<MarineComponent>(SPlayer).Faction = "opfor"; break;
                        case "critical": Server.System<MobStateSystem>().ChangeMobState(SPlayer, MobState.Critical); break;
                    }
                });
            }
            await RunSeconds(1.2f);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Reserve.GetRemaining(_network), Is.EqualTo(TimeSpan.FromSeconds(40)), interruption);
                Assert.That(SEntMan.GetComponent<CMUFobRepairableComponent>(STarget!.Value).User, Is.Null, interruption);
            });
            await Client.WaitAssertion(() =>
            {
                Assert.That(Client.System<SharedAppearanceSystem>().TryGetData<CMUFobPresentationStatus>(
                    CTarget!.Value, CMUFobPresentationVisuals.Status, out var status), Is.True);
                Assert.That(status, Is.EqualTo(CMUFobPresentationStatus.Ready),
                    $"The {interruption} cancellation must clear the working effect on observers too.");
            });
        }
    }

    [Test]
    public async Task FasterAlternativeCancelsOtherRepairAndOperatorsCannotStack()
    {
        await SetupEquipment();
        var tool = SEntMan.GetEntity(await PlaceInHands("CMScrewdriver"));
        EntityUid other = default;
        await Server.WaitAssertion(() =>
        {
            other = SEntMan.SpawnAtPosition("InteractionTestMob", SEntMan.GetCoordinates(PlayerCoords));
            ConfigureMarine(other, construction: 0);
            var otherTool = SEntMan.SpawnAtPosition("CMScrewdriver", SEntMan.GetCoordinates(PlayerCoords));
            Assert.That(HandSys.TryPickupAnyHand(other, otherTool), Is.True);
            var target = STarget!.Value;
            Assert.That(Devices.TryStartRepair((target, SEntMan.GetComponent<CMUFobRepairableComponent>(target)),
                SPlayer, tool), Is.True);
            Assert.That(Devices.TryStartRepair((target, SEntMan.GetComponent<CMUFobRepairableComponent>(target)),
                other, otherTool), Is.False);
            Assert.That(Devices.TryStartRepair((_otherRelay, SEntMan.GetComponent<CMUFobRepairableComponent>(_otherRelay)),
                other, otherTool), Is.True);
        });
        await RunSeconds(1.3f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Reserve.GetRemaining(_network), Is.EqualTo(TimeSpan.FromSeconds(60)));
            Assert.That(SEntMan.GetComponent<CMUFobRepairableComponent>(_otherRelay).User, Is.Null);
        });
        await RunSeconds(1.2f);
        await Server.WaitAssertion(() =>
            Assert.That(Reserve.GetRemaining(_network), Is.EqualTo(TimeSpan.FromSeconds(60))));
    }

    [Test]
    public async Task ControlUiEnforcesTrainingTimersAndRechecksFactionAndReach()
    {
        await SetupEquipment("CMUFobControl");
        await Server.WaitPost(() => ConfigureMarine(SPlayer, construction: 0));
        await Interact();
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.System<SharedUserInterfaceSystem>().IsUiOpen(STarget!.Value, CMUFobControlUiKey.Key),
                Is.False));
        await Server.WaitPost(() => ConfigureMarine(SPlayer));
        await Interact();
        Assert.That(TryGetBui(CMUFobControlUiKey.Key, out _), Is.True);
        await SendBui(CMUFobControlUiKey.Key, new CMUFobControlMessage(true));
        await SendBui(CMUFobControlUiKey.Key, new CMUFobControlMessage(false));
        await Server.WaitAssertion(() =>
            Assert.That(_network.Comp.State, Is.EqualTo(CMUFobReserveState.Active)));
        await RunSeconds(2.1f);
        await SendBui(CMUFobControlUiKey.Key, new CMUFobControlMessage(false));
        await Server.WaitAssertion(() =>
        {
            Assert.That(_network.Comp.State, Is.EqualTo(CMUFobReserveState.Standby));
            SEntMan.GetComponent<MarineComponent>(SPlayer).Faction = "opfor";
        });
        await RunSeconds(1.1f);
        await SendBui(CMUFobControlUiKey.Key, new CMUFobControlMessage(true));
        await Server.WaitAssertion(() => Assert.That(_network.Comp.State, Is.EqualTo(CMUFobReserveState.Standby)));
        await Server.WaitPost(() => ConfigureMarine(SPlayer));
        await Interact();
        await Server.WaitPost(() => Transform.SetCoordinates(SPlayer,
            SEntMan.GetCoordinates(PlayerCoords).Offset(new Vector2(6, 0))));
        await SendBui(CMUFobControlUiKey.Key, new CMUFobControlMessage(true));
        await Server.WaitAssertion(() => Assert.That(_network.Comp.State, Is.EqualTo(CMUFobReserveState.Standby)));
    }

    [Test]
    public async Task BrokenControlStopsSupportAndRepairOrReplacementNeverRefillsIt()
    {
        await SetupEquipment("CMUFobControl");
        await Server.WaitAssertion(() =>
        {
            Assert.That(Devices.TryBindControl((STarget!.Value,
                SEntMan.GetComponent<CMUFobControlComponent>(STarget.Value)), out _), Is.True);
            Assert.That(Reserve.TryActivate(_network), Is.True);
            Hurt(STarget.Value, 150);
            Assert.That(SEntMan.GetComponent<CMUFobControlComponent>(STarget.Value).Broken, Is.True);
            Assert.That(_network.Comp.State, Is.EqualTo(CMUFobReserveState.Standby));
            Assert.That(Reserve.GetRemaining(_network), Is.EqualTo(TimeSpan.FromSeconds(38)));
        });
        await Interact();
        await Server.WaitAssertion(() => Assert.That(SEntMan.System<SharedUserInterfaceSystem>()
            .IsUiOpen(STarget!.Value, CMUFobControlUiKey.Key), Is.False));
        await PlaceInHands("CMScrewdriver");
        await Interact(awaitDoAfters: false);
        await RunSeconds(1.3f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<CMUFobControlComponent>(STarget!.Value).Broken, Is.False);
            Assert.That(Server.System<DamageableSystem>().GetTotalDamage(STarget.Value), Is.EqualTo(FixedPoint2.Zero));
            Assert.That(_network.Comp.State, Is.EqualTo(CMUFobReserveState.Standby));
            Assert.That(Reserve.GetRemaining(_network), Is.EqualTo(TimeSpan.FromSeconds(38)));
            var duplicate = SEntMan.SpawnAtPosition("CMUFobControl", SEntMan.GetCoordinates(TargetCoords));
            Assert.That(Devices.TryBindControl((duplicate, SEntMan.GetComponent<CMUFobControlComponent>(duplicate)),
                out _), Is.False);
            SEntMan.DeleteEntity(STarget.Value);
            Assert.That(Devices.TryBindControl((duplicate, SEntMan.GetComponent<CMUFobControlComponent>(duplicate)),
                out var existing), Is.True);
            Assert.That(existing.Owner, Is.EqualTo(_network.Owner));
            Assert.That(Reserve.GetRemaining(existing), Is.EqualTo(TimeSpan.FromSeconds(38)));
            Assert.That(Reserve.TryActivate(existing), Is.True);
            Transform.Unanchor(duplicate);
            Assert.That(existing.Comp.State, Is.EqualTo(CMUFobReserveState.Standby));
            Assert.That(Reserve.GetRemaining(existing), Is.EqualTo(TimeSpan.FromSeconds(36)));
            Assert.That(Devices.TryBindControl((duplicate, SEntMan.GetComponent<CMUFobControlComponent>(duplicate)),
                out _), Is.False);
        });
    }

    [Test]
    public async Task DisabledEquipmentCannotCreateBanksOpenControlsOrStartRepairs()
    {
        await SetupEquipment("CMUFobControl", enabled: false);
        await Interact();
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<SharedUserInterfaceSystem>().IsUiOpen(STarget!.Value, CMUFobControlUiKey.Key),
                Is.False);
            Assert.That(Reserve.TryGetNetwork("govfor", out _), Is.False);
        });
        await SpawnTarget("CMUFobRelay");
        await PlaceInHands("CMScrewdriver");
        await Interact(awaitDoAfters: false);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<CMUFobRepairableComponent>(STarget!.Value).User, Is.Null));
    }
}
