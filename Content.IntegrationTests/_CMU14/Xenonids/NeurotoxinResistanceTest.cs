using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Xenonids.Neurotoxin;
using Content.Shared.Damage;
using Content.Shared.Inventory;
using Content.Shared.Projectiles;
using Content.Shared.Standing;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Xenonids;

// CMU14 Test: CM-SS13 /proc/apply_neuro resistance contracts for xeno neurotoxin.
// Reference: code/datums/ammo/xeno.dm in CM-SS13 - max Endurance shrugs a weak neurotoxin off
// (insta_neuro bypasses that, /datum/ammo/xeno/toxin/queen), NO_NEURO species shrug it off, and
// neither check touches the gas injector path used by the neurotoxin glob.
[TestFixture]
public sealed class NeurotoxinResistanceTest
{
    private static readonly EntProtoId<SkillDefinitionComponent> Endurance = "RMCSkillEndurance";

    // CM-SS13 SKILL_ENDURANCE_MAX.
    private const int MaxEndurance = 5;

    private static void HitWithSpit(IEntityManager entities, string projectileId, EntityCoordinates coords, EntityUid target)
    {
        var spit = entities.SpawnEntity(projectileId, coords);
        var hit = new ProjectileHitEvent(new DamageSpecifier(), target);
        entities.EventBus.RaiseLocalEvent(spit, ref hit);
    }

    // Confirmed scenario 1: Endurance 5, no armour, Sentinel neurotoxin -> the target withstands it.
    // Confirmed scenario 2: the Queen's neurotoxin still stuns that same target.
    [Test]
    public async Task MaxEnduranceWithstandsTheSentinelNeurotoxinButNotTheQueens()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var skills = entities.System<SkillsSystem>();
            var standing = entities.System<StandingStateSystem>();

            var veteran = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            skills.SetSkill(veteran, Endurance, MaxEndurance);
            HitWithSpit(entities, "XenoSlowingSpitProjectile", map.GridCoords, veteran);
            Assert.Multiple(() =>
            {
                Assert.That(standing.IsDown(veteran), Is.False, "Endurance 5 withstands the Sentinel's neurotoxin");
                Assert.That(entities.HasComponent<RMCSuperSlowdownComponent>(veteran), Is.False,
                    "and shrugs its slow off too");
            });

            // One level below the maximum is not protected: the check is the top of the scale only.
            var trained = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            skills.SetSkill(trained, Endurance, MaxEndurance - 1);
            HitWithSpit(entities, "XenoSlowingSpitProjectile", map.GridCoords, trained);
            Assert.Multiple(() =>
            {
                Assert.That(standing.IsDown(trained), Is.True, "Endurance 4 does not withstand the neurotoxin");
                Assert.That(entities.HasComponent<RMCSuperSlowdownComponent>(trained), Is.True,
                    "and it is slowed as well");
            });

