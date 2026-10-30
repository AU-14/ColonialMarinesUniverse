using System.Linq;
using System.Numerics;
using Content.Server.CMU14.Yautja;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Pulling;
using Content.Shared.CMU14.Yautja;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Body;
using Content.Shared._RMC14.StatusEffect;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.CMU14.Chemistry.Effects.Positive;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja;

// cmss13 PR 11542: a linked thrall is the Thrall species, the hero human baseline plus the thrall changes
[TestFixture]
public sealed class YautjaThrallHeroTest
{
    [Test]
    public async Task LinkedThrallBecomesThrallSpeciesAndUnlinkTakesItBack()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var thresholds = entMan.System<MobThresholdSystem>();
            var damageable = entMan.System<DamageableSystem>();
            var prototypes = server.ResolveDependency<IPrototypeManager>();

            var control = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0, 2)));
            SpawnLinkedThrall(entMan, map.GridCoords, out var master, out _, out var thrall, out _);

            var baseCrit = thresholds.GetThresholdForState(control, MobState.Critical);
            var baseDead = thresholds.GetThresholdForState(control, MobState.Dead);

            Assert.That(entMan.HasComponent<YautjaThrallHeroComponent>(thrall), Is.True, "linking the bracer is when cmss13 sets the Thrall species");
            Assert.Multiple(() =>
            {
                Assert.That(thresholds.GetThresholdForState(thrall, MobState.Critical), Is.EqualTo(baseCrit + 50), "150 health, crit comes 50 later");
                Assert.That(thresholds.GetThresholdForState(thrall, MobState.Dead), Is.EqualTo(baseDead + 50), "150 health, death comes 50 later");
                Assert.That(entMan.HasComponent<CMUNoPullSlowdownComponent>(thrall), Is.True, "TRAIT_DEXTROUS, no drag slowdown");
                Assert.That(entMan.GetComponent<RMCStunResistanceComponent>(thrall).Resistance, Is.EqualTo(2f), "hero stun_reduction");
                Assert.That(entMan.GetComponent<YautjaButcherAbilityComponent>(thrall).RequiresBlade, Is.False, "species butcher verb, no tool");
                Assert.That(entMan.System<SharedRMCActionsSystem>().GetActionsWithEvent<YautjaMarkForHuntActionEvent>(thrall).Count(),
                    Is.EqualTo(1), "mark_for_hunt verb");
            });

            var hit = new DamageSpecifier(prototypes.Index<DamageTypePrototype>("Blunt"), 20);
            damageable.TryChangeDamage(control, hit);
            damageable.TryChangeDamage(thrall, hit);
            Assert.That(Brute(entMan, prototypes, thrall), Is.EqualTo(Brute(entMan, prototypes, control) * 0.5f),
                "hero brute_mod halves brute");

            entMan.RemoveComponent<YautjaThrallComponent>(thrall);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<YautjaThrallHeroComponent>(thrall), Is.False);
                Assert.That(thresholds.GetThresholdForState(thrall, MobState.Critical), Is.EqualTo(baseCrit));
                Assert.That(thresholds.GetThresholdForState(thrall, MobState.Dead), Is.EqualTo(baseDead));
                Assert.That(entMan.HasComponent<CMUNoPullSlowdownComponent>(thrall), Is.False);
                Assert.That(entMan.HasComponent<RMCStunResistanceComponent>(thrall), Is.False);
                Assert.That(entMan.HasComponent<YautjaButcherAbilityComponent>(thrall), Is.False);
                Assert.That(entMan.System<SharedRMCActionsSystem>().GetActionsWithEvent<YautjaMarkForHuntActionEvent>(thrall).Count(),
                    Is.Zero);
            });

            entMan.DeleteEntity(master);
            entMan.DeleteEntity(thrall);
            entMan.DeleteEntity(control);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LinkedThrallMarksAndReleasesPrey()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var marks = entMan.System<YautjaMarkSystem>();
            SpawnLinkedThrall(entMan, map.GridCoords, out var master, out _, out var thrall, out _);
            var prey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var stranger = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0, 2)));

            Assert.That(marks.TryToggleThrallPrey(stranger, prey), Is.False, "only a linked thrall gets the verb");
            Assert.That(marks.TryToggleThrallPrey(thrall, prey), Is.True);
            Assert.That(marks.IsMarkedBy(prey, YautjaMarkKind.Prey, thrall), Is.True);
            Assert.That(marks.TryToggleThrallPrey(thrall, prey), Is.True, "remove_from_hunt");
            Assert.That(marks.IsMarkedBy(prey, YautjaMarkKind.Prey, thrall), Is.False);

            Assert.That(marks.TryToggleThrallPrey(thrall, prey), Is.True);
            entMan.RemoveComponent<YautjaThrallComponent>(thrall);
            Assert.That(marks.IsMarkedBy(prey, YautjaMarkKind.Prey, thrall), Is.False, "losing the species drops the hunt");

            entMan.DeleteEntity(master);
            entMan.DeleteEntity(thrall);
            entMan.DeleteEntity(prey);
            entMan.DeleteEntity(stranger);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaCanButcherAYautjaCorpse()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var badBlood = entMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(1, 0)));
            entMan.System<MobStateSystem>().ChangeMobState(badBlood, MobState.Dead);

            Assert.That(entMan.System<YautjaTrophySystem>().TryStartButcher(hunter, badBlood, YautjaButcherProcedure.Skin), Is.True,
                "cmss13 PR 11542 lets yautja butcher fellow yautja");

            entMan.DeleteEntity(hunter);
            entMan.DeleteEntity(badBlood);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DathweiRestoresAHumansBlood()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid treated = default, control = default;
        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var bloodstream = entMan.System<BloodstreamSystem>();
            treated = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            control = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            foreach (var human in new[] { treated, control })
                Assert.That(bloodstream.TryModifyBloodLevel(human, -150), Is.True);

            Assert.That(bloodstream.TryAddToBloodstream(treated, new([new("dathwei", FixedPoint2.New(15))])), Is.True);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(20));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var readout = entMan.System<SharedRMCBloodstreamSystem>();
            Assert.That(readout.TryGetBloodReadout(treated, out var treatedBlood, out _), Is.True);
            Assert.That(readout.TryGetBloodReadout(control, out var controlBlood, out _), Is.True);
            Assert.That(treatedBlood, Is.GreaterThan(controlBlood + 10), "dathwei did nothing for a human's blood loss");

            entMan.DeleteEntity(treated);
            entMan.DeleteEntity(control);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ThweiAndDathweiMendShatteredBones()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            foreach (var id in new[] { "thwei", "dathwei" })
            {
                var effects = prototypes.Index<ReagentPrototype>(id).Metabolisms!.Metabolisms.Values.SelectMany(m => m.Effects);
                Assert.That(effects.OfType<Bonemending>().Single().MendsShattered, Is.True, $"{id} should knit any break like cmss13");
            }

            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            Assert.That(entMan.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(human,
                new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left), out var arm), Is.True);
            var bones = entMan.System<SharedBoneSystem>();
            Assert.That(bones.SeedFracture(arm, FractureSeverity.Shattered), Is.True);
            var bone = entMan.GetComponent<BoneComponent>(arm);

            Assert.That(bones.ChemicallyMendFractures(human, bone.IntegrityMax, requireSet: false), Is.Zero,
                "ordinary bonemending still leaves shattered bones to surgery");
            Assert.That(bones.ChemicallyMendFractures(human, bone.IntegrityMax, requireSet: false, mendShattered: true), Is.EqualTo(1));
            Assert.That(entMan.HasComponent<FractureComponent>(arm), Is.False);

            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    private static FixedPoint2 Brute(IEntityManager entMan, IPrototypeManager prototypes, EntityUid uid)
    {
        var damage = entMan.System<DamageableSystem>().GetAllDamage((uid, entMan.GetComponent<DamageableComponent>(uid)));
        return damage.TryGetDamageInGroup(prototypes.Index<DamageGroupPrototype>("Brute"), out var brute) ? brute : FixedPoint2.Zero;
    }

    private static void SpawnLinkedThrall(
        IEntityManager entMan,
        EntityCoordinates coordinates,
        out EntityUid master,
        out EntityUid masterBracer,
        out EntityUid thrall,
        out EntityUid thrallBracer)
    {
        var inventory = entMan.System<InventorySystem>();
        master = entMan.SpawnEntity("CMMobHuman", coordinates);
        masterBracer = entMan.SpawnEntity("CMUYautjaBracer", coordinates);
        entMan.EnsureComponent<YautjaComponent>(master);
        Assert.That(inventory.TryEquip(master, masterBracer, "gloves", silent: true, force: true), Is.True);

        thrall = entMan.SpawnEntity("CMMobHuman", coordinates.Offset(new Vector2(1, 0)));
        var bracerEnt = (masterBracer, entMan.GetComponent<YautjaBracerComponent>(masterBracer));
        Assert.That(entMan.System<YautjaMarkSystem>().TryMark(bracerEnt, master, thrall, YautjaMarkKind.Thrall, "claimed"), Is.True);

        thrallBracer = entMan.SpawnEntity("CMUYautjaThrallBracer", coordinates.Offset(new Vector2(1, 0)));
        Assert.That(inventory.TryEquip(thrall, thrallBracer, "gloves", silent: true, force: true), Is.True);
        Assert.That(entMan.System<YautjaThrallSystem>().TryLinkThrallBracer(bracerEnt, master), Is.True);
    }
}
