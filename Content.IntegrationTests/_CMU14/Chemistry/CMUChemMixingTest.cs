using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Chemistry.Components;
using Content.Server.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.Chemistry;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.CMU14.Chemistry;

// ARCH-04: grenade-making reactions wait for a lid-closed Mix. FLOW-11: payloads only take vials and beakers.
// FLOW-03/FLOW-12: spray vapor always touches barricades, and each puff touches an entity once.
[TestFixture]
public sealed class CMUChemMixingTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    private const string RepairReagent = "CMUChemMixingTestRepairer";

    [TestPrototypes]
    private const string Prototypes = $"""
        - type: reagent
          id: {RepairReagent}
          name: reagent-name-nothing
          desc: reagent-desc-nothing
          physicalDesc: reagent-physical-desc-nothing
          metabolisms:
            Bloodstream:
              metabolismRate: 0.1
              effects:
              - !type:Repairing
                potency: 2
        """;

    // CMUFoamingAgent: RMCLithium + RMCHydrogen -> FoamingAgent, gated behind CMUMix
    [Test]
    public async Task GatedReactionWaitsForTheLidClosedMix()
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        EntityUid beaker = default;

        await Server.WaitAssertion(() =>
        {
            user = SSpawnAtPosition("CMMobHuman", map.GridCoords);
            beaker = SSpawnAtPosition("CMBeaker", map.GridCoords);
            var solution = BeakerSolution(beaker);
            Server.System<SharedSolutionContainerSystem>().TryAddReagent(solution, "RMCLithium", 5, out _);
            Server.System<SharedSolutionContainerSystem>().TryAddReagent(solution, "RMCHydrogen", 5, out _);
        });

        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var solution = BeakerSolution(beaker).Comp.Solution;
            Assert.Multiple(() =>
            {
                Assert.That(solution.GetTotalPrototypeQuantity("FoamingAgent"), Is.EqualTo(FixedPoint2.Zero),
                    "a CMUMix reaction fired just from adding reagents to an open beaker");
                Assert.That(solution.GetTotalPrototypeQuantity("RMCLithium"), Is.EqualTo(FixedPoint2.New(5)));
            });

            // lid off: no Mix verb, and the shake itself is refused
            Assert.That(MixVerb(beaker, user), Is.Null, "Mix shows while the lid is off");
            var shakeable = Server.System<ShakeableSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(shakeable.TryStartShake(beaker, user), Is.False, "mix doafter started with the lid off");
                Assert.That(shakeable.TryShake(beaker, user), Is.False, "mixed with the lid off");
                Assert.That(solution.GetTotalPrototypeQuantity("FoamingAgent"), Is.EqualTo(FixedPoint2.Zero));
            });

            Assert.That(Server.System<OpenableSystem>().TryClose(beaker, user: user), Is.True);
            var verb = MixVerb(beaker, user);
            Assert.That(verb, Is.Not.Null, "no Mix verb on a closed beaker");
            // runs ShakeableSystem.TryStartShake, the 3 second doafter that ends in ShakeEvent -> ReactionMixer
            verb!.Act!.Invoke();
        });

        await RunSeconds(4);

        await Server.WaitAssertion(() =>
        {
            var solution = BeakerSolution(beaker).Comp.Solution;
            Assert.Multiple(() =>
            {
                Assert.That(solution.GetTotalPrototypeQuantity("FoamingAgent"), Is.EqualTo(FixedPoint2.New(10)),
                    "the lid-closed Mix did not run the CMUMix reaction");
                Assert.That(solution.GetTotalPrototypeQuantity("RMCLithium"), Is.EqualTo(FixedPoint2.Zero));
            });
        });
    }

    [TestCase("CMBeaker", true)]
    [TestCase("CMBeakerLarge", true)]
    [TestCase("RMCVial", true)]
    [TestCase("RMCBeakerHighCapacity", true)]
    [TestCase("RMCReagentJug", false)]
    [TestCase("CMMiniTank", false)]
    public async Task PayloadOnlyTakesVialsAndBeakers(string container, bool fits)
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var payload = SSpawnAtPosition("ChemicalPayload", map.GridCoords);
            var item = SSpawnAtPosition(container, map.GridCoords);
            var slots = Server.System<ItemSlotsSystem>();
            Assert.That(slots.TryGetSlot(payload, "BeakerSlotA", out var slot), Is.True);
            Assert.That(slots.CanInsert(payload, slot!, item, null), Is.EqualTo(fits));
        });
    }

    [Test]
    public async Task VaporIsNeverPassedOverByBarricades()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var transform = Server.System<SharedTransformSystem>();
            var barricade = SSpawnAtPosition("CMBarricadeMetal", map.GridCoords);
            var user = SSpawnAtPosition(null, map.GridCoords);
            var vapor = SSpawnAtPosition("Vapor", map.GridCoords);
            var vaporComp = SComp<VaporComponent>(vapor);
            var target = transform.GetMapCoordinates(vapor).Offset(new Vector2(4, 0));
            Server.System<VaporSystem>().Start((vapor, vaporComp), SComp<TransformComponent>(vapor), Vector2.UnitX, 2, target, 10, user);

            Assert.That(SEntMan.HasComponent<CMUThrownIgnoresBarricadeBlockComponent>(vapor), Is.True);
            for (var i = 0; i < 50; i++)
            {
                var prevent = MakePreventEvent(vapor, barricade);
                SEntMan.EventBus.RaiseLocalEvent(vapor, ref prevent);
                Assert.That(prevent.Cancelled, Is.False, "marked vapor passed over a barricade");
            }

            // without the marker the thrower right next to the barricade always passes over it
            SEntMan.RemoveComponent<CMUThrownIgnoresBarricadeBlockComponent>(vapor);
            var unmarked = MakePreventEvent(vapor, barricade);
            SEntMan.EventBus.RaiseLocalEvent(vapor, ref unmarked);
            Assert.That(unmarked.Cancelled, Is.True, "the unmarked control should still pass over");
        });
    }

    [Test]
    public async Task VaporRepairsABarricadeOncePerPuff()
    {
        var map = await Pair.CreateTestMap();
        EntityUid barricade = default;
        EntityUid vapor = default;
        FixedPoint2 before = default;

        await Server.WaitAssertion(() =>
        {
            _ = Server.System<CMUChemMixingCollideProbeSystem>();
            var damage = Server.System<DamageableSystem>();
            barricade = SSpawnAtPosition("CMBarricadeMetal", map.GridCoords);
            SEntMan.EnsureComponent<CMUChemMixingCollideProbeComponent>(barricade);
            damage.TryChangeDamage(barricade, new DamageSpecifier { DamageDict = { ["Blunt"] = 30, ["Heat"] = 30 } }, true);
            before = damage.GetTotalDamage(barricade);

            // BigVapor's fixture covers the barricade's south-edge fixture from the tile centre
            vapor = SSpawnAtPosition("BigVapor", map.GridCoords);
            SComp<SolutionComponent>(vapor).Solution.AddReagent(RepairReagent, 1);
        });

        await PoolManager.WaitUntil(Server,
            () => SComp<CMUChemMixingCollideProbeComponent>(barricade).Collisions > 0,
            maxTicks: 10);
        await Server.WaitRunTicks(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<DamageableSystem>().GetTotalDamage(barricade), Is.EqualTo(before - FixedPoint2.New(20)),
                "one unit of Repairing (potency 2) should repair ten brute and ten burn on touch");

            // what a throw landing or stopping does: drop and rebuild the vapor's contacts
            Server.System<SharedBroadphaseSystem>().RegenerateContacts(vapor);
        });

        await PoolManager.WaitUntil(Server,
            () => SComp<CMUChemMixingCollideProbeComponent>(barricade).Collisions > 1,
            maxTicks: 10);
        await Server.WaitRunTicks(2);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SComp<VaporComponent>(vapor).ReactedEntities, Does.Contain(barricade));
                Assert.That(Server.System<DamageableSystem>().GetTotalDamage(barricade), Is.EqualTo(before - FixedPoint2.New(20)),
                    "the same puff repaired the barricade again after re-contact");
            });
        });
    }

    private Entity<SolutionComponent> BeakerSolution(EntityUid beaker)
    {
        var name = SComp<FitsInDispenserComponent>(beaker).Solution;
        Assert.That(Server.System<SharedSolutionContainerSystem>().TryGetSolution(beaker, name, out var soln, out _), Is.True);
        return soln!.Value;
    }

    private Verb? MixVerb(EntityUid beaker, EntityUid user)
    {
        var text = Server.ResolveDependency<ILocalizationManager>().GetString("cmu-chem-mix-verb");
        return Server.System<SharedVerbSystem>()
            .GetLocalVerbs(beaker, user, typeof(Verb), force: true)
            .FirstOrDefault(v => v.Text == text);
    }

    private PreventCollideEvent MakePreventEvent(EntityUid vapor, EntityUid target)
    {
        var vaporBody = SComp<PhysicsComponent>(vapor);
        var targetBody = SComp<PhysicsComponent>(target);
        var vaporFixture = SComp<FixturesComponent>(vapor).Fixtures.Values.First();
        var targetFixture = SComp<FixturesComponent>(target).Fixtures.Values.First();
        return new PreventCollideEvent(vapor, target, vaporBody, targetBody, vaporFixture, targetFixture);
    }
}

[RegisterComponent]
public sealed partial class CMUChemMixingCollideProbeComponent : Component
{
    public int Collisions;
}

public sealed class CMUChemMixingCollideProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUChemMixingCollideProbeComponent, StartCollideEvent>(OnStartCollide);
    }

    private void OnStartCollide(Entity<CMUChemMixingCollideProbeComponent> ent, ref StartCollideEvent args)
    {
        if (HasComp<VaporComponent>(args.OtherEntity))
            ent.Comp.Collisions++;
    }
}
