#pragma warning disable RA0002 // Arrange a tracking cache to verify immediate IFF removal.

using System;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.TacticalMap;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Overwatch;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.CMU14.Weapons.Ranged.IFF;
using Content.Shared.Cuffs;
using Content.Shared.Damage.Components;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.NPC.Systems;
using Content.Shared.Standing;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Weapons;

[TestFixture]
public sealed class CMUIFFRemovalTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [TestCase(true, false, 15, 10, 5)]
    [TestCase(false, true, 10, 5, 2.5f)]
    [TestCase(false, false, 30, 35, 15)]
    public async Task RemovalRequiresTimeAndRemainsRemovedWithAnotherCard(
        bool self, bool trained, float seconds, float brute, float poison)
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default, target = default, knife = default, card = default, console = default;
        await Server.WaitAssertion(() =>
        {
            user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            target = self ? user : SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.EnsureComponent<MarineComponent>(target).Faction = "govfor";
            knife = SEntMan.SpawnEntity("RMCM5Bayonet", map.GridCoords);
            card = SEntMan.SpawnEntity("AU14IDCardGOVFORPlatCo", map.GridCoords);
            console = SEntMan.SpawnEntity("RMCOverwatchConsoleGovfor", map.GridCoords);
            var squad = SEntMan.SpawnEntity("SquadGovforBravo", MapCoordinates.Nullspace);
            Server.System<SquadSystem>().AssignSquad(target, squad, null);
            var hands = Server.System<SharedHandsSystem>();
            Assert.That(hands.TryPickupAnyHand(user, knife), Is.True);
            Assert.That(Server.System<InventorySystem>().TryEquip(target, card, "id", force: true), Is.True);
            var iff = Server.System<GunIFFSystem>();
            iff.SetUserFaction(target, "GOVFOR");
            Server.System<NpcFactionSystem>().AddFaction(target, "GOVFOR");
            if (!self)
            {
                iff.SetUserFaction(user, "OPFOR");
                Assert.That(Server.System<StandingStateSystem>().Down(target, playSound: false), Is.True);
            }
            if (trained)
                Server.System<SkillsSystem>().SetSkill(user, "RMCSkillSurgery", 1);

            var verbs = GetVerbs(user, target, knife);
            var verb = verbs.Verbs.Single(v => v.Text == "Remove IFF");
            verb.Act!();
            Assert.That(SEntMan.GetComponent<DoAfterComponent>(user).DoAfters.Values.Single().Args.Delay,
                Is.EqualTo(TimeSpan.FromSeconds(seconds)));
        });

        await Pair.RunSeconds(seconds - 1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<CMUIFFRemovedComponent>(target), Is.False);
            var state = Server.System<SharedOverwatchConsoleSystem>().GetOverwatchBuiState(
                (console, SEntMan.GetComponent<OverwatchConsoleComponent>(console)));
            Assert.That(state.Marines.Values.SelectMany(m => m).Any(m => m.Id == SEntMan.GetNetEntity(target)),
                Is.True, "The target is visible on overwatch before IFF removal.");
        });
        await Pair.RunSeconds(1.5f);
        await Server.WaitAssertion(() =>
        {
            var iff = Server.System<GunIFFSystem>();
            var damage = SEntMan.GetComponent<DamageableComponent>(target).Damage.DamageDict;
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<CMUIFFRemovedComponent>(target), Is.True);
                Assert.That(iff.IsInFaction(target, "GOVFOR"), Is.False);
                Assert.That(Server.System<NpcFactionSystem>().IsMember(target, "GOVFOR"), Is.False);
                Assert.That(SEntMan.HasComponent<ItemIFFComponent>(card), Is.True, "Removal must not destroy the card's IFF.");
                Assert.That(damage["Blunt"].Float(), Is.EqualTo(brute).Within(0.01));
                Assert.That(damage["Poison"].Float(), Is.EqualTo(poison).Within(0.01));
            });
            var state = Server.System<SharedOverwatchConsoleSystem>().GetOverwatchBuiState(
                (console, SEntMan.GetComponent<OverwatchConsoleComponent>(console)));
            Assert.That(state.Marines.Values.SelectMany(m => m).Any(m => m.Id == SEntMan.GetNetEntity(target)),
                Is.False, "Removal must hide the target from overwatch.");

            var factions = new System.Collections.Generic.HashSet<Robust.Shared.Prototypes.EntProtoId<IFFFactionComponent>>();
            var ev = new GetIFFFactionEvent(SlotFlags.IDCARD, factions);
            SEntMan.EventBus.RaiseLocalEvent(target, ref ev);
            Assert.That(factions, Is.Empty, "Direct IFF queries must not relay the equipped card.");
            var inventory = Server.System<InventorySystem>();
            Assert.That(inventory.TryUnequip(target, "id", force: true), Is.True);
            var replacement = SEntMan.SpawnEntity("AU14IDCardGOVFORPlatCo", map.GridCoords);
            Assert.That(inventory.TryEquip(target, replacement, "id", force: true), Is.True);
            Assert.That(SEntMan.HasComponent<ItemIFFComponent>(replacement), Is.True);
            Assert.That(iff.IsInFaction(target, "GOVFOR"), Is.False, "A replacement ID must not restore IFF.");
            iff.AddUserFaction(target, "OPFOR");
            Assert.That(iff.IsInFaction(target, "OPFOR"), Is.False, "Later faction or ID changes must not restore IFF.");
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public async Task OtherRemovalRequiresDownTargetAndIsInterrupted(int interruption)
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default, target = default, knife = default;
        await Server.WaitAssertion(() =>
        {
            user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            target = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.EnsureComponent<MarineComponent>(target).Faction = "opfor";
            knife = SEntMan.SpawnEntity("RMCM5Bayonet", map.GridCoords);
            Server.System<GunIFFSystem>().SetUserFaction(target, "OPFOR");
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(user, knife), Is.True);
            Assert.That(GetVerbs(user, target, knife).Verbs.Any(v => v.Text == "Remove IFF"), Is.False);
            Assert.That(Server.System<StandingStateSystem>().Down(target, playSound: false), Is.True);
            GetVerbs(user, target, knife).Verbs.Single(v => v.Text == "Remove IFF").Act!();
        });
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            if (interruption == 0)
                Assert.That(Server.System<SharedHandsSystem>().TryDrop(user, knife), Is.True);
            else if (interruption == 1)
                Server.System<SharedTransformSystem>().SetCoordinates(user, map.GridCoords.Offset(new System.Numerics.Vector2(5, 0)));
            else
                Assert.That(Server.System<StandingStateSystem>().Stand(target, force: true), Is.True);
        });
        await Pair.RunSeconds(31);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.HasComponent<CMUIFFRemovedComponent>(target), Is.False));
    }

    [Test]
    public async Task StandingCuffedTargetCanBeSelected()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var target = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.EnsureComponent<MarineComponent>(target).Faction = "opfor";
            var knife = SEntMan.SpawnEntity("RMCM5Bayonet", map.GridCoords);
            Server.System<GunIFFSystem>().SetUserFaction(target, "OPFOR");
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(user, knife), Is.True);
            Assert.That(Server.System<SharedCuffableSystem>().TryAddCuffsInstant(target, "RMCHandcuffs"), Is.Not.Null);
            Assert.That(GetVerbs(user, target, knife).Verbs.Any(v => v.Text == "Remove IFF"), Is.True);
        });
    }

    [Test]
    public async Task RemovedIFFClearsAndDoesNotRecreateTacticalBlips()
    {
        var map = await Pair.CreateTestMap();
        EntityUid target = default, trackingMap = default;
        await Server.WaitAssertion(() =>
        {
            target = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            trackingMap = SEntMan.SpawnEntity(null, map.GridCoords);
            var tracking = SEntMan.EnsureComponent<ActiveTacticalMapTrackedComponent>(target);
            var tactical = SEntMan.EnsureComponent<TacticalMapComponent>(trackingMap);
            tracking.Map = trackingMap;
            tactical.GovforBlips[target.Id] = new TacticalMapBlip();
            tactical.OpforBlips[target.Id] = new TacticalMapBlip();
            SEntMan.EnsureComponent<CMUIFFRemovedComponent>(target);

            Server.System<TacticalMapSystem>().OnIFFRemoved(target);
            Assert.That(tactical.GovforBlips, Does.Not.ContainKey(target.Id));
            Assert.That(tactical.OpforBlips, Does.Not.ContainKey(target.Id));
            Assert.That(tactical.MapDirty, Is.True);

            Server.System<SharedTransformSystem>().SetCoordinates(target, map.GridCoords.Offset(new System.Numerics.Vector2(1, 0)));
        });
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            var tactical = SEntMan.GetComponent<TacticalMapComponent>(trackingMap);
            Assert.That(tactical.GovforBlips, Does.Not.ContainKey(target.Id));
            Assert.That(tactical.OpforBlips, Does.Not.ContainKey(target.Id));
        });
    }

    private GetVerbsEvent<Verb> GetVerbs(EntityUid user, EntityUid target, EntityUid knife)
    {
        var verbs = new GetVerbsEvent<Verb>(user, target, knife,
            SEntMan.GetComponent<HandsComponent>(user), true, true, true, new());
        SEntMan.EventBus.RaiseLocalEvent(target, verbs, true);
        return verbs;
    }
}
