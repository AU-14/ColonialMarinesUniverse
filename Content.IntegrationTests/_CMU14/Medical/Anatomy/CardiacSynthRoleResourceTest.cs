using System.Linq;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Synth;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Threats.Mobs.CLF;
using Content.Shared.CMU14.Threats.Mobs.Cultist;
using Content.Shared.CMU14.Threats.Mobs.SubvertedSynth;
using Content.Shared.FixedPoint;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Medical;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Radio.Components;
using Content.Shared.Roles.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Medical.Anatomy;

[TestFixture]
public sealed class CardiacSynthRoleResourceTest
{
    private const string ClfKey = "CMUCLFSynthSubversionKey";
    private const string CultistKey = "AU14SynthSubversionKeyCultist";
    private const string ResetKey = "RMCSynthResetKeySMART";
    private const string ClfRole = "MindRoleCLFSubvertedSynth";
    private const string CultistRole = "MindRoleXenoHackedSynth";
    private const string NativeRole = "MindRoleCultist";
    private const string ClfFaction = "CLF";
    private const string XenoFaction = "Xeno";
    private const string WeyuFaction = "AUWeYu";

    [Test]
    public async Task RepeatedCultistKeysThenResetRestoreWorkingJoesOriginalRadios()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        EntityUid patient = default, user = default, mind = default, first = default, second = default, reset = default;
        string[] transmit = [], receive = [];
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                var coordinates = entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
                (patient, user, mind) = CreatePatient(entities, coordinates, "AU14MobWorkingJoeColony");
                transmit = entities.GetComponent<IntrinsicRadioTransmitterComponent>(patient).Channels
                    .Select(channel => channel.Id).ToArray();
                receive = entities.GetComponent<ActiveRadioComponent>(patient).Channels
                    .Select(channel => channel.Id).ToArray();
                Assert.That(transmit, Is.EquivalentTo(new[] { "radioAI", "Colony", "radioWEYU", "radioCMB" }));
                // Radio startup adds every intrinsic transmit channel to reception.
                Assert.That(receive, Is.EquivalentTo(transmit));
                Assert.That(entities.HasComponent<IntrinsicRadioReceiverComponent>(patient), Is.True);
                first = UseKey(entities, coordinates, patient, user, CultistKey);
                Assert.That(entities.GetComponent<IntrinsicRadioTransmitterComponent>(patient).Channels
                    .Select(channel => channel.Id), Is.EquivalentTo(new[] { "Hivemind" }));
            });
            await pair.RunTicksSync(2);
            await pair.Server.WaitAssertion(() =>
            {
                var coordinates = entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
                second = UseKey(entities, coordinates, patient, user, CultistKey);
                Assert.That(entities.GetComponent<ActiveRadioComponent>(patient).Channels
                    .Select(channel => channel.Id), Is.EquivalentTo(new[] { "Hivemind" }));
            });
            await pair.RunTicksSync(2);
            await pair.Server.WaitAssertion(() =>
            {
                var coordinates = entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
                reset = UseKey(entities, coordinates, patient, user, ResetKey);
            });
            await pair.RunTicksSync(2);
            await pair.Server.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(entities.GetComponent<MobStateComponent>(patient).CurrentState, Is.EqualTo(MobState.Alive));
                    Assert.That(entities.GetComponent<IntrinsicRadioTransmitterComponent>(patient).Channels
                        .Select(channel => channel.Id), Is.EquivalentTo(transmit));
                    Assert.That(entities.GetComponent<ActiveRadioComponent>(patient).Channels
                        .Select(channel => channel.Id), Is.EquivalentTo(receive));
                    Assert.That(entities.GetComponent<ActiveRadioComponent>(patient).ReceiveAllChannels, Is.False);
                    Assert.That(entities.GetComponent<ActiveRadioComponent>(patient).GlobalReceive, Is.False);
                    Assert.That(entities.HasComponent<IntrinsicRadioReceiverComponent>(patient), Is.True);
                    Assert.That(entities.System<NpcFactionSystem>().IsMember(patient, WeyuFaction), Is.True);
                    Assert.That(entities.System<NpcFactionSystem>().IsMember(patient, XenoFaction), Is.False);
                    Assert.That(entities.HasComponent<SubvertedSynthComponent>(patient), Is.False);
                    Assert.That(MarkedRoles(entities, mind), Is.Empty);
                });
            });
        }
        finally
        {
            await pair.Server.WaitPost(() => Delete(entities, patient, user, first, second, reset, mind));
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CrossKeyAndResetPreserveIndependentClfFactionAndExactMedicIcon()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        EntityUid patient = default, user = default, mind = default, first = default, second = default, reset = default;
        CLFMemberComponent member = default!;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                var coordinates = entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
                (patient, user, mind) = CreatePatient(entities, coordinates);
                entities.System<NpcFactionSystem>().AddFaction(patient, ClfFaction);
                member = entities.AddComponent<CLFMemberComponent>(patient);
                member.StatusIcon = "CLFFactionMedic";
                first = UseKey(entities, coordinates, patient, user, ClfKey);
                Assert.That(entities.GetComponent<CLFMemberComponent>(patient), Is.SameAs(member));
            });
            await pair.RunTicksSync(2);
            await pair.Server.WaitAssertion(() =>
            {
                var coordinates = entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
                second = UseKey(entities, coordinates, patient, user, CultistKey);
                Assert.That(entities.System<NpcFactionSystem>().IsMember(patient, ClfFaction), Is.True);
                Assert.That(entities.System<NpcFactionSystem>().IsMember(patient, XenoFaction), Is.True);
                Assert.That(entities.GetComponent<CLFMemberComponent>(patient), Is.SameAs(member));
                Assert.That(member.StatusIcon.Id, Is.EqualTo("CLFFactionMedic"));
            });
            await pair.RunTicksSync(2);
            await pair.Server.WaitAssertion(() =>
            {
                var coordinates = entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
                reset = UseKey(entities, coordinates, patient, user, ResetKey);
            });
            await pair.RunTicksSync(2);
            await pair.Server.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(entities.GetComponent<MobStateComponent>(patient).CurrentState, Is.EqualTo(MobState.Alive));
                    Assert.That(entities.System<NpcFactionSystem>().IsMember(patient, ClfFaction), Is.True);
                    Assert.That(entities.System<NpcFactionSystem>().IsMember(patient, XenoFaction), Is.False);
                    Assert.That(entities.GetComponent<CLFMemberComponent>(patient), Is.SameAs(member));
                    Assert.That(member.StatusIcon.Id, Is.EqualTo("CLFFactionMedic"));
                    Assert.That(entities.HasComponent<SubvertedSynthComponent>(patient), Is.False);
                    Assert.That(MarkedRoles(entities, mind), Is.Empty);
                });
            });
        }
        finally
        {
            await pair.Server.WaitPost(() => Delete(entities, patient, user, first, second, reset, mind));
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ResetPreservesAnIndependentlyReplacedRadioAndItsCurrentValues()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        EntityUid patient = default, user = default, mind = default, key = default, reset = default;
        ActiveRadioComponent replacement = default!;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                var coordinates = entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
                (patient, user, mind) = CreatePatient(entities, coordinates, "AU14MobWorkingJoeColony");
                key = UseKey(entities, coordinates, patient, user, CultistKey);
                var applied = entities.GetComponent<ActiveRadioComponent>(patient);
                entities.RemoveComponent<ActiveRadioComponent>(patient);
                replacement = entities.AddComponent<ActiveRadioComponent>(patient);
                replacement.Channels.Add("radioWEYU");
                replacement.ReceiveAllChannels = true;
                replacement.GlobalReceive = true;
                Assert.That(replacement, Is.Not.SameAs(applied));
            });
            await pair.RunTicksSync(2);
            await pair.Server.WaitAssertion(() =>
            {
                var coordinates = entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
                reset = UseKey(entities, coordinates, patient, user, ResetKey);
            });
            await pair.RunTicksSync(2);
            await pair.Server.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(entities.GetComponent<MobStateComponent>(patient).CurrentState, Is.EqualTo(MobState.Alive));
                    Assert.That(entities.GetComponent<ActiveRadioComponent>(patient), Is.SameAs(replacement));
                    Assert.That(replacement.Channels.Select(channel => channel.Id), Is.EquivalentTo(new[] { "radioWEYU" }));
                    Assert.That(replacement.ReceiveAllChannels, Is.True);
                    Assert.That(replacement.GlobalReceive, Is.True);
                    Assert.That(entities.GetComponent<IntrinsicRadioTransmitterComponent>(patient).Channels
                        .Select(channel => channel.Id), Is.EquivalentTo(new[] { "radioAI", "Colony", "radioWEYU", "radioCMB" }));
                    Assert.That(entities.HasComponent<SubvertedSynthComponent>(patient), Is.False);
                    Assert.That(MarkedRoles(entities, mind), Is.Empty);
                });
            });
        }
        finally
        {
            await pair.Server.WaitPost(() => Delete(entities, patient, user, key, reset, mind));
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BothPeersResolveSubversionRolesWithExistingAntagonistPolicies()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() => AssertRoles(pair.Server.ResolveDependency<IPrototypeManager>()));
        await pair.Client.WaitAssertion(() => AssertRoles(pair.Client.ResolveDependency<IPrototypeManager>()));
        await pair.CleanReturnAsync();
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public async Task ActualKeysRetainOrReplaceOnlyTheirRoleAndResetPreservesIndependentRole(
        bool cultistFirst, bool changeKey)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        EntityUid patient = default, user = default, mind = default, first = default, second = default,
            reset = default, independentRole = default, firstRole = default;
        var secondCultist = changeKey ? !cultistFirst : cultistFirst;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                var coordinates = entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
                (patient, user, mind) = CreatePatient(entities, coordinates);
                // This independently assigned role shares the cultist policy,
                // but has no subversion marker and must survive both keys/reset.
                entities.System<RoleSystem>().MindAddRole(mind, NativeRole);
                independentRole = Roles(entities, mind, NativeRole).Single();
                first = UseKey(entities, coordinates, patient, user, cultistFirst ? CultistKey : ClfKey);
                AssertSubverted(entities, patient, mind, cultistFirst);
                firstRole = MarkedRoles(entities, mind).Single();
            });
            await pair.RunTicksSync(2);
            await pair.Server.WaitAssertion(() =>
            {
                var coordinates = entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
                second = UseKey(entities, coordinates, patient, user, secondCultist ? CultistKey : ClfKey);
                AssertSubverted(entities, patient, mind, secondCultist);
                var secondRole = MarkedRoles(entities, mind).Single();
                Assert.That(secondRole, changeKey ? Is.Not.EqualTo(firstRole) : Is.EqualTo(firstRole),
                    "Repeated use retains the role entity; a different configured key replaces its owned role.");
                Assert.That(Roles(entities, mind, NativeRole).Single(), Is.EqualTo(independentRole));
            });
            await pair.RunTicksSync(2);
            await pair.Server.WaitAssertion(() =>
            {
                AssertSubverted(entities, patient, mind, secondCultist);
                var coordinates = entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
                reset = UseKey(entities, coordinates, patient, user, ResetKey);
                Assert.That(entities.GetComponent<MobStateComponent>(patient).CurrentState, Is.EqualTo(MobState.Alive));
                Assert.That(MarkedRoles(entities, mind), Is.Empty);
                Assert.That(Roles(entities, mind, NativeRole).Single(), Is.EqualTo(independentRole));
                Assert.That(entities.System<NpcFactionSystem>().IsMember(patient, XenoFaction), Is.False);
                Assert.That(entities.System<NpcFactionSystem>().IsMember(patient, ClfFaction), Is.False);
            });
            await pair.RunTicksSync(2);
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(entities.HasComponent<SubvertedSynthComponent>(patient), Is.False);
                Assert.That(entities.HasComponent<CultistComponent>(patient), Is.False);
                Assert.That(entities.HasComponent<CLFMemberComponent>(patient), Is.False);
                Assert.That(entities.HasComponent<SynthComponent>(patient), Is.True);
                Assert.That(entities.GetComponent<MindComponent>(mind).CurrentEntity, Is.EqualTo(patient));
                Assert.That(Roles(entities, mind, NativeRole).Single(), Is.EqualTo(independentRole));
            });
        }
        finally
        {
            await pair.Server.WaitPost(() => Delete(entities, patient, user, first, second, reset, mind));
        }
        await pair.CleanReturnAsync();
    }

    [TestCase("MissingCMUSubversionRole")]
    [TestCase(NativeRole)]
    public async Task InvalidConfiguredRoleVetoesActualKeyBeforeRevival(string role)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        EntityUid patient = default, user = default, mind = default, key = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                var coordinates = entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
                (patient, user, mind) = CreatePatient(entities, coordinates);
                key = PrepareKey(entities, coordinates, user, ClfKey);
                entities.GetComponent<SynthSubverterComponent>(key).Role = role;
                entities.System<MobStateSystem>().ChangeMobState(patient, MobState.Dead);
                Zap(entities, key, patient, user);
                Assert.Multiple(() =>
                {
                    Assert.That(entities.GetComponent<MobStateComponent>(patient).CurrentState, Is.EqualTo(MobState.Dead));
                    Assert.That(entities.HasComponent<SubvertedSynthComponent>(patient), Is.False);
                    Assert.That(entities.HasComponent<CLFMemberComponent>(patient), Is.False);
                    Assert.That(entities.System<NpcFactionSystem>().IsMember(patient, ClfFaction), Is.False);
                    Assert.That(entities.GetComponent<MindComponent>(mind).MindRoleContainer.ContainedEntities, Is.Empty);
                });
            });
        }
        finally
        {
            await pair.Server.WaitPost(() => Delete(entities, patient, user, key, mind));
        }
        await pair.CleanReturnAsync();
    }

    private static void AssertRoles(IPrototypeManager prototypes)
    {
        foreach (var role in new[] { ClfRole, CultistRole })
        {
            var prototype = prototypes.Index<EntityPrototype>(role);
            Assert.That(prototype.Components.ContainsKey("SubvertedSynthRole"), Is.True);
            var policy = (MindRoleComponent) prototype.Components["MindRole"].Component;
            Assert.That(policy.AntagPrototype?.Id,
                Is.EqualTo(role == ClfRole ? "CLFSubvertedSynthRole" : "CultistRole"));
        }
        Assert.That(prototypes.Index<EntityPrototype>(NativeRole).Components.ContainsKey("SubvertedSynthRole"),
            Is.False, "The existing cultist role itself must not become reset-owned.");
    }

    private static (EntityUid Patient, EntityUid User, EntityUid Mind) CreatePatient(
        IEntityManager entities, MapCoordinates coordinates, string prototype = "CMMobHuman")
    {
        var patient = entities.SpawnEntity(prototype, coordinates);
        entities.EnsureComponent<SynthComponent>(patient);
        var user = entities.SpawnEntity("CMMobHuman", coordinates);
        entities.System<SkillsSystem>().SetSkill(user, "RMCSkillEngineer", 2);
        var minds = entities.System<MindSystem>();
        var mind = minds.CreateMind(null).Owner;
        minds.TransferTo(mind, patient);
        Assert.That(entities.System<NpcFactionSystem>().IsMember(patient, ClfFaction), Is.False);
        Assert.That(entities.System<NpcFactionSystem>().IsMember(patient, XenoFaction), Is.False);
        return (patient, user, mind);
    }

    private static EntityUid UseKey(IEntityManager entities, MapCoordinates coordinates,
        EntityUid patient, EntityUid user, string prototype)
    {
        // Repeated key use tests role ownership, not accumulated shock trauma.
        // Heal the exact attached heart through its public medical API before
        // each new death; the cardiac lifecycle fixture tests trauma separately.
        Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<HeartComponent>(patient, out var heart),
            Is.True);
        entities.System<SharedOrganHealthSystem>().HealOrgan(heart, patient, FixedPoint2.New(100));
        var key = PrepareKey(entities, coordinates, user, prototype);
        entities.System<MobStateSystem>().ChangeMobState(patient, MobState.Dead);
        Zap(entities, key, patient, user);
        return key;
    }

    private static EntityUid PrepareKey(IEntityManager entities, MapCoordinates coordinates,
        EntityUid user, string prototype)
    {
        var key = entities.SpawnEntity(prototype, coordinates);
        Assert.That(entities.System<ItemToggleSystem>().TryActivate(key, user: user), Is.True);
        return key;
    }

    private static void Zap(IEntityManager entities, EntityUid key, EntityUid patient, EntityUid user)
    {
        var defibrillator = entities.System<SharedDefibrillatorSystem>();
        Assert.That(defibrillator.CanZap(key, patient, user), Is.True);
        defibrillator.Zap(key, patient, user);
    }

    private static void AssertSubverted(IEntityManager entities, EntityUid patient, EntityUid mind, bool cultist)
    {
        Assert.Multiple(() =>
        {
            Assert.That(entities.GetComponent<MobStateComponent>(patient).CurrentState, Is.EqualTo(MobState.Alive));
            Assert.That(entities.System<NpcFactionSystem>().IsMember(patient, ClfFaction), Is.EqualTo(!cultist));
            Assert.That(entities.System<NpcFactionSystem>().IsMember(patient, XenoFaction), Is.EqualTo(cultist));
            Assert.That(entities.GetComponent<SubvertedSynthComponent>(patient).Faction.Id,
                Is.EqualTo(cultist ? "Xeno" : "CLF"));
            Assert.That(entities.HasComponent<CLFMemberComponent>(patient), Is.EqualTo(!cultist));
            Assert.That(entities.HasComponent<CultistComponent>(patient), Is.EqualTo(cultist));
            Assert.That(MarkedRoles(entities, mind), Has.Length.EqualTo(1));
            Assert.That(Roles(entities, mind, cultist ? CultistRole : ClfRole), Has.Length.EqualTo(1));
        });
    }

    private static EntityUid[] MarkedRoles(IEntityManager entities, EntityUid mind)
        => entities.GetComponent<MindComponent>(mind).MindRoleContainer.ContainedEntities
            .Where(role => entities.HasComponent<SubvertedSynthRoleComponent>(role)).ToArray();

    private static EntityUid[] Roles(IEntityManager entities, EntityUid mind, string prototype)
        => entities.GetComponent<MindComponent>(mind).MindRoleContainer.ContainedEntities
            .Where(role => entities.GetComponent<MetaDataComponent>(role).EntityPrototype?.ID == prototype).ToArray();

    private static void Delete(IEntityManager entities, params EntityUid[] targets)
    {
        foreach (var target in targets)
            if (entities.EntityExists(target)) entities.DeleteEntity(target);
    }
}
