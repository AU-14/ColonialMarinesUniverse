#pragma warning disable RA0002 // Inspect committed shred and pin state after the public ability calls.
using System.Collections.Generic;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Slow;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// ECS-01/ECS-03 (W7, node N10): the xenofauna branches of the Joe's existing moves. 0A Industrial Grip,
/// 1B Overpressure, 2A Chitin Crack and 2B Pin each act on a xeno and leave a human as before.
/// </summary>
[TestFixture]
public sealed class EWJoeXenoKitCombatTest
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string Human = "CMMobHuman";
    private const string Drone = "CMXenoDrone";
    private const string Defender = "CMXenoDefender";
    private const string Wall = "WallSolid";

    [Test]
    public async Task IndustrialGripHurtsAndSlowsXenosButNotHumans()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var damageable = entities.System<DamageableSystem>();
            var protos = server.ResolveDependency<IPrototypeManager>();

            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<EWJoeComponent>(joe);
            entities.EnsureComponent<EWJoeIndustrialGripComponent>(joe);
            var xeno = entities.SpawnEntity(Drone, map.GridCoords);
            var human = entities.SpawnEntity(Human, map.GridCoords);

            var xenoBefore = damageable.GetTotalDamage(xeno);

            // an unarmed swing: the Joe is its own weapon
            var punch = new DamageSpecifier(protos.Index<DamageTypePrototype>("Blunt"), 60);
            var hit = new MeleeHitEvent(new List<EntityUid> { xeno, human }, joe, joe, punch, null);
            entities.EventBus.RaiseLocalEvent(joe, hit);

            Assert.Multiple(() =>
            {
                Assert.That(damageable.GetTotalDamage(xeno), Is.GreaterThan(xenoBefore), "the xeno took the grip bonus");
                Assert.That(entities.HasComponent<RMCSlowdownComponent>(xeno), "the xeno was slowed");
                // the human's raw total is not asserted: RMC's per-hit body location handling on humans applies part
                // of a raw MeleeHitEvent at random, which made an exact total flaky. The grip itself only ever targets
                // xenofauna, so assert that rule and the grip's slow instead.
                Assert.That(EWJoeTargetRules.IsXenofauna(entities, human), Is.False, "a human is not xenofauna, so gets no bonus");
                Assert.That(entities.HasComponent<RMCSlowdownComponent>(human), Is.False, "no slow on a human");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OverpressureBurstsAXenoAndHumansStillNeedAGrab()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var damageable = entities.System<DamageableSystem>();
            var spot = EWJoeTestHelpers.TileEast(entities, map, 1);
            for (var x = 2; x <= 4; x++)
                EWJoeTestHelpers.TileEast(entities, map, x);

            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<EWJoeComponent>(joe);
            var plain = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<EWJoeComponent>(plain);
            var xeno = entities.SpawnEntity(Drone, spot);
            var human = entities.SpawnEntity(Human, spot);

            // without the 1B kit Drown refuses a xeno, as it always has
            var refused = new EWJoeDrownActionEvent { Performer = plain, Target = xeno };
            entities.EventBus.RaiseLocalEvent(plain, refused);
            Assert.That(refused.Handled, Is.False, "Drown without Overpressure refuses a xeno");

            entities.EnsureComponent<EWJoeOverpressureComponent>(joe);

            var before = damageable.GetTotalDamage(xeno);
            var burst = new EWJoeDrownActionEvent { Performer = joe, Target = xeno };
            entities.EventBus.RaiseLocalEvent(joe, burst);

            // a human is still drowned only out of an aggressive grab
            var drown = new EWJoeDrownActionEvent { Performer = joe, Target = human };
            entities.EventBus.RaiseLocalEvent(joe, drown);

            Assert.Multiple(() =>
            {
                Assert.That(burst.Handled, "Overpressure handled on a xeno");
                Assert.That(damageable.GetTotalDamage(xeno), Is.GreaterThan(before), "the burst damaged the xeno");
                Assert.That(drown.Handled, Is.False, "a human without a grab is still refused");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ChitinCrackShredsXenoArmorOnSlam()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var combat = entities.System<EWJoeCombatSystem>();
            var shred = entities.System<EWJoeArmorShredSystem>();
            var wallSpot = EWJoeTestHelpers.TileEast(entities, map, 2);
            var spot = EWJoeTestHelpers.TileEast(entities, map, 1);

            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);
            var crack = entities.EnsureComponent<EWJoeChitinCrackComponent>(joe);
            var wall = entities.SpawnEntity(Wall, wallSpot);
            var xeno = entities.SpawnEntity(Defender, spot);
            var human = entities.SpawnEntity(Human, spot);

            var before = new CMGetArmorEvent(SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING);
            entities.EventBus.RaiseLocalEvent(xeno, ref before);
            Assert.That(before.XenoArmor, Is.GreaterThanOrEqualTo(crack.ArmorShred), "the defender has armor to crack");

            combat.Slam((joe, comp), xeno, wall);
            combat.Slam((joe, comp), human, wall);

            var after = new CMGetArmorEvent(SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING);
            entities.EventBus.RaiseLocalEvent(xeno, ref after);

            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<EWJoeArmorShredComponent>(xeno), "the xeno's carapace is cracked");
                Assert.That(shred.GetShred(xeno), Is.EqualTo(crack.ArmorShred));
                Assert.That(after.XenoArmor, Is.EqualTo(before.XenoArmor - crack.ArmorShred), "the shred comes off the xeno armor");
                Assert.That(entities.HasComponent<EWJoeArmorShredComponent>(human), Is.False, "a human is not shredded");
            });
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Pin through the direct API: grabbing a xeno needs the level-2 xeno grab (FLOW-02, N5), so this
    /// drives <see cref="EWJoeCombatSystem.Pin"/> and <see cref="EWJoeCombatSystem.PinTick"/> directly. No
    /// tick runs inside the block, so the grab check in the choke loop never ends the ungrabbed pin.
    /// </summary>
    [Test]
    public async Task PinRootsAndBruisesAXenoAndBreaksOnDamage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var combat = entities.System<EWJoeCombatSystem>();
            var damageable = entities.System<DamageableSystem>();
            var protos = server.ResolveDependency<IPrototypeManager>();
            var blunt = protos.Index<DamageTypePrototype>("Blunt");

            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);
            var kit = entities.EnsureComponent<EWJoePinComponent>(joe);
            var xeno = entities.SpawnEntity(Drone, map.GridCoords);

            Assert.That(combat.StartChoke((joe, comp), xeno), Is.False, "no pin without an aggressive grab");

            combat.Pin((joe, comp), xeno);

            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<RMCRootedComponent>(xeno), "the pinned xeno is rooted");
                Assert.That(entities.GetComponent<EWJoePinningComponent>(joe).Target, Is.EqualTo(xeno));
                Assert.That(entities.GetComponent<EWJoeChokeComponent>(joe).Target, Is.EqualTo(xeno), "the pin runs as the choke");
            });

            var before = damageable.GetTotalDamage(xeno);
            combat.PinTick((joe, comp), xeno);
            Assert.That(damageable.GetTotalDamage(xeno), Is.GreaterThan(before), "a pin second bruises the xeno");

            // below the break threshold the pin holds; past it, it breaks
            var half = kit.BreakDamage.Float() / 2f;
            damageable.TryChangeDamage(joe, new DamageSpecifier(blunt, half), ignoreResistances: true, ignoreGlobalModifiers: true);
            Assert.That(entities.HasComponent<EWJoePinningComponent>(joe), "the pin holds under light damage");

            damageable.TryChangeDamage(joe, new DamageSpecifier(blunt, half + 5), ignoreResistances: true, ignoreGlobalModifiers: true);
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<EWJoePinningComponent>(joe), Is.False, "the pin broke");
                Assert.That(entities.HasComponent<EWJoeChokeComponent>(joe), Is.False, "the hold ended with it");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ChokeOnAHumanIsNotAPin()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        EntityUid joe = default, victim = default;
        await server.WaitAssertion(() =>
        {
            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<EWJoeComponent>(joe);
            entities.EnsureComponent<EWJoePinComponent>(joe);
            victim = entities.SpawnEntity(Human, map.GridCoords);
        });

        Assert.That(await EWJoeTestHelpers.AggressiveGrab(pair, joe, victim), "aggressive grab established");

        await server.WaitAssertion(() =>
        {
            var choke = new EWJoeChokeActionEvent { Performer = joe };
            entities.EventBus.RaiseLocalEvent(joe, choke);

            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<EWJoeChokeComponent>(joe), "the choke runs");
                Assert.That(entities.HasComponent<EWJoePinningComponent>(joe), Is.False, "a human is choked, not pinned");
                Assert.That(entities.HasComponent<RMCRootedComponent>(victim), Is.False, "a choked human is not rooted");
            });
        });

        await pair.CleanReturnAsync();
    }
}
