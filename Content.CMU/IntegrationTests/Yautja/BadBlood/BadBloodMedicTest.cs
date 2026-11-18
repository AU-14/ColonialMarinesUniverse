#pragma warning disable RA0002 // the test sets wound tiers, bracer charge and regen directly
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared._RMC14.Slow;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Medical.Treatment.FirstAid;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// CMU14 ECS-702 / ECS-711 / ECS-712 (D17): the front-line Medic. Field Mend patches surface bleeding, the worst break,
// pain and slows (never internal bleeding), costs charge, has a per-patient cooldown and skips the medical steps when
// the CMU layers are off. The field kit goes on in half the time for a Medic only. The Medic has no immobilizer.
// Blood Rite lifts the critical (not the dead, not the rite-sick) with a second wind that ends. Death's Door halves a
// critical clanmate's drain near a Medic. A Medic drags clanmates at 0.8, a grunt at 0.45. The tonic heals only out of
// crit and halves limb regrowth, and only a Medic brews it at the Lodge cauldron.
[TestFixture]
public sealed class BadBloodMedicTest : GameTest
{
    private static readonly ProtoId<BadBloodCastePrototype> Medic = "Medic";
    private const string BoneSplints = "CMUBadBloodBoneSplints";
    private const string TonicCrystal = "CMUBadBloodTonicCrystal";
    private const string Tonic = "CMUBadBloodTonic";
    private const string LodgeCauldron = "CMUBadBloodCampCauldron";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task FieldMendPatchesBleedingBreaksPainAndSlowsButNotInternalBleeding()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 3);

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var (medic, patient) = (party.Grunts[0], party.Grunts[1]);
                var mend = MakeMedic(medic);

                var (arm, leg, torso) = Wound(patient);
                var pain = entMan.System<SharedPainShockSystem>();
                Assert.That(pain.GetTierSuppression(patient), Is.Zero, "Test setup: no painkiller yet");

                Server.System<BadBloodMedicSystem>().ApplyFieldMend((medic, mend), patient);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<BodyPartWoundComponent>(arm).ExternalBleeding, Is.EqualTo(ExternalBleedTier.None),
                        "surface bleeding stops");
                    Assert.That(entMan.HasComponent<CMUSplintedComponent>(leg), Is.True, "the break is splinted");
                    Assert.That(pain.GetTierSuppression(patient), Is.GreaterThanOrEqualTo(1), "the pain drops a tier");
                    Assert.That(entMan.HasComponent<RMCSlowdownComponent>(patient), Is.False, "slows are cleared");
                    Assert.That(entMan.HasComponent<InternalBleedingComponent>(torso), Is.True,
                        "internal bleeding is left to the medicomp");
                });
            });
        }
        finally
        {
            await Cleanup(party);
        }
    }

    [Test]
    public async Task FieldMendIsADoAfterThatCostsChargeAndWaitsPerPatient()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 3);
        var (medic, patient, other) = (party.Grunts[0], party.Grunts[1], party.Grunts[2]);
        YautjaBracerComponent bracer = default!;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var mend = MakeMedic(medic);
                bracer = Bracer(medic);
                bracer.NextRegen = Server.Timing.CurTime + TimeSpan.FromMinutes(10);
                var medics = Server.System<BadBloodMedicSystem>();

                bracer.Charge = 100;
                Assert.That(medics.TryStartFieldMend((medic, mend), patient), Is.False, "150 charge is needed");

                bracer.Charge = 1000;
                Assert.That(medics.TryStartFieldMend((medic, mend), patient), Is.True);
            });

            await Pair.RunSeconds(4);

            await Server.WaitAssertion(() =>
            {
                var mend = Server.EntMan.GetComponent<BadBloodFieldMendComponent>(medic);
                var medics = Server.System<BadBloodMedicSystem>();
                Assert.Multiple(() =>
                {
                    Assert.That(bracer.Charge.Float(), Is.InRange(840f, 850f), "a finished mend spends 150 charge (plus a few seconds of the bracer's own drain)");
                    Assert.That(Server.EntMan.HasComponent<BadBloodFieldMendedComponent>(patient), Is.True);
                    Assert.That(medics.TryStartFieldMend((medic, mend), patient), Is.False, "20 s before the same patient again");
                    Assert.That(medics.TryStartFieldMend((medic, mend), other), Is.True, "another patient is fine");
                });
            });
        }
        finally
        {
            await Cleanup(party);
        }
    }

    [Test]
    public async Task FieldMendSkipsTheMedicalStepsWhileTheLayersAreOff()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 3);

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var (medic, patient) = (party.Grunts[0], party.Grunts[1]);
                var mend = MakeMedic(medic);
                var (arm, leg, _) = Wound(patient);

                Server.CfgMan.SetCVar(CMUMedicalCCVars.Enabled, false);
                Assert.DoesNotThrow(() => Server.System<BadBloodMedicSystem>().ApplyFieldMend((medic, mend), patient));

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<BodyPartWoundComponent>(arm).ExternalBleeding, Is.EqualTo(ExternalBleedTier.Moderate),
                        "the wounds layer is off, so the bleeding is untouched");
                    Assert.That(entMan.HasComponent<CMUSplintedComponent>(leg), Is.False, "the bone layer is off, so no splint");
                    Assert.That(entMan.HasComponent<RMCSlowdownComponent>(patient), Is.False, "slows are still cleared");
                });
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.CfgMan.SetCVar(CMUMedicalCCVars.Enabled, true));
            await Cleanup(party);
        }
    }

    [Test]
    public async Task FieldKitGoesOnInHalfTheTimeForAMedicOnly()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 3);

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var (medic, patient, grunt) = (party.Grunts[0], party.Grunts[1], party.Grunts[2]);
                MakeMedic(medic);
                Wound(patient);

                Assert.Multiple(() =>
                {
                    Assert.That(SplintDelay(grunt, patient, map.GridCoords), Is.EqualTo(TimeSpan.FromSeconds(3)),
                        "anyone else splints at the normal time");
                    Assert.That(SplintDelay(medic, patient, map.GridCoords), Is.EqualTo(TimeSpan.FromSeconds(1.5)),
                        "a Medic splints in half the time");
                });
            });
        }
        finally
        {
            await Cleanup(party);
        }
    }

    [Test]
    public async Task MedicCarriesNoImmobilizer()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 3);

        try
        {
            await Server.WaitAssertion(() =>
            {
                var (medic, plain) = (party.Grunts[0], party.Grunts[1]);
                MakeMedic(medic);
                Assert.Multiple(() =>
                {
                    Assert.That(AskMode(plain, BadBloodEradicatorSystem.ImmobilizerMode).Cancelled, Is.False,
                        "a Bad Blood without the caste keeps the immobilizer");
                    Assert.That(AskMode(medic, BadBloodEradicatorSystem.ImmobilizerMode).Cancelled, Is.True,
                        "a Medic carries no immobilizer");
                    Assert.That(AskMode(medic, BadBloodEradicatorSystem.StunMode).Cancelled, Is.False,
                        "a Medic keeps the stun bolt");
                });
            });
        }
        finally
        {
            await Cleanup(party);
        }
    }

    [Test]
    public async Task BloodRiteLiftsTheCriticalOnceAndRefusesTheDead()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 3);
        var (medic, patient, dead) = (party.Grunts[0], party.Grunts[1], party.Grunts[2]);

        try
        {
            await Server.WaitAssertion(() =>
            {
                MakeMedic(medic);
                Asphyxiate(patient, 300);
                Assert.That(Server.System<MobStateSystem>().IsCritical(patient), Is.True, "Test setup: the patient is critical");
                Assert.That(Server.System<BadBloodMedicSystem>().TryStartBloodRite(Rite(medic), patient), Is.True);
            });

            await Pair.RunSeconds(7);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var mobState = Server.System<MobStateSystem>();
                var attack = new AttackAttemptEvent(patient);
                entMan.EventBus.RaiseLocalEvent(patient, attack);
                Assert.Multiple(() =>
                {
                    Assert.That(mobState.IsAlive(patient), Is.True, "the rite lifts the patient out of crit");
                    Assert.That(entMan.HasComponent<BadBloodSecondWindComponent>(patient), Is.True, "with a second wind");
                    Assert.That(entMan.HasComponent<BadBloodRiteSicknessComponent>(patient), Is.True, "and rite sickness");
                    Assert.That(attack.Cancelled, Is.True, "no attacking on a second wind");
                });

                // down again: the sickness refuses a second rite, and the dead are beyond it
                Asphyxiate(patient, 50);
                Assert.That(mobState.IsCritical(patient), Is.True, "Test setup: the patient is critical again");
                mobState.ChangeMobState(dead, Content.Shared.Mobs.MobState.Dead);
                var medics = Server.System<BadBloodMedicSystem>();
                Assert.Multiple(() =>
                {
                    Assert.That(medics.TryStartBloodRite(Rite(medic), patient), Is.False, "no second rite while rite-sick");
                    Assert.That(medics.TryStartBloodRite(Rite(medic), dead), Is.False, "no rite for the dead");
                });
            });

            await Pair.RunSeconds(9);

            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.EntMan.HasComponent<BadBloodSecondWindComponent>(patient), Is.False,
                    "the second wind ends");
            });
        }
        finally
        {
            await Cleanup(party);
        }
    }

    [Test]
    public async Task DeathsDoorHalvesTheDrainOnlyNearALivingMedic()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 3);

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var (medic, patient) = (party.Grunts[0], party.Grunts[1]);
                MakeMedic(medic);
                var medics = Server.System<BadBloodMedicSystem>();
                Asphyxiate(patient, 290);

                medics.RefreshDeathsDoor();
                Assert.That(entMan.HasComponent<BadBloodDeathsDoorComponent>(patient), Is.True, "Test setup: a Medic is near");
                Assert.That(Asphyxiate(patient, 10).Float(), Is.EqualTo(5f).Within(0.01f), "half the drain near a Medic");

                Server.System<SharedTransformSystem>().SetCoordinates(medic, map.GridCoords.Offset(new Vector2(30, 0)));
                medics.RefreshDeathsDoor();
                Assert.That(entMan.HasComponent<BadBloodDeathsDoorComponent>(patient), Is.False);
                Assert.That(Asphyxiate(patient, 10).Float(), Is.EqualTo(10f).Within(0.01f), "the full drain without one");
            });
        }
        finally
        {
            await Cleanup(party);
        }
    }

    [Test]
    public async Task MedicDragsClanmatesFasterThanAGrunt()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 3);

        try
        {
            await Server.WaitAssertion(() =>
            {
                var (medic, patient, grunt) = (party.Grunts[0], party.Grunts[1], party.Grunts[2]);
                MakeMedic(medic);
                Asphyxiate(patient, 300);

                Assert.Multiple(() =>
                {
                    Assert.That(DragRatio(medic, patient), Is.EqualTo(0.8f).Within(0.01f), "a Medic drags a clanmate at 0.8");
                    Assert.That(DragRatio(grunt, patient), Is.EqualTo(0.45f).Within(0.01f), "a grunt drags at 0.45");
                });
            });
        }
        finally
        {
            await Cleanup(party);
        }
    }

    [Test]
    public async Task TonicHealsOnlyOutOfCritAndHalvesLimbRegrowth()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 4);
        var (treated, control) = (party.Grunts[0], party.Grunts[1]);
        var (critTreated, critControl) = (party.Grunts[2], party.Grunts[3]);

        try
        {
            await Server.WaitAssertion(() =>
            {
                var bloodstream = Server.System<BloodstreamSystem>();
                foreach (var body in new[] { treated, control, critTreated, critControl })
                {
                    Hurt(body, "Blunt", 20);
                }

                Asphyxiate(critTreated, 290);
                Asphyxiate(critControl, 290);
                Assert.That(Server.System<MobStateSystem>().IsCritical(critTreated), Is.True, "Test setup: critical");

                Assert.That(bloodstream.TryAddToBloodstream(treated, new([new(Tonic, FixedPoint2.New(15))])), Is.True);
                Assert.That(bloodstream.TryAddToBloodstream(critTreated, new([new(Tonic, FixedPoint2.New(15))])), Is.True);
                Assert.That(Server.ProtoMan.Index<ReagentPrototype>(Tonic).Overdose, Is.EqualTo(FixedPoint2.New(30)),
                    "the tonic overdoses at 30 u");
            });

            await Pair.RunSeconds(6);

            await Server.WaitAssertion(() =>
            {
                var survival = Server.System<BadBloodSurvivalSystem>();
                Assert.Multiple(() =>
                {
                    Assert.That(Blunt(treated), Is.LessThan(Blunt(control) - 0.8f), "the tonic heals a fighter");
                    Assert.That(Blunt(critTreated), Is.EqualTo(Blunt(critControl)).Within(0.01f), "but not the critical");
                    Assert.That(survival.GetLimbRegrowDelayMultiplier(treated), Is.EqualTo(0.5f).Within(0.001f),
                        "limbs regrow in half the time while it lasts");
                    Assert.That(survival.GetLimbRegrowDelayMultiplier(control), Is.EqualTo(1f).Within(0.001f));
                });
            });
        }
        finally
        {
            await Cleanup(party);
        }
    }

    [Test]
    public async Task OnlyAMedicBrewsTonicAtTheLodgeCauldron()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 3);
        var (medic, grunt) = (party.Grunts[0], party.Grunts[1]);
        var trophiesBefore = 0;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                MakeMedic(medic);
                trophiesBefore = entMan.Count<YautjaTrophyComponent>();
                var medicCauldron = entMan.SpawnEntity(LodgeCauldron, map.GridCoords.Offset(new Vector2(0, 2)));
                var gruntCauldron = entMan.SpawnEntity(LodgeCauldron, map.GridCoords.Offset(new Vector2(3, 2)));
                Boil(medic, medicCauldron, map.GridCoords.Offset(new Vector2(0, 4)));
                Boil(grunt, gruntCauldron, map.GridCoords.Offset(new Vector2(0, 6)));
            });

            await Pair.RunSeconds(16);

            await Server.WaitAssertion(() =>
            {
                Assert.That(Count(TonicCrystal), Is.EqualTo(2), "a Medic renders one limb into two tonic crystals, a grunt none");
                Assert.That(Server.EntMan.Count<YautjaTrophyComponent>(), Is.EqualTo(trophiesBefore + 1),
                    "the grunt still gets a bone");
            });
        }
        finally
        {
            await Cleanup(party);
        }
    }

    private BadBloodFieldMendComponent MakeMedic(EntityUid body)
    {
        Assert.That(Server.System<BadBloodCasteSystem>().TryTakeCaste(body, Medic), Is.True, "Test setup: takes the Medic caste");
        var entMan = Server.EntMan;
        Assert.That(entMan.HasComponent<BadBloodBloodRiteComponent>(body), Is.True, "the caste adds Blood Rite");
        var mend = entMan.GetComponent<BadBloodFieldMendComponent>(body);
        Assert.That(mend.MendActionEntity, Is.Not.Null, "the caste grants Field Mend");
        Assert.That(entMan.GetComponent<BadBloodBloodRiteComponent>(body).RiteActionEntity, Is.Not.Null, "and Blood Rite");
        return mend;
    }

    private Entity<BadBloodBloodRiteComponent> Rite(EntityUid medic)
    {
        return (medic, Server.EntMan.GetComponent<BadBloodBloodRiteComponent>(medic));
    }

    // a bleeding left arm, a simply broken left leg, an internal bleed in the torso and a marine's stagger
    private (EntityUid Arm, EntityUid Leg, EntityUid Torso) Wound(EntityUid patient)
    {
        var entMan = Server.EntMan;
        var arm = Part(patient, BodyPartType.Arm, BodyPartSymmetry.Left);
        var leg = Part(patient, BodyPartType.Leg, BodyPartSymmetry.Left);
        var torso = Part(patient, BodyPartType.Torso, BodyPartSymmetry.None);

        entMan.EnsureComponent<BodyPartWoundComponent>(arm).ExternalBleeding = ExternalBleedTier.Moderate;
        var fracture = entMan.EnsureComponent<FractureComponent>(leg);
        Server.System<SharedFractureSystem>().SetSeverity((leg, fracture), FractureSeverity.Simple);
        Server.System<SharedCMUWoundsSystem>().SeedInternalBleed(torso, "test", 1f);
        Assert.That(Server.System<RMCSlowSystem>().TrySlowdown(patient, TimeSpan.FromSeconds(60), ignoreImmunity: true), Is.True,
            "Test setup: the patient is staggered");
        Assert.That(entMan.HasComponent<InternalBleedingComponent>(torso), Is.True, "Test setup: the torso bleeds inside");
        return (arm, leg, torso);
    }

    private EntityUid Part(EntityUid body, BodyPartType type, BodyPartSymmetry symmetry)
    {
        Assert.That(Server.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(body,
            new CMUMedicalBodyPartKey(type, symmetry), out var part), Is.True, $"Test setup: the body has a {symmetry} {type}");
        return part;
    }

    private YautjaBracerComponent Bracer(EntityUid body)
    {
        Assert.That(Server.System<InventorySystem>().TryGetSlotEntity(body, "gloves", out var worn), Is.True,
            "a Bad Blood lands wearing a bracer");
        return Server.EntMan.GetComponent<YautjaBracerComponent>(worn!.Value);
    }

    // the delay of the do-after a fresh set of bone splints starts when the user splints the patient
    private TimeSpan SplintDelay(EntityUid user, EntityUid patient, EntityCoordinates at)
    {
        var entMan = Server.EntMan;
        var splints = entMan.SpawnEntity(BoneSplints, at);
        Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(user, splints), Is.True, "Test setup: holds the splints");

        var interact = new AfterInteractEvent(user, splints, patient, entMan.GetComponent<TransformComponent>(patient).Coordinates, true);
        entMan.EventBus.RaiseLocalEvent(splints, interact);
        Assert.That(interact.Handled, Is.True, "the splints start a do-after");

        var doAfter = entMan.GetComponent<DoAfterComponent>(user).DoAfters.Values.Single(d => !d.Cancelled && !d.Completed);
        return doAfter.Args.Delay;
    }

    private YautjaCasterModeAttemptEvent AskMode(EntityUid user, int mode)
    {
        var attempt = new YautjaCasterModeAttemptEvent(EntityUid.Invalid, mode) { Silent = true };
        Server.EntMan.EventBus.RaiseLocalEvent(user, ref attempt);
        return attempt;
    }

    // the damage the body really took, all of it asphyxiation, past resistances
    private FixedPoint2 Asphyxiate(EntityUid body, int amount)
    {
        return Hurt(body, "Asphyxiation", amount);
    }

    private FixedPoint2 Hurt(EntityUid body, string type, int amount)
    {
        var damageable = Server.System<DamageableSystem>();
        var before = damageable.GetTotalDamage(body);
        var damage = new DamageSpecifier();
        damage.DamageDict[type] = amount;
        damageable.ChangeDamage(body, damage, ignoreResistances: true, interruptsDoAfters: false, ignoreGlobalModifiers: true);
        return damageable.GetTotalDamage(body) - before;
    }

    private float Blunt(EntityUid body)
    {
        var damage = Server.EntMan.GetComponent<DamageableComponent>(body).Damage;
        return damage.DamageDict.TryGetValue("Blunt", out var blunt) ? blunt.Float() : 0f;
    }

    // sprint speed while dragging the body over sprint speed with empty hands
    private float DragRatio(EntityUid puller, EntityUid body)
    {
        var entMan = Server.EntMan;
        var speed = Server.System<MovementSpeedModifierSystem>();
        var move = entMan.GetComponent<MovementSpeedModifierComponent>(puller);
        speed.RefreshMovementSpeedModifiers(puller);
        var free = move.CurrentSprintSpeed;

        var pulling = Server.System<PullingSystem>();
        Assert.That(pulling.TryStartPull(puller, body), Is.True, "Test setup: the pull starts");
        var ratio = move.CurrentSprintSpeed / free;
        Assert.That(pulling.TryStopPull(body, entMan.GetComponent<PullableComponent>(body), puller), Is.True);
        return ratio;
    }

    // a flayed human arm into the cauldron
    private void Boil(EntityUid user, EntityUid cauldron, EntityCoordinates humanAt)
    {
        var entMan = Server.EntMan;
        var human = entMan.SpawnEntity("CMMobHuman", humanAt);
        Assert.That(Server.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(human,
            new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left), out var arm), Is.True);

        var sever = new BodyPartSeverAttemptEvent(human, arm, BodyPartType.Arm, Surgical: true);
        entMan.EventBus.RaiseLocalEvent(arm, ref sever);
        Assert.That(sever.DetachedBody, Is.Not.Null, "Test setup: the arm comes off");
        var limb = sever.DetachedBody!.Value;
        entMan.EnsureComponent<YautjaFlayedComponent>(limb);

        var use = new InteractUsingEvent(user, limb, cauldron, entMan.GetComponent<TransformComponent>(cauldron).Coordinates);
        entMan.EventBus.RaiseLocalEvent(cauldron, use);
        Assert.That(use.Handled, Is.True);
    }

    private int Count(string prototype)
    {
        var count = 0;
        var query = Server.EntMan.AllEntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out _, out var meta))
        {
            if (meta.EntityPrototype?.ID == prototype)
                count++;
        }

        return count;
    }

    private async Task Cleanup(BadBloodTestParty party)
    {
        await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
        await Server.WaitPost(() =>
        {
            foreach (var uid in party.All)
            {
                if (Server.EntMan.EntityExists(uid))
                    Server.EntMan.DeleteEntity(uid);
            }
        });
    }
}
