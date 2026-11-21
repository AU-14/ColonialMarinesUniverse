using Content.Server.Mind;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Medical;
using Content.Shared._RMC14.Synth;
using Content.Shared.CMU14.Round.Antags;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// Any synthetic labelled an antagonist may fire guns, however it got the label (here the INSFOR
/// subversion key), and loses that again when a reset key takes the label away. The Early Warning Joe
/// never fires guns, even when it holds another antagonist role or its role came first.
/// </summary>
[TestFixture]
public sealed class EWJoeAntagSynthGunsTest
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string SubversionKey = "CMUCLFSynthSubversionKey";
    private const string ResetKey = "RMCSynthResetKeySMART";
    private const string SubversionRole = "MindRoleCLFSubvertedSynth";

    [Test]
    public async Task SubvertedSynthsFireGunsUntilResetAndTheJoeNever()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        EntityUid patient = default, user = default;

        await pair.Server.WaitAssertion(() =>
        {
            var minds = entities.System<MindSystem>();
            var coordinates = entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
            patient = entities.SpawnEntity("CMMobHuman", coordinates);
            entities.EnsureComponent<SynthComponent>(patient);
            user = entities.SpawnEntity("CMMobHuman", coordinates);
            entities.System<SkillsSystem>().SetSkill(user, "RMCSkillEngineer", 2);
            minds.TransferTo(minds.CreateMind(null).Owner, patient);
            Assert.That(entities.GetComponent<SynthComponent>(patient).CanUseGuns, Is.False, "a loyal synth cannot fire");

            Zap(entities, Key(entities, coordinates, user, SubversionKey), patient, user);
            Assert.That(entities.GetComponent<SynthComponent>(patient).CanUseGuns, Is.True, "the subverted synth can");
            Assert.That(entities.HasComponent<SynthAntagGunsGrantedComponent>(patient));
        });

        await pair.RunTicksSync(2);

        await pair.Server.WaitAssertion(() =>
        {
            var coordinates = entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
            Zap(entities, Key(entities, coordinates, user, ResetKey), patient, user);
        });

        await pair.RunTicksSync(2);

        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<SynthComponent>(patient).CanUseGuns, Is.False, "reset: loyal again");
            Assert.That(entities.HasComponent<SynthAntagGunsGrantedComponent>(patient), Is.False);

            // a Working Joe with another antagonist role fires guns until it becomes the Early Warning Joe
            var minds = entities.System<MindSystem>();
            var roles = entities.System<SharedRoleSystem>();
            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var mind = minds.CreateMind(null, "Early Warning Joe");
            minds.TransferTo(mind, joe);
            roles.MindAddRole(mind, SubversionRole, silent: true);
            Assert.That(entities.GetComponent<SynthComponent>(joe).CanUseGuns, Is.True, "an antagonist Working Joe can fire");

            entities.EnsureComponent<EWJoeComponent>(joe);
            Assert.That(entities.GetComponent<SynthComponent>(joe).CanUseGuns, Is.False, "the Early Warning Joe cannot");

            // and a role arriving later does not hand them back
            roles.MindAddRole(mind, "MindRoleEWJoe", silent: true);
            Assert.That(entities.GetComponent<SynthComponent>(joe).CanUseGuns, Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ArrivingMindsEarlyRolesAndStandDown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        EntityUid joe = default;

        await pair.Server.WaitAssertion(() =>
        {
            var minds = entities.System<MindSystem>();
            var roles = entities.System<SharedRoleSystem>();

            // an antagonist mind already holding its role moves into a synth body
            var synth = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var early = minds.CreateMind(null, "Subverted");
            minds.TransferTo(early, entities.SpawnEntity("CMMobHuman", map.GridCoords));
            roles.MindAddRole(early, SubversionRole, silent: true);
            Assert.That(entities.GetComponent<SynthComponent>(synth).CanUseGuns, Is.False);
            minds.TransferTo(early, synth);
            Assert.That(entities.GetComponent<SynthComponent>(synth).CanUseGuns, Is.True, "the role came with the mind");

            // the Early Warning role alone excludes, before the component exists
            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var mind = minds.CreateMind(null, "Early Warning Joe");
            minds.TransferTo(mind, joe);
            roles.MindAddRole(mind, "MindRoleEWJoe", silent: true);
            roles.MindAddRole(mind, SubversionRole, silent: true);
            Assert.That(entities.GetComponent<SynthComponent>(joe).CanUseGuns, Is.False, "an Early Warning mind, no component yet");

            entities.EnsureComponent<EWJoeComponent>(joe);
            Assert.That(entities.GetComponent<SynthComponent>(joe).CanUseGuns, Is.False);

            // standing down takes the Early Warning role; the other antag role is left and so are the guns
            entities.RemoveComponent<EWJoeComponent>(joe);
        });

        await pair.RunTicksSync(2);

        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entities.HasComponent<EWJoeComponent>(joe), Is.False);
            Assert.That(entities.GetComponent<SynthComponent>(joe).CanUseGuns, Is.True, "a stood-down Joe that is still an antagonist");
        });

        await pair.CleanReturnAsync();
    }

    private static EntityUid Key(IEntityManager entities, MapCoordinates coordinates, EntityUid user, string prototype)
    {
        var key = entities.SpawnEntity(prototype, coordinates);
        Assert.That(entities.System<ItemToggleSystem>().TryActivate(key, user: user), Is.True);
        return key;
    }

    private static void Zap(IEntityManager entities, EntityUid key, EntityUid patient, EntityUid user)
    {
        entities.System<MobStateSystem>().ChangeMobState(patient, MobState.Dead);
        var defibrillator = entities.System<SharedDefibrillatorSystem>();
        Assert.That(defibrillator.CanZap(key, patient, user), Is.True);
        defibrillator.Zap(key, patient, user);
    }
}
