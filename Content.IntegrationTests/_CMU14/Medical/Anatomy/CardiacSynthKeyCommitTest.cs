using System.Linq;
using Content.Server.Mind;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared._RMC14.Synth;
using Content.Shared.CMU14.SynthRepairer;
using Content.Shared.CMU14.Threats.Mobs.CLF;
using Content.Shared.CMU14.Threats.Mobs.SubvertedSynth;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Medical;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Medical.Anatomy;

[TestFixture]
public sealed class CardiacSynthKeyCommitTest
{
    private const string SubversionKey = "CMUCLFSynthSubversionKey";
    private const string ResetKey = "RMCSynthResetKeySMART";
    private const string SubversionRole = "MindRoleCLFSubvertedSynth";
    private const string Faction = "CLF";

    [TestCase(false)]
    [TestCase(true)]
    public async Task CommittedSubversionSurvivesBothInheritedHandlerOrdersAndResetRemovesIt(bool repairerLast)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        EntityUid patient = default, user = default, mind = default, subverter = default, reset = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                var coordinates = entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
                (patient, user, mind) = CreatePatient(entities, coordinates);
                subverter = PrepareKey(entities, coordinates, user, SubversionKey);
                Assert.That(entities.HasComponent<SynthRepairerComponent>(subverter), Is.True,
                    "The production subversion key inherits the reset-key listener as well as its own.");
                ConfigureInheritedOrder(entities, subverter, repairerLast);
                entities.System<MobStateSystem>().ChangeMobState(patient, MobState.Dead);

