#pragma warning disable RA0002 // tests read component data directly
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Atmos.EntitySystems;
using Content.Server.CMU14.Medical.Treatment.Surgery;
using Content.Shared.Atmos;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Treatment.Surgery;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Roles;
using Content.Shared.Storage.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// STATE-202: every Bad Blood lands with a filled medicomp, and its scavenged tools work 1.25x slower than a hunter's
[TestFixture]
public sealed class BadBloodMedicompTest : GameTest
{
    private const string HunterGear = "CMUYautjaHunterGear";
    private const string HunterMedicomp = "CMUYautjaMedicompFull";
    private const string BadBloodMedicomp = "CMUYautjaBadBloodMedicompFull";
    private const string Surgery = "CMUSurgeryMcompWounds";
    private const float SlowFactor = 1.25f;

    private static readonly (string Hunter, string BadBlood)[] Tools =
    [
        ("CMUYautjaStabilizerGel", "CMUYautjaBadBloodStabilizerGel"),
        ("CMUYautjaHealingGun", "CMUYautjaBadBloodHealingGun"),
        ("CMUYautjaWoundClamp", "CMUYautjaBadBloodWoundClamp"),
    ];

    [Test]
    public async Task EveryBadBloodLoadoutCarriesAFilledMedicomp()
    {
        await Server.WaitAssertion(() =>
        {
            var protos = Server.ProtoMan;
            var factory = SEntMan.ComponentFactory;

            var loadouts = protos.EnumeratePrototypes<StartingGearPrototype>()
                .Where(g => g.ID.StartsWith("CMUYautjaBadBloodLeaderGear") ||
                            g.ID.StartsWith("CMUYautjaBadBloodGruntGear") ||
                            g.ID == "CMUYautjaBadBloodGearBase")
                .ToList();
            Assert.That(loadouts, Has.Count.GreaterThan(2));

            foreach (var loadout in loadouts)
            {
                Assert.That(loadout.Equipment.TryGetValue("pocket2", out var kit), Is.True, $"{loadout.ID} has no pocket2");
                Assert.That(kit.Id, Is.EqualTo(BadBloodMedicomp), $"{loadout.ID}: medicomp");
            }

            Assert.That(protos.Index<EntityPrototype>(BadBloodMedicomp).TryComp<StorageFillComponent>(out var fill, factory), Is.True,
                "the Bad Blood medicomp is empty");

            int Holding<T>() where T : IComponent, new()
            {
                return fill!.Contents
                    .Where(entry => entry.PrototypeId is { } id && protos.Index<EntityPrototype>(id.Id).TryComp<T>(out _, factory))
                    .Sum(entry => entry.Amount);
            }

            Assert.Multiple(() =>
            {
                Assert.That(Holding<YautjaHealingGunComponent>(), Is.EqualTo(1), "healing gun");
                Assert.That(Holding<CMUYautjaMedicompClampToolComponent>(), Is.EqualTo(1), "wound clamp");
                Assert.That(Holding<CMUYautjaMedicompStabilizerToolComponent>(), Is.EqualTo(1), "stabilizer gel");
                Assert.That(Holding<YautjaHealingCapsuleComponent>(), Is.GreaterThanOrEqualTo(3), "healing capsules");
            });

            // every surgical tool in the Bad Blood kit is a slowed copy, none is the hunter's own
            foreach (var entry in fill!.Contents)
            {
                var proto = protos.Index<EntityPrototype>(entry.PrototypeId!.Value.Id);
                var isTool = proto.TryComp<CMUYautjaMedicompStabilizerToolComponent>(out _, factory) ||
                             proto.TryComp<CMUYautjaMedicompHealingGunToolComponent>(out _, factory) ||
                             proto.TryComp<CMUYautjaMedicompClampToolComponent>(out _, factory);
                if (!isTool)
                    continue;

                Assert.That(proto.TryComp<CMUImprovisedSurgeryToolComponent>(out var slowed, factory), Is.True, $"{proto.ID} is not slowed");
                Assert.That(slowed!.DelayMultiplier, Is.EqualTo(SlowFactor).Within(0.0001f), proto.ID);
                Assert.That(slowed.GetFailurePenalty(null), Is.Zero, $"{proto.ID} must only be slower, not clumsier");
            }
        });
    }

