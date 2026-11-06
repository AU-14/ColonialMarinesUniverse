#pragma warning disable RA0002 // reads the action's enabled flag directly

using Content.Server.CMU14.Round.Antags;
using Content.Server.CMU14.Round.Antags.EarlyWarning;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Server.Antag.Components;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Actions.Components;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using System.Linq;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// The Early Warning Joe rolls at 35% on Colony Fall, Insurgency and Distress Signal; on Distress Signal, whose
/// planet jobs are cleared, its own rule offers a colony Working Joe on the planet as an antag ghost role. The ewjoemechanics switch only stops it
/// being rolled; Joes already in the round keep playing.
/// </summary>
[TestFixture]
public sealed class EWJoeRollingToggleTest
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string HostSpawn = "AU14SpawnPointColonyWorkingJoe";
    private const string GenericSpawn = "SpawnPointLatejoin";
    private const string GhostSpawner = "CMUSpawnPointGhostEWJoe";

    [Test]
    public async Task RollsOnThreeModesAndDistressBringsItsOwnColonyJoe()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ColonyAntagsRuleSystem.AntagRulePrototypes[EWJoeRuleSystem.AntagId], Is.EqualTo(0.35f), "Colony Fall and Insurgency");
            Assert.That(DistressAntagsRuleSystem.AntagRulePrototypes[EWJoeRuleSystem.DistressAntagId], Is.EqualTo(0.35f), "Distress Signal");
        });

        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            Assert.That(prototypes.Index<AntagPrototype>("EWJoeRole").BlacklistedGamemodes, Does.Not.Contain("DistressSignal"),
                "Distress Signal no longer refuses the antag");

            // the host spot: a colony Joe spawn point when the planet has one, else any spawn point there
            var rule = entities.System<EWJoeRuleSystem>();
            var maps = entities.System<SharedMapSystem>();
            var xforms = entities.System<SharedTransformSystem>();
            maps.CreateMap(out var empty);
            Assert.That(rule.FindHostSpot(empty), Is.Null, "no spawn point, no spot");
            var other = entities.SpawnEntity(GenericSpawn, testMap.GridCoords);
            Assert.That(rule.FindHostSpot(testMap.MapId), Is.EqualTo(xforms.GetMapCoordinates(other)), "any spawn point will do");
            var joeSpot = entities.SpawnEntity(HostSpawn, EWJoeTestHelpers.TileEast(entities, testMap, 2));
            Assert.That(rule.FindHostSpot(testMap.MapId), Is.EqualTo(xforms.GetMapCoordinates(joeSpot)), "a colony Joe spawn point first");
            entities.DeleteEntity(other);
            maps.DeleteMap(empty);
        });

        // Distress Signal's own rule offers the antag as a ghost role: taking it puts a player in a colony Joe that is
        // the Early Warning Joe. (Where the spawner goes is FindHostSpot on the planet; the test pool runs no round.)
        await server.WaitAssertion(() =>
        {
            var ticker = entities.System<GameTicker>();
            var ruleEntity = ticker.AddGameRule(EWJoeRuleSystem.DistressAntagId);
            Assert.That(entities.GetComponent<EWJoeRuleComponent>(ruleEntity).HostOnPlanet);

            var spawner = entities.SpawnEntity(GhostSpawner, EWJoeTestHelpers.TileEast(entities, testMap, 3));
            var antagSpawner = entities.GetComponent<GhostRoleAntagSpawnerComponent>(spawner);
            antagSpawner.Rule = ruleEntity;
            antagSpawner.Definition = EWJoeRuleSystem.DistressHostId;

            var player = server.PlayerMan.Sessions.First();
            var take = new TakeGhostRoleEvent(player);
            entities.EventBus.RaiseLocalEvent(spawner, ref take);
            Assert.That(take.TookRole, "the role was taken");
            var body = player.AttachedEntity;
            Assert.That(body, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(entities.GetComponent<MetaDataComponent>(body!.Value).EntityPrototype?.ID, Is.EqualTo(ColonyJoe), "a colony Working Joe");
                Assert.That(entities.HasComponent<EWJoeComponent>(body.Value), "it is the Early Warning Joe");
                Assert.That(entities.GetComponent<EWJoeComponent>(body.Value).GrantedActions, Is.Not.Empty, "set up with its level-0 kit");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TheSwitchOnlyStopsTheAntagBeingRolled()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var cfg = server.ResolveDependency<IConfigurationManager>();
        EntityUid joe = default, spawner = default, ruleEntity = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var rule = entities.System<EWJoeRuleSystem>();
                joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
                var comp = entities.EnsureComponent<EWJoeComponent>(joe);
                rule.ApplyLevel((joe, comp), 4);
                Assert.That(entities.System<EWJoeRespawnSystem>().Grant((joe, comp)), "respawn granted");
                Assert.That(comp.GrantedActions, Is.Not.Empty);

                // a Distress Signal ghost role nobody has taken yet
                spawner = entities.SpawnEntity(GhostSpawner, EWJoeTestHelpers.TileEast(entities, map, 3));
                entities.GetComponent<GhostRoleAntagSpawnerComponent>(spawner).Definition = EWJoeRuleSystem.DistressHostId;

                cfg.SetCVar(CCVars.CMUEWJoeEnabled, false);
                Assert.That(rule.Enabled, Is.False);

                // the rules roll nobody while it is off
                ruleEntity = entities.System<GameTicker>().AddGameRule(EWJoeRuleSystem.AntagId);
                Assert.That(entities.HasComponent<EndedGameRuleComponent>(ruleEntity), "the rule ends before selecting anyone");
                Assert.That(entities.HasComponent<AntagSelectionComponent>(ruleEntity), Is.False, "and can never select later");

                // the Joe already in the round keeps every ability, including ones it gains while it is off
                rule.ApplyLevel((joe, comp), 5);
                foreach (var action in comp.GrantedActions)
                {
                    Assert.That(entities.GetComponent<ActionComponent>(action).Enabled, Is.True, "no action greys out");
                }

                // and its respawn still turns a lethal blow back
                var protos = server.ResolveDependency<IPrototypeManager>();
                entities.System<DamageableSystem>().TryChangeDamage(joe,
                    new DamageSpecifier(protos.Index<DamageTypePrototype>("Blunt"), 1000), ignoreResistances: true);
                Assert.Multiple(() =>
                {
                    Assert.That(entities.System<MobStateSystem>().IsDead(joe), Is.False, "the respawn is honoured");
                    Assert.That(comp.RespawnReady, Is.False, "and spent");
                });
            });

            await pair.RunTicksSync(5);

            await server.WaitAssertion(() =>
            {
                Assert.That(entities.Deleted(spawner), "the untaken ghost role is withdrawn");
                Assert.That(entities.HasComponent<EWJoeComponent>(joe), "the Joe is still the antag");

                cfg.SetCVar(CCVars.CMUEWJoeEnabled, true);
                Assert.That(entities.System<EWJoeRuleSystem>().Enabled);
                entities.DeleteEntity(ruleEntity);
            });
        }
        finally
        {
            await server.WaitPost(() => cfg.SetCVar(CCVars.CMUEWJoeEnabled, true));
        }

        await pair.CleanReturnAsync();
    }
}