            // /datum/ammo/xeno/toxin/queen passes insta_neuro, so Endurance cannot protect against it.
            var queenPrey = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            skills.SetSkill(queenPrey, Endurance, MaxEndurance);
            HitWithSpit(entities, "XenoQueenNeuroSpitProjectile", map.GridCoords, queenPrey);
            Assert.That(standing.IsDown(queenPrey), Is.True, "the Queen's neurotoxin stuns through max Endurance");
        });

        await pair.CleanReturnAsync();
    }

    // The bombardier glob is gas in CM-SS13: it never calls apply_neuro, so Endurance and insta_neuro
    // do not apply to it - only the NO_NEURO species check does.
    [Test]
    public async Task TheNeurotoxinGlobIsNotGatedByEndurance()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var skills = entities.System<SkillsSystem>();
            var standing = entities.System<StandingStateSystem>();

            var gassed = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            skills.SetSkill(gassed, Endurance, MaxEndurance);
            HitWithSpit(entities, "XenoBombardNeurotoxinProjectile", map.GridCoords, gassed);
            Assert.Multiple(() =>
            {
                Assert.That(standing.IsDown(gassed), Is.True, "the glob's knockdown is not an Endurance check");
                Assert.That(entities.HasComponent<NeurotoxinComponent>(gassed), Is.True,
                    "and the glob still injects the neurotoxin effect");
            });
        });

        await pair.CleanReturnAsync();
    }

    // Confirmed scenario 3: Yautja (NO_NEURO) shrug the neurotoxin off, whatever fires it.
    [Test]
    public async Task YautjaShrugsOffNeurotoxinSpitsAndGlobs()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var standing = entities.System<StandingStateSystem>();

            // CMU14: ActionXenoScatteredSpit is the one deliberate exception - its own projectile carries
            // knocksDownNeuroImmune, so its knockdown lands while its slow (0) and the status do not.
            var scatteredVictim = entities.SpawnEntity("CMUMobYautja", map.GridCoords);
            HitWithSpit(entities, "XenoScatteredSpitProjectile", map.GridCoords, scatteredVictim);
            Assert.Multiple(() =>
            {
                Assert.That(standing.IsDown(scatteredVictim), Is.True,
                    "ActionXenoScatteredSpit knocks down a Yautja");
                Assert.That(entities.HasComponent<RMCSuperSlowdownComponent>(scatteredVictim), Is.False,
                    "but the ability's slow does not apply to them");
            });

            // The ordinary Sentinel neurospit is not exempt: neither slow nor knockdown reach a Yautja.
            var sentinelVictim = entities.SpawnEntity("CMUMobYautja", map.GridCoords);
            HitWithSpit(entities, "XenoSlowingSpitProjectile", map.GridCoords, sentinelVictim);
            Assert.Multiple(() =>
            {
                Assert.That(standing.IsDown(sentinelVictim), Is.False,
                    "NO_NEURO species shrug the ordinary Sentinel spit off");
                Assert.That(entities.HasComponent<RMCSuperSlowdownComponent>(sentinelVictim), Is.False,
                    "including its slow");
            });

            // Everything else keeps CM-SS13 NO_NEURO: the Queen's spit and the bombard injector are refused.
            var queenVictim = entities.SpawnEntity("CMUMobYautja", map.GridCoords);
            HitWithSpit(entities, "XenoQueenNeuroSpitProjectile", map.GridCoords, queenVictim);
            Assert.Multiple(() =>
            {
                Assert.That(standing.IsDown(queenVictim), Is.False, "NO_NEURO species shrug the Queen's spit off");
                Assert.That(entities.HasComponent<RMCSuperSlowdownComponent>(queenVictim), Is.False,
                    "including the spit's slow");
            });

            var injectorVictim = entities.SpawnEntity("CMUMobYautja", map.GridCoords);
            HitWithSpit(entities, "XenoBombardNeurotoxinProjectile", map.GridCoords, injectorVictim);
            Assert.That(entities.HasComponent<NeurotoxinComponent>(injectorVictim), Is.False,
                "the injector path still refuses Yautja");
        });

        await pair.CleanReturnAsync();
    }

    // CM-SS13's no_clothes_neuro: a suit stops the neurotoxin knockdown, but the slow is applied
    // before that check, so an armoured target is still slowed. Scaled up here to hard armour.
    [Test]
    public async Task HardArmorPreventsTheNeurotoxinKnockdownButNotTheSlow()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var skills = entities.System<SkillsSystem>();
            var standing = entities.System<StandingStateSystem>();
            var inventory = entities.System<InventorySystem>();

            var armored = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            skills.SetSkill(armored, Endurance, 1);
            var armor = entities.SpawnEntity("CMArmorRiot", map.GridCoords);
            Assert.That(inventory.TryEquip(armored, armor, "outerClothing", silent: true, force: true), Is.True);

            HitWithSpit(entities, "XenoSlowingSpitProjectile", map.GridCoords, armored);
            Assert.Multiple(() =>
            {
                Assert.That(standing.IsDown(armored), Is.False, "armour resists the spit's knockdown");
                Assert.That(entities.HasComponent<RMCSuperSlowdownComponent>(armored), Is.True,
                    "the slow lands regardless of armour");
            });
        });

        await pair.CleanReturnAsync();
    }
}
