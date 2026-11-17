#pragma warning disable RA0002 // reads the caster's mode and sets the bracer's charge for the test
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared._RMC14.Weapons.Common;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Interaction.Events;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// the lethal and eradicator unlocks through the caster's own controls, not just the gate event
[TestFixture]
public sealed class BadBloodCasterLethalRealPathTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    // the unique action (Space): refused before the unlock, after it the caster switches, the shot is allowed and a
    // lethal bolt comes out
    [Test]
    public async Task BoughtLethalBoltSwitchesAndFiresThroughTheCaster()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var grunt = party.Grunts[0];
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var unlocks = Server.System<BadBloodEradicatorUnlockSystem>();
                unlocks.SyncHolders();

                var (caster, casterComp) = DeployedCaster(grunt, out var bracer);
                entMan.GetComponent<YautjaBracerComponent>(bracer).Charge = 5000;

                entMan.EventBus.RaiseLocalEvent(caster, new UniqueActionEvent(grunt));
                Assert.That(casterComp.CurrentMode, Is.EqualTo(BadBloodEradicatorSystem.StunMode),
                    "before the unlock the unique action refuses the lethal bolt");

                Assert.That(unlocks.UnlockLethal(), Is.True);

                entMan.EventBus.RaiseLocalEvent(caster, new UniqueActionEvent(grunt));
                Assert.That(casterComp.CurrentMode, Is.EqualTo(BadBloodEradicatorSystem.LethalMode),
                    "after the unlock the unique action switches to the lethal bolt");

                var coords = entMan.GetComponent<TransformComponent>(grunt).Coordinates;
                var attempt = new AttemptShootEvent(grunt, null, coords, coords);
                entMan.EventBus.RaiseLocalEvent(caster, ref attempt);
                Assert.That(attempt.Cancelled, Is.False, $"the lethal shot is allowed ({attempt.Message})");

                var ammo = new TakeAmmoEvent(1, new List<(EntityUid? Entity, IShootable Shootable)>(), coords, grunt);
                entMan.EventBus.RaiseLocalEvent(caster, ammo);
                Assert.That(ammo.Ammo, Has.Count.EqualTo(1), $"a bolt comes out ({ammo.Reason})");
                var bolt = ammo.Ammo[0].Entity!.Value;
                spawned.Add(bolt);
                Assert.That(entMan.HasComponent<YautjaCasterSingleLethalProjectileComponent>(bolt), Is.True,
                    "and it is the lethal bolt");

                // the once-a-second sync must not drop it back
                unlocks.SyncHolders();
                Assert.That(casterComp.CurrentMode, Is.EqualTo(BadBloodEradicatorSystem.LethalMode));
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    // use in hand (Z) is how most players switch: it has to step into the lethal bolt once bought, into the
    // eradicator for the Leader once that is bought, and skip whatever is still locked
    [Test]
    public async Task UseInHandCyclesIntoEarnedModes()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var grunt = party.Grunts[0];

        try
        {
            await Server.WaitAssertion(() =>
            {
                var unlocks = Server.System<BadBloodEradicatorUnlockSystem>();
                unlocks.SyncHolders();

                var gruntCaster = DeployedCaster(grunt, out _);
                var leaderCaster = DeployedCaster(party.Leader, out _);

                Assert.That(Cycle(gruntCaster, grunt, 3), Is.EqualTo(new[] { 1, 0, 1 }),
                    "before any unlock Z flips stun and immobilizer only");

                Assert.That(unlocks.UnlockLethal(), Is.True);
                Assert.That(Cycle(gruntCaster, grunt, 4), Is.EqualTo(new[] { 2, 0, 1, 2 }),
                    "after the lethal unlock Z steps into the lethal bolt, and a grunt never into the eradicator");

                Assert.That(unlocks.UnlockEradicator(), Is.True);
                Assert.That(Cycle(leaderCaster, party.Leader, 4), Is.EqualTo(new[] { 1, 2, 3, 0 }),
                    "the Leader's Z steps into the eradicator once it is bought");
                Assert.That(Cycle(gruntCaster, grunt, 3), Is.EqualTo(new[] { 0, 1, 2 }),
                    "a grunt's still skips it");
            });
        }
        finally
        {
            await Cleanup(party, new List<EntityUid>());
        }
    }

    // the caster the Bad Blood's own bracer carries, marked deployed so it fires from that bracer
    private Entity<YautjaCasterComponent> DeployedCaster(EntityUid badBlood, out EntityUid bracer)
    {
        var entMan = Server.EntMan;
        Assert.That(Server.System<InventorySystem>().TryGetSlotEntity(badBlood, "gloves", out var worn), Is.True,
            "a Bad Blood lands wearing a bracer");
        bracer = worn!.Value;
        var gear = entMan.GetComponent<YautjaGearContainerComponent>(bracer);
        Assert.That(gear.Gear.TryGetValue(YautjaGearKind.Caster, out var caster), Is.True,
            "the bracer carries a plasma caster");
        var stored = entMan.GetComponent<YautjaStoredGearComponent>(caster);
        stored.Bracer = bracer;
        stored.Deployed = true;
        var comp = entMan.GetComponent<YautjaCasterComponent>(caster);
        Assert.That(comp.CycleEarnedModes, Is.True, "the Bad Blood caster cycles its earned modes");
        return (caster, comp);
    }

    // presses Z on the caster that many times and returns the mode after each press
    private int[] Cycle(Entity<YautjaCasterComponent> caster, EntityUid user, int times)
    {
        var modes = new int[times];
        for (var i = 0; i < times; i++)
        {
            Server.EntMan.EventBus.RaiseLocalEvent(caster.Owner, new UseInHandEvent(user));
            modes[i] = caster.Comp.CurrentMode;
        }

        return modes;
    }

    private async Task Cleanup(BadBloodTestParty party, List<EntityUid> spawned)
    {
        await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
        await Server.WaitPost(() =>
        {
            foreach (var uid in spawned.Concat(party.All))
            {
                if (Server.EntMan.EntityExists(uid))
                    Server.EntMan.DeleteEntity(uid);
            }
        });
    }
}
