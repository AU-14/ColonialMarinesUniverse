#pragma warning disable RA0002 // the test puts the caster in each mode directly instead of driving the mode-switch input
using Content.Shared.CMU14.Yautja;
using Content.Shared.FixedPoint;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// ECS-104: the Bad Blood caster drains each mode's own cost per shot, not the cost of the last mode switch
[TestFixture]
public sealed class BadBloodCasterCostTest
{
    private const string BadBloodBracer = "CMUYautjaBadBloodBracer";
    private const string BadBloodCaster = "CMUYautjaBadBloodPlasmaCaster";
    private const string HunterCaster = "CMUYautjaPlasmaCaster";

    [Test]
    public async Task EachBadBloodCasterModeDrainsItsOwnCostPerShot()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var bracer = entMan.SpawnEntity(BadBloodBracer, map.GridCoords);
            var projectiles = new List<EntityUid>();

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaBadBloodComponent>(hunter);

                var gear = entMan.GetComponent<YautjaGearContainerComponent>(bracer);
                Assert.That(gear.Gear.TryGetValue(YautjaGearKind.Caster, out var caster), Is.True,
                    "The bad blood bracer must come with its caster installed.");
                Assert.That(entMan.GetComponent<MetaDataComponent>(caster).EntityPrototype?.ID, Is.EqualTo(BadBloodCaster),
                    "The bad blood bracer must install the Bad Blood caster, not a hunter's.");

                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                var casterComp = entMan.GetComponent<YautjaCasterComponent>(caster);
                Assert.That(casterComp.Modes, Has.Count.EqualTo(4));

                // a leftover mode-switch cost must not leak into any mode
                casterComp.PowerCost = 7;

                var costs = new HashSet<FixedPoint2>();
                for (var mode = 0; mode < casterComp.Modes.Count; mode++)
                {
                    var modeCost = casterComp.Modes[mode].PowerCost;
                    Assert.That(modeCost, Is.Not.Null, $"Bad Blood caster mode {mode} has no cost of its own.");
                    costs.Add(modeCost!.Value);

                    casterComp.CurrentMode = mode;
                    bracerComp.Charge = bracerComp.MaxCharge;
                    projectiles.Add(TakeOneShot(entMan, caster, hunter, map.GridCoords));

                    Assert.That(bracerComp.Charge, Is.EqualTo(bracerComp.MaxCharge - modeCost.Value),
                        $"Bad Blood caster mode {mode} must drain its own cost for a shot.");
                }

                Assert.That(costs, Has.Count.EqualTo(casterComp.Modes.Count),
                    "Every Bad Blood caster mode is meant to have a different cost.");
            }
            finally
            {
                foreach (var uid in projectiles.Append(hunter).Append(bracer))
                {
                    if (entMan.EntityExists(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HonourableCasterStillDrainsItsStoredCost()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            var caster = entMan.SpawnEntity(HunterCaster, map.GridCoords);
            var projectiles = new List<EntityUid>();

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var stored = entMan.GetComponent<YautjaStoredGearComponent>(caster);
                stored.Bracer = bracer;
                stored.Deployed = true;

                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                var casterComp = entMan.GetComponent<YautjaCasterComponent>(caster);

                Assert.That(casterComp.Modes.All(m => m.PowerCost == null), Is.True,
                    "The honourable caster must not pick up per-mode costs.");

                for (var mode = 0; mode < casterComp.Modes.Count; mode++)
                {
                    casterComp.CurrentMode = mode;
                    bracerComp.Charge = bracerComp.MaxCharge;
                    projectiles.Add(TakeOneShot(entMan, caster, hunter, map.GridCoords));

                    Assert.That(bracerComp.Charge, Is.EqualTo(bracerComp.MaxCharge - casterComp.PowerCost),
                        $"Honourable caster mode {mode} must keep draining the cost stored on the caster.");
                }
            }
            finally
            {
                foreach (var uid in projectiles.Append(hunter).Append(bracer).Append(caster))
                {
                    if (entMan.EntityExists(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    // one prepared shot, the way the gun asks the caster for ammo
    private static EntityUid TakeOneShot(IEntityManager entMan, EntityUid caster, EntityUid user, EntityCoordinates coords)
    {
        var takeAmmo = new TakeAmmoEvent(
            1,
            new List<(EntityUid? Entity, IShootable Shootable)>(),
            coords,
            user);
        entMan.EventBus.RaiseLocalEvent(caster, takeAmmo);

        Assert.That(takeAmmo.Ammo, Has.Count.EqualTo(1), "The caster did not prepare a shot.");
        var projectile = takeAmmo.Ammo[0].Entity;
        Assert.That(projectile, Is.Not.Null);

        // mark it fired so deleting it in cleanup does not refund the bracer mid-test
        entMan.EventBus.RaiseLocalEvent(caster, new AmmoShotEvent
        {
            FiredProjectiles = [projectile!.Value],
        });
        return projectile.Value;
    }
}