                Zap(entities, subverter, patient, user);
                AssertSubverted(entities, patient, mind);
            });

            // Include queued component/role removal and normal lifecycle updates;
            // a transient state inside one success listener is insufficient.
            await pair.RunTicksSync(2);
            await pair.Server.WaitAssertion(() =>
            {
                AssertSubverted(entities, patient, mind);
                var coordinates = entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
                reset = PrepareKey(entities, coordinates, user, ResetKey);
                Assert.That(entities.HasComponent<SynthSubverterComponent>(reset), Is.False);
                entities.System<MobStateSystem>().ChangeMobState(patient, MobState.Dead);
                Zap(entities, reset, patient, user);
                Assert.That(entities.GetComponent<MobStateComponent>(patient).CurrentState, Is.EqualTo(MobState.Alive));
                Assert.That(entities.System<NpcFactionSystem>().IsMember(patient, Faction), Is.False);
                Assert.That(entities.HasComponent<CMUSynthKeyAdditionalMarkerComponent>(patient), Is.False);
            });
            await pair.RunTicksSync(2);
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(entities.HasComponent<SynthComponent>(patient), Is.True,
                    "Reset retires subversion, while preserving the patient's synthetic body.");
                Assert.That(entities.HasComponent<SubvertedSynthComponent>(patient), Is.False);
                Assert.That(entities.HasComponent<CLFMemberComponent>(patient), Is.False);
                Assert.That(RoleCount(entities, mind), Is.Zero);
                Assert.That(entities.GetComponent<MindComponent>(mind).CurrentEntity, Is.EqualTo(patient));
            });
        }
        finally
        {
            await pair.Server.WaitPost(() => Delete(entities, patient, user, subverter, reset, mind));
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task VetoedPublicSubversionKeyDoesNotChangeFactionComponentsOrRole()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        EntityUid patient = default, user = default, mind = default, subverter = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                entities.System<CMUSynthKeyVetoProbeSystem>();
                var coordinates = entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
                (patient, user, mind) = CreatePatient(entities, coordinates);
                subverter = PrepareKey(entities, coordinates, user, SubversionKey);
                ConfigureInheritedOrder(entities, subverter, repairerLast: true);
                entities.AddComponent<CMUSynthKeyVetoProbeComponent>(subverter);
                entities.System<MobStateSystem>().ChangeMobState(patient, MobState.Dead);
                Zap(entities, subverter, patient, user);
                Assert.Multiple(() =>
                {
                    Assert.That(entities.GetComponent<CMUSynthKeyVetoProbeComponent>(subverter).Invocations, Is.EqualTo(1));
                    Assert.That(entities.GetComponent<MobStateComponent>(patient).CurrentState, Is.EqualTo(MobState.Dead));
                    Assert.That(entities.System<NpcFactionSystem>().IsMember(patient, Faction), Is.False);
                    Assert.That(entities.HasComponent<SubvertedSynthComponent>(patient), Is.False);
                    Assert.That(entities.HasComponent<CMUSynthKeyAdditionalMarkerComponent>(patient), Is.False);
                    Assert.That(entities.HasComponent<CLFMemberComponent>(patient), Is.False);
                    Assert.That(RoleCount(entities, mind), Is.Zero);
                });
            });
        }
        finally
        {
            await pair.Server.WaitPost(() => Delete(entities, patient, user, subverter, mind));
        }
        await pair.CleanReturnAsync();
    }

    private static (EntityUid Patient, EntityUid User, EntityUid Mind) CreatePatient(
        IEntityManager entities, MapCoordinates coordinates)
    {
        var patient = entities.SpawnEntity("CMMobHuman", coordinates);
        // This is the public runtime conversion used by synthetic jobs, including
        // its physiology, thresholds, damage modifier and blood initialization.
        entities.EnsureComponent<SynthComponent>(patient);
        var user = entities.SpawnEntity("CMMobHuman", coordinates);
        entities.System<SkillsSystem>().SetSkill(user, "RMCSkillEngineer", 2);
        var minds = entities.System<MindSystem>();
        var mind = minds.CreateMind(null).Owner;
        minds.TransferTo(mind, patient);
        Assert.That(entities.GetComponent<MindComponent>(mind).CurrentEntity, Is.EqualTo(patient));
        Assert.That(entities.System<NpcFactionSystem>().IsMember(patient, Faction), Is.False);
        Assert.That(RoleCount(entities, mind), Is.Zero);
        return (patient, user, mind);
    }

    private static EntityUid PrepareKey(IEntityManager entities, MapCoordinates coordinates,
        EntityUid user, string prototype)
    {
        var key = entities.SpawnEntity(prototype, coordinates);
        Assert.That(entities.System<ItemToggleSystem>().TryActivate(key, user: user), Is.True);
        return key;
    }

    private static void ConfigureInheritedOrder(IEntityManager entities, EntityUid key, bool repairerLast)
    {
        var original = entities.GetComponent<SynthSubverterComponent>(key);
        Assert.That(original.Role, Is.EqualTo(SubversionRole));
        Assert.That(original.Faction.Id, Is.EqualTo(Faction));
        if (repairerLast)
        {
            entities.RemoveComponent<SynthRepairerComponent>(key);
            entities.AddComponent<SynthRepairerComponent>(key);
        }
        else
        {
            // Exercise the other component dispatch order while retaining the
            // actual production key's configuration, rather than synthesizing a
            // success event or relying on one incidental component insertion order.
            entities.RemoveComponent<SynthSubverterComponent>(key);
            var replacement = entities.AddComponent<SynthSubverterComponent>(key);
            replacement.Faction = original.Faction;
            replacement.Role = original.Role;
            replacement.Briefing = original.Briefing;
            replacement.Sound = original.Sound;
            original = replacement;
        }

        // The CLF key's resource registry is empty. A test-only per-entity marker
        // exercises the same registry ownership used by configured cultist keys,
        // without modifying prototypes or depending on their separate role resource.
        original.AdditionalComponents = new ComponentRegistry
        {
            ["CMUSynthKeyAdditionalMarker"] = new EntityPrototype.ComponentRegistryEntry(
                new CMUSynthKeyAdditionalMarkerComponent()),
        };
    }

    private static void Zap(IEntityManager entities, EntityUid key, EntityUid patient, EntityUid user)
    {
        var defibrillator = entities.System<SharedDefibrillatorSystem>();
        Assert.That(defibrillator.CanZap(key, patient, user), Is.True);
        defibrillator.Zap(key, patient, user);
    }

    private static void AssertSubverted(IEntityManager entities, EntityUid patient, EntityUid mind)
    {
        Assert.Multiple(() =>
        {
            Assert.That(entities.GetComponent<MobStateComponent>(patient).CurrentState, Is.EqualTo(MobState.Alive));
            Assert.That(entities.System<NpcFactionSystem>().IsMember(patient, Faction), Is.True);
            Assert.That(entities.HasComponent<SubvertedSynthComponent>(patient), Is.True);
            Assert.That(entities.HasComponent<CMUSynthKeyAdditionalMarkerComponent>(patient), Is.True);
            Assert.That(entities.HasComponent<CLFMemberComponent>(patient), Is.True);
            Assert.That(RoleCount(entities, mind), Is.EqualTo(1));
        });
    }

    private static int RoleCount(IEntityManager entities, EntityUid mind)
        => entities.GetComponent<MindComponent>(mind).MindRoleContainer.ContainedEntities.Count(role =>
            entities.GetComponent<MetaDataComponent>(role).EntityPrototype?.ID == SubversionRole);

    private static void Delete(IEntityManager entities, params EntityUid[] targets)
    {
        foreach (var target in targets)
            if (entities.EntityExists(target)) entities.DeleteEntity(target);
    }
}

[RegisterComponent]
public sealed partial class CMUSynthKeyAdditionalMarkerComponent : Component;

[RegisterComponent]
public sealed partial class CMUSynthKeyVetoProbeComponent : Component
{
    public int Invocations;
}

public sealed partial class CMUSynthKeyVetoProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUSynthKeyVetoProbeComponent, RMCDefibrillatorDamageModifyEvent>(OnModify,
            after: [typeof(RMCDefibrillatorSystem)]);
    }

    private static void OnModify(Entity<CMUSynthKeyVetoProbeComponent> ent, ref RMCDefibrillatorDamageModifyEvent args)
    {
        ent.Comp.Invocations++;
        args.Cancelled = true;
    }
}
