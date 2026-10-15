using System.Numerics;
using Content.Server.Decals;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.FixedPoint;
using Content.Shared.FootPrint;
using Content.Shared.Gravity;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.StepTrigger.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests._CMU14;

[TestFixture]
public sealed class BloodContactFootprintsTest
{
    [TestCase(ExternalBleedTier.Minor, ExternalBleedTier.None, false)]
    [TestCase(ExternalBleedTier.Moderate, ExternalBleedTier.None, false)]
    [TestCase(ExternalBleedTier.Moderate, ExternalBleedTier.Minor, false)]
    [TestCase(ExternalBleedTier.Moderate, ExternalBleedTier.Moderate, true)]
    [TestCase(ExternalBleedTier.Severe, ExternalBleedTier.None, true)]
    [TestCase(ExternalBleedTier.Arterial, ExternalBleedTier.None, true)]
    public async Task WoundBleedingUsesDropletsUntilHeavyAndOnlyPuddlesStainFeet(
        ExternalBleedTier first, ExternalBleedTier second, bool normalPuddle)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        EntityUid patient = default;
        EntityUid walker = default;
        float startingBlood = 0;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = server.System<SharedMapSystem>();
            for (var x = 0; x < 5; x++)
                maps.SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);
            entities.EnsureComponent<GravityComponent>(map.Grid).Enabled = true;
            patient = entities.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            var bloodstream = entities.GetComponent<BloodstreamComponent>(patient);
#pragma warning disable RA0002 // Isolate wound blood loss from regeneration.
            bloodstream.BloodRefreshAmount = FixedPoint2.Zero;
