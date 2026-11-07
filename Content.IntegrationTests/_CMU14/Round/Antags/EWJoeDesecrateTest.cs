#pragma warning disable RA0002 // Inspect committed bone and fracture state after the public Desecrate call.
using System.Numerics;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared._RMC14.Damage.ObstacleSlamming;
using Content.Shared._RMC14.Fireman;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// ECS-05 (node N11): Desecrate. Damage, the guaranteed fracture on the selected zone with its fallback,
/// the compound fracture when empowered, the knockback and its armed obstacle slam, the boneless bonus,
/// and the release of a grabbed body.
/// </summary>
[TestFixture]
public sealed class EWJoeDesecrateTest
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string Human = "CMMobHuman";

    private static Entity<EWJoeComponent> SpawnJoe(IEntityManager entities, TestMapData map, TargetBodyZone zone)
    {
        var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
        var comp = entities.EnsureComponent<EWJoeComponent>(joe);
        entities.EnsureComponent<EWJoeDesecrateComponent>(joe);
        entities.EnsureComponent<BodyZoneTargetingComponent>(joe);
        entities.System<SharedBodyZoneTargetingSystem>().SelectZone(joe, zone);
        return (joe, comp);
    }

    private static EntityUid Part(IEntityManager entities, EntityUid body, BodyPartType type, BodyPartSymmetry symmetry)
    {
        Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(body, new CMUMedicalBodyPartKey(type, symmetry), out var part),
            $"the body has a {symmetry} {type}");
        return part;
    }

    private static FractureSeverity Severity(IEntityManager entities, EntityUid part)
    {
        return entities.TryGetComponent(part, out FractureComponent? fracture) ? fracture.Severity : FractureSeverity.None;
    }

    [Test]
    public async Task DesecrateDamagesFracturesTheSelectedZoneAndKnocksBack()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        EntityUid joe = default, victim = default;
        var startDistance = 0f;
        await server.WaitAssertion(() =>
        {
            var combat = entities.System<EWJoeCombatSystem>();
            var damageable = entities.System<DamageableSystem>();
            var xforms = entities.System<SharedTransformSystem>();

            // room to fly: the victim one tile east, open floor beyond it
            for (var x = 2; x <= 6; x++)
                EWJoeTestHelpers.TileEast(entities, map, x);
            var spot = EWJoeTestHelpers.TileEast(entities, map, 1);

            var ent = SpawnJoe(entities, map, TargetBodyZone.LeftArm);
            joe = ent.Owner;
            victim = entities.SpawnEntity(Human, spot);
            var desecrate = entities.GetComponent<EWJoeDesecrateComponent>(joe);
            var arm = Part(entities, victim, BodyPartType.Arm, BodyPartSymmetry.Left);

            startDistance = (xforms.GetWorldPosition(victim) - xforms.GetWorldPosition(joe)).Length();
            var before = damageable.GetTotalDamage(victim);

            combat.Desecrate(ent, victim);

            Assert.Multiple(() =>
            {
                Assert.That(damageable.GetTotalDamage(victim), Is.GreaterThanOrEqualTo(before + desecrate.Damage - 5), "the Desecrate damage landed");
                Assert.That(Severity(entities, arm).IsAtLeast(desecrate.Severity), "the selected zone's part is fractured");
                Assert.That(entities.HasComponent<RMCObstacleSlamBonusEffectsComponent>(victim), "the obstacle slam is armed for the flight");
            });
        });

        await pair.RunTicksSync(pair.SecondsToTicks(1));

        await server.WaitAssertion(() =>
        {
            var xforms = entities.System<SharedTransformSystem>();
            var distance = (xforms.GetWorldPosition(victim) - xforms.GetWorldPosition(joe)).Length();
            Assert.That(distance, Is.GreaterThan(startDistance + 1f), "the knockback carried the victim away from the Joe");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FractureFallsBackToTheTorsoWhenTheSelectedPartRefuses()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var combat = entities.System<EWJoeCombatSystem>();
            var ent = SpawnJoe(entities, map, TargetBodyZone.LeftArm);
            var victim = entities.SpawnEntity(Human, map.GridCoords);
            var arm = Part(entities, victim, BodyPartType.Arm, BodyPartSymmetry.Left);
            Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetRootPart(victim, out var root), "the body has a root part");

            // a part with no bone cannot be fractured, so SeedFracture refuses it
            entities.RemoveComponent<BoneComponent>(arm);

            Assert.That(combat.TryDesecrateFracture(ent, victim, FractureSeverity.Simple, out var broken), "a fallback part was fractured");
            Assert.Multiple(() =>
            {
                Assert.That(broken, Is.EqualTo(root.Owner), "the fallback is the torso (root part)");
                Assert.That(Severity(entities, root.Owner).IsAtLeast(FractureSeverity.Simple), "the torso is fractured");
                Assert.That(entities.HasComponent<FractureComponent>(arm), Is.False, "the refused part is untouched");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EmpoweredDesecrateSeedsACompoundFracture()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var combat = entities.System<EWJoeCombatSystem>();
            var ent = SpawnJoe(entities, map, TargetBodyZone.RightLeg);
            var victim = entities.SpawnEntity(Human, map.GridCoords);
            var leg = Part(entities, victim, BodyPartType.Leg, BodyPartSymmetry.Right);

            combat.Desecrate(ent, victim, empowered: true);

            Assert.That(Severity(entities, leg).IsAtLeast(FractureSeverity.Compound), "an empowered Desecrate seeds a compound fracture");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SynthTargetTakesBonusDamageInsteadOfAFracture()
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
            var bodyIndex = entities.System<CMUMedicalBodyIndexSystem>();

            var ent = SpawnJoe(entities, map, TargetBodyZone.Chest);
            var desecrate = entities.GetComponent<EWJoeDesecrateComponent>(ent.Owner);
            var synth = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var reference = entities.SpawnEntity(ColonyJoe, map.GridCoords);

            var synthBefore = damageable.GetTotalDamage(synth);
            combat.Desecrate(ent, synth);
            var synthTaken = damageable.GetTotalDamage(synth) - synthBefore;

            // the same Blunt without the bonus, through the same resistances
            var referenceBefore = damageable.GetTotalDamage(reference);
            damageable.TryChangeDamage(reference, new DamageSpecifier(protos.Index<DamageTypePrototype>("Blunt"), desecrate.Damage), origin: ent.Owner);
            var referenceTaken = damageable.GetTotalDamage(reference) - referenceBefore;

            Assert.Multiple(() =>
            {
                foreach (var (part, _) in bodyIndex.GetBodyParts(synth))
                    Assert.That(entities.HasComponent<FractureComponent>(part), Is.False, $"synth part {entities.ToPrettyString(part)} was not fractured");

                Assert.That(synthTaken, Is.GreaterThan(referenceTaken), "the boneless target took the bonus damage");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GrabbedTargetIsReleasedAndLaunched()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        EntityUid joe = default, victim = default;
        await server.WaitAssertion(() =>
        {
            joe = SpawnJoe(entities, map, TargetBodyZone.Chest).Owner;
            victim = entities.SpawnEntity(Human, map.GridCoords);
        });

        Assert.That(await EWJoeTestHelpers.AggressiveGrab(pair, joe, victim), "aggressive grab established");

        var start = Vector2.Zero;
        await server.WaitAssertion(() =>
        {
            start = entities.System<SharedTransformSystem>().GetWorldPosition(victim);

            var desecrate = new EWJoeDesecrateActionEvent { Performer = joe, Target = victim };
            entities.EventBus.RaiseLocalEvent(joe, desecrate);

            Assert.Multiple(() =>
            {
                Assert.That(desecrate.Handled, "Desecrate handled on a held body");
                Assert.That(entities.System<FiremanCarrySystem>().IsAggressivelyGrabbed(victim), Is.False, "the grab was released");
            });
        });

        await pair.RunTicksSync(pair.SecondsToTicks(1));

        await server.WaitAssertion(() =>
        {
            var moved = (entities.System<SharedTransformSystem>().GetWorldPosition(victim) - start).Length();
            Assert.That(moved, Is.GreaterThan(1f), "the released body was launched");
        });

        await pair.CleanReturnAsync();
    }
}
