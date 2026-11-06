using System.Linq;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.Decals;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Melee;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// The Serrated Machete tears blood out with every hit on an organic, at once (it is not left to the wound's
/// slow tick), and throws a spray of it on the far side of the victim. An ordinary machete swung the same way
/// leaves no splatter on the hit, so the spray is the serrated blade's own.
/// </summary>
[TestFixture]
public sealed class EWJoeMacheteBloodTest
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string Human = "CMMobHuman";
    private const string PlainMachete = "AU14ToolMachete";

    [Test]
    public async Task SerratedHitsSprayAndSpillBloodAtOnce()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid joe = default, machete = default, joeVictim = default, attacker = default, plain = default, humanVictim = default;
        await server.WaitAssertion(() =>
        {
            var hands = entities.System<SharedHandsSystem>();

            // tile centres, so the swing (and the spray) runs straight east along the row
            joe = entities.SpawnEntity(ColonyJoe, EWJoeTestHelpers.TileEast(entities, map, 0));
            entities.EnsureComponent<EWJoeComponent>(joe);
            foreach (var held in hands.EnumerateHeld(joe).ToArray())
                hands.TryDrop(joe, held);
            entities.EventBus.RaiseLocalEvent(joe, new EWJoeMacheteActionEvent { Performer = joe });
            machete = entities.System<EWJoeArsenalSystem>().FindMachete(joe)!.Value;
            joeVictim = entities.SpawnEntity(Human, EWJoeTestHelpers.TileEast(entities, map, 1));
            EWJoeTestHelpers.Face(entities, joe, joeVictim);
            // floor for the spray to land on, behind the victim
            EWJoeTestHelpers.TileEast(entities, map, 2);
            EWJoeTestHelpers.TileEast(entities, map, 3);

            // the control stands well apart, so neither spray reaches the other
            attacker = entities.SpawnEntity(Human, EWJoeTestHelpers.TileEast(entities, map, 8));
            plain = entities.SpawnEntity(PlainMachete, EWJoeTestHelpers.TileEast(entities, map, 8));
            foreach (var held in hands.EnumerateHeld(attacker).ToArray())
                hands.TryDrop(attacker, held);
            Assert.That(hands.TryPickupAnyHand(attacker, plain));
            humanVictim = entities.SpawnEntity(Human, EWJoeTestHelpers.TileEast(entities, map, 9));
            EWJoeTestHelpers.Face(entities, attacker, humanVictim);
        });

        // past the fresh weapons' first-swing cooldown
        await pair.RunTicksSync((int) Math.Ceiling(2 / timing.TickPeriod.TotalSeconds));

        await server.WaitAssertion(() =>
        {
            var melee = entities.System<SharedMeleeWeaponSystem>();
            Assert.That(melee.AttemptLightAttack(joe, machete, entities.GetComponent<MeleeWeaponComponent>(machete), joeVictim,
                requireCombatMode: false), "the Joe's swing lands");
            Assert.That(melee.AttemptLightAttack(attacker, plain, entities.GetComponent<MeleeWeaponComponent>(plain), humanVictim,
                requireCombatMode: false), "the control swing lands");
        });

        // a few ticks: far too soon for the wound's own bleed to have spilled anything
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var blood = entities.System<BloodstreamSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(blood.GetBloodLevel(joeVictim), Is.LessThan(0.96f), "a serrated hit takes blood at once (25 of 560)");
                Assert.That(BloodDecals(entities, joeVictim, beyond: true), Is.GreaterThanOrEqualTo(2), "and sprays both splatters on the far side");
                Assert.That(blood.GetBloodLevel(humanVictim), Is.GreaterThan(0.99f), "an ordinary machete takes it only through the wound");
                Assert.That(BloodDecals(entities, humanVictim, beyond: false), Is.Zero, "and leaves no splatter on the hit");
            });
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>Blood decals around the victim; with <paramref name="beyond"/>, only those east of it (away from the attacker).</summary>
    private static int BloodDecals(IEntityManager entities, EntityUid victim, bool beyond)
    {
        var xform = entities.GetComponent<TransformComponent>(victim);
        return entities.System<SharedDecalSystem>()
            .GetDecalsInRange(xform.GridUid!.Value, xform.LocalPosition, 3f, d => d.Id.StartsWith("CMUBlood", StringComparison.Ordinal))
            .Count(d => !beyond || d.Decal.Coordinates.X + 0.5f > xform.LocalPosition.X + 0.25f);
    }
}