#pragma warning restore RA0002
            startingBlood = server.System<BloodstreamSystem>().GetBloodLevel((patient, bloodstream));
            var arms = server.System<CMUMedicalBodyIndexSystem>().GetBodyParts(patient)
                .Where(part => part.Comp.PartType == BodyPartType.Arm).ToArray();
            SetBleeding(entities, arms[0], first);
            SetBleeding(entities, arms[1], second);
        });

        // Exercise real wound ticks through the bloodstream's accumulation threshold.
        await pair.RunSeconds(15);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            Assert.That(server.System<BloodstreamSystem>().GetBloodLevel(
                (patient, entities.GetComponent<BloodstreamComponent>(patient))), Is.LessThan(startingBlood),
                "Droplets must still drain blood from the patient.");
            Assert.That(server.System<PuddleSystem>().TryGetPuddle(map.Tile, out _), Is.EqualTo(normalPuddle));
            var decals = server.System<DecalSystem>().GetAllDecals(map.Grid).ToArray();
            Assert.That(decals.Any(entry => entry.Decal.Id.StartsWith("CMUBloodDroplet")), Is.EqualTo(!normalPuddle));
            if (!normalPuddle)
                Assert.That(decals.All(entry => entry.Decal.Cleanable), Is.True);

            var transforms = server.System<SharedTransformSystem>();
            transforms.SetLocalPosition(patient, new Vector2(4.5f, 0.5f));
            walker = entities.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 3.5f, 0.5f));
            server.System<SharedPhysicsSystem>().SetBodyStatus(walker,
                entities.GetComponent<PhysicsComponent>(walker), BodyStatus.OnGround);
        });
        await pair.RunTicksSync(2);
        await server.WaitPost(() => server.System<SharedTransformSystem>()
            .SetLocalPosition(walker, new Vector2(0.5f, 0.5f)));
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            var prints = server.EntMan.GetComponent<FootPrintsComponent>(walker);
            Assert.That(prints.PrintsColor.A > 0, Is.EqualTo(normalPuddle));
            server.System<SharedTransformSystem>().SetLocalPosition(walker, new Vector2(1.5f, 0.5f));
            Assert.That(server.System<DecalSystem>().GetAllDecals(map.Grid).Any(entry =>
                entry.Decal.Id == prints.LeftBareDecal || entry.Decal.Id == prints.RightBareDecal),
                Is.EqualTo(normalPuddle));
            if (!normalPuddle)
            {
                Assert.That(server.System<PuddleSystem>().CleanDecalsAt(map.Tile), Is.True);
                Assert.That(server.System<DecalSystem>().GetAllDecals(map.Grid), Is.Empty);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TreatingSecondModerateBleedRestoresDropletsWithoutChangingExistingPuddles()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var patient = entities.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            var arms = server.System<CMUMedicalBodyIndexSystem>().GetBodyParts(patient)
                .Where(part => part.Comp.PartType == BodyPartType.Arm).ToArray();
            SetBleeding(entities, arms[0], ExternalBleedTier.Moderate);
            SetBleeding(entities, arms[1], ExternalBleedTier.Moderate);
            var blood = entities.GetComponent<BloodstreamComponent>(patient);
            var bloodstream = server.System<BloodstreamSystem>();
            Assert.That(bloodstream.TryBleedOut((patient, blood), 2, cmuWoundBleed: true), Is.True);
            var puddles = server.System<PuddleSystem>();
            Assert.That(puddles.TryGetPuddle(map.Tile, out var puddle), Is.True);
            var originalVolume = puddles.CurrentVolume(puddle);

            SetBleeding(entities, arms[1], ExternalBleedTier.None);
            Assert.That(bloodstream.TryBleedOut((patient, blood), 2, cmuWoundBleed: true), Is.True);
            Assert.That(puddles.CurrentVolume(puddle), Is.EqualTo(originalVolume),
                "Light drips must not feed an existing footprint-producing puddle.");
            Assert.That(server.System<DecalSystem>().GetAllDecals(map.Grid).Any(entry =>
                entry.Decal.Id.StartsWith("CMUBloodDroplet")), Is.True);

            // Other blood spills keep their normal behavior even on a lightly wounded body.
            Assert.That(bloodstream.TryBleedOut((patient, blood), 2), Is.True);
            Assert.That(puddles.CurrentVolume(puddle), Is.GreaterThan(originalVolume));
        });
        await pair.CleanReturnAsync();
    }

    private static void SetBleeding(IEntityManager entities, EntityUid part, ExternalBleedTier tier)
    {
        entities.EnsureComponent<BodyPartWoundComponent>(part);
        entities.System<CMUWoundLedgerSystem>().TryUpdateExternalBleeding(part, tier);
    }

    [TestCase("Blood", false)]
    [TestCase("Blood", true)]
    [TestCase("Water", false)]
    public async Task FloorContactStainsWithoutRequiringSlipperyPuddle(string reagent, bool airborne)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        EntityUid human = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = server.System<SharedMapSystem>();
            var floor = map.Tile.Tile;
            for (var x = 0; x < 6; x++)
                maps.SetTile(map.Grid, new Vector2i(x, 0), floor);
            entities.EnsureComponent<GravityComponent>(map.Grid).Enabled = true;
            human = entities.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            server.System<SharedPhysicsSystem>().SetBodyStatus(human,
                entities.GetComponent<PhysicsComponent>(human), airborne ? BodyStatus.InAir : BodyStatus.OnGround);
            Assert.That(server.System<PuddleSystem>().TrySpillAt(new EntityCoordinates(map.Grid, 2.5f, 0.5f),
                new Solution(reagent, reagent == "Water" ? 2 : 20), out var puddle), Is.True);
            Assert.That(entities.GetComponent<StepTriggerComponent>(puddle).Active, Is.False,
                "This regression requires a puddle whose slip trigger is disabled.");
        });
        await pair.RunTicksSync(2);
        await server.WaitPost(() => server.System<SharedTransformSystem>()
            .SetLocalPosition(human, new Vector2(2.5f, 0.5f)));
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var prints = entities.GetComponent<FootPrintsComponent>(human);
            if (airborne || reagent == "Water")
            {
                Assert.That(prints.PrintsColor.A, Is.Zero, "Clean water and airborne contact must not stain feet.");
                return;
            }

            Assert.That(prints.PrintsColor.A, Is.GreaterThan(0), "Blood contact must stain feet even without slipping.");
            var transforms = server.System<SharedTransformSystem>();
            transforms.SetLocalPosition(human, new Vector2(3.5f, 0.5f));
            var decals = server.System<DecalSystem>();
            Assert.That(decals.GetAllDecals(map.Grid).Any(entry =>
                entry.Decal.Id == prints.LeftBareDecal || entry.Decal.Id == prints.RightBareDecal), Is.True);

            server.System<MobStateSystem>().ChangeMobState(human, MobState.Critical);
            transforms.SetLocalPosition(human, new Vector2(4.5f, 0.5f));
            Assert.That(decals.GetAllDecals(map.Grid).Any(entry => prints.DraggingDecals.Contains(entry.Decal.Id)), Is.True,
                "Dragging a stained body must leave a visible blood trail.");
        });
        await pair.CleanReturnAsync();
    }
}