    [Test]
    public async Task HonourableMedicompIsUnchanged()
    {
        await Server.WaitAssertion(() =>
        {
            var protos = Server.ProtoMan;
            var factory = SEntMan.ComponentFactory;

            Assert.That(protos.Index<StartingGearPrototype>(HunterGear).Equipment["pocket2"].Id, Is.EqualTo(HunterMedicomp));
            Assert.That(protos.Index<EntityPrototype>(HunterMedicomp).TryComp<StorageFillComponent>(out var fill, factory), Is.True);

            var contents = fill!.Contents.Select(entry => entry.PrototypeId!.Value.Id).ToList();
            foreach (var (hunterTool, badBloodTool) in Tools)
            {
                Assert.That(contents, Does.Contain(hunterTool));
                Assert.That(contents, Does.Not.Contain(badBloodTool));
                Assert.That(protos.Index<EntityPrototype>(hunterTool).TryComp<CMUImprovisedSurgeryToolComponent>(out _, factory), Is.False,
                    $"{hunterTool} must keep its speed");
            }
        });
    }

    [Test]
    public async Task BadBloodStabilizeStepTakesLongerThanAHunters()
    {
        EntityUid map = default;
        EntityUid hunter = default;
        EntityUid badBlood = default;
        var spawned = new List<EntityUid>();
        var hunterDelay = 0f;
        var badBloodDelay = 0f;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var maps = SEntMan.System<SharedMapSystem>();
                map = maps.CreateMap(out var mapId, runMapInit: true);
                maps.SetPaused(map, false);
                var moles = new float[Atmospherics.AdjustedNumberOfGases];
                moles[(int) Gas.Oxygen] = 21.824779f;
                moles[(int) Gas.Nitrogen] = 82.10312f;
                SEntMan.System<AtmosphereSystem>().SetMapAtmosphere(map, false, new GasMixture(moles, Atmospherics.T20C));

                hunter = SEntMan.SpawnEntity("CMUMobYautja", new MapCoordinates(Vector2.Zero, mapId));
                badBlood = SEntMan.SpawnEntity("CMUMobYautja", new MapCoordinates(new Vector2(5, 0), mapId));
                SEntMan.EnsureComponent<YautjaBadBloodComponent>(badBlood);
                spawned.Add(hunter);
                spawned.Add(badBlood);
            });

            await Pair.RunTicksSync(Pair.SecondsToTicks(1));

            await Server.WaitAssertion(() =>
            {
                hunterDelay = StartStabilize(hunter, Tools[0].Hunter, spawned);
                badBloodDelay = StartStabilize(badBlood, Tools[0].BadBlood, spawned);
            });

            Assert.Multiple(() =>
            {
                // stabilize is a 5 s step, and a Yautja's surgery skill (3) works it at 0.55x: 2.75 s for a hunter
                Assert.That(hunterDelay, Is.EqualTo(2.75f).Within(0.01f), "the hunter's step must not change");
                Assert.That(badBloodDelay, Is.GreaterThan(hunterDelay));
                Assert.That(badBloodDelay, Is.EqualTo(hunterDelay * SlowFactor).Within(0.01f));
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                foreach (var uid in spawned)
                {
                    if (SEntMan.EntityExists(uid))
                        SEntMan.DeleteEntity(uid);
                }

                if (SEntMan.EntityExists(map))
                    SEntMan.DeleteEntity(map);
            });
        }
    }

    // wounds the patient, arms the first medicomp step on themself and returns the do-after's length in seconds
    private float StartStabilize(EntityUid patient, string toolPrototype, List<EntityUid> spawned)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict.Add("Blunt", 10);
        SEntMan.System<DamageableSystem>().TryChangeDamage(patient, damage, true);

        Assert.That(SEntMan.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(patient,
            new CMUMedicalBodyPartKey(BodyPartType.Torso, BodyPartSymmetry.None), out var torso), Is.True);

        var tool = SEntMan.SpawnEntity(toolPrototype, SEntMan.GetComponent<TransformComponent>(patient).Coordinates);
        spawned.Add(tool);
        Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(patient, tool), Is.True);
        Assert.That(SEntMan.System<CMUSurgeryFlowSystem>().TryArmExactStep(patient, patient, torso,
            Surgery, 0, BodyPartType.Torso, BodyPartSymmetry.None), Is.Not.Null);
        Assert.That(SEntMan.System<CMUSurgeryDispatchSystem>().TryDispatch(patient, patient, tool), Is.True);

        return (float) SEntMan.GetComponent<DoAfterComponent>(patient).DoAfters.Values
            .Single(d => !d.Cancelled && !d.Completed).Args.Delay.TotalSeconds;
    }
}
