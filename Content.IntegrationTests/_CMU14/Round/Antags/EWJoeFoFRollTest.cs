using System.Collections.Generic;
using System.Linq;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.Antag.Selectors;
using Content.Server.CMU14.Round.Antags;
using Content.Server.CMU14.Round.Antags.EarlyWarning;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Antag;
using Content.Shared.CCVar;
using Content.Shared.GameTicking.Components;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// Force on Force rolls the Early Warning Joe at 35% from its own table (FoFAntags), and the rolled rule offers
/// 1-2 ghost-role Joes scaled by player count, never more than 2 (ARCH-01). The ghost role is gated by the
/// synthetic whitelist only and previews a Working Joe (COUPLING-01).
/// </summary>
[TestFixture]
public sealed class EWJoeFoFRollTest
{
    private const string FoFRoller = "FoFAntags";
    private const string FoFRule = "EWJoeFoF";
    private const string FoFPreset = "ForceOnForce";
    private const string ForcedRoller = "CMUTestFoFAntagsForced";
    private const string GhostSpawner = "CMUSpawnPointGhostEWJoe";
    private const string ColonyJoeJob = "AU14JobColonyWorkingJoe";
    private const string ColonyJoePreview = "AU14MobWorkingJoeDummy";

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUTestFoFAntagsForced
          parent: BaseGameRule
          components:
          - type: GameRule
          - type: DistressAntagsRule
            rules:
              EWJoeFoF: 1
        """;

    [Test]
    public async Task FoFRollsItsOwnTableAndCapsAtTwoJoes()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var protos = server.ResolveDependency<IPrototypeManager>();
        var cfg = server.ResolveDependency<IConfigurationManager>();

        await server.WaitAssertion(() =>
        {
            // the preset carries the roller, and the roller's table is the 35% Joe
            Assert.That(protos.Index<GamePresetPrototype>(FoFPreset).Rules, Does.Contain(new EntProtoId(FoFRoller)));
            Assert.That(protos.Index<EntityPrototype>(FoFRoller).TryGetComponent(out DistressAntagsRuleComponent? roller, entities.ComponentFactory));
            Assert.That(roller!.Rules, Is.EquivalentTo(new Dictionary<EntProtoId, float> { [FoFRule] = 0.35f }));

            // the rolled rule is the planet ghost-role Joe on the Distress host specifier, so the rule system's gates match it
            var rule = protos.Index<EntityPrototype>(FoFRule);
            Assert.That(rule.TryGetComponent(out EWJoeRuleComponent? ewRule, entities.ComponentFactory));
            Assert.That(ewRule!.HostOnPlanet);
            Assert.That(rule.TryGetComponent(out AntagSelectionComponent? selection, entities.ComponentFactory));
            Assert.That(selection!.SelectionTime, Is.EqualTo(AntagSelectionTime.Never));
            Assert.That(selection.Antags, Has.Length.EqualTo(1));
            var selector = selection.Antags[0];
            Assert.That(selector, Is.TypeOf<LinearAntagCount>());
            Assert.That(selector.Proto, Is.EqualTo(new ProtoId<AntagSpecifierPrototype>(EWJoeRuleSystem.DistressHostId)));

            // the count the spawner path computes (fresh running count, the ready-player count)
            var antag = entities.System<AntagSelectionSystem>();
            int Count(int players)
            {
                var running = 0;
                return antag.GetTargetAntagCount(selector, players, ref running);
            }

            Assert.Multiple(() =>
            {
                Assert.That(Count(100), Is.EqualTo(1), "100 players: one Joe");
                Assert.That(Count(149), Is.EqualTo(1));
                Assert.That(Count(150), Is.EqualTo(2), "150 players: two");
                Assert.That(Count(225), Is.EqualTo(2), "never three");
                for (var players = 0; players <= 1000; players += 25)
                {
                    Assert.That(Count(players), Is.InRange(1, 2), $"{players} players");
                }
            });

            // a table entry that always wins adds the FoF rule
            cfg.SetCVar(CCVars.CMUEWJoeEnabled, true);
            var ticker = entities.System<GameTicker>();
            List<EntityUid> FoFRules() => ticker.GetAddedGameRules()
                .Where(uid => entities.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == FoFRule)
                .ToList();
            var before = FoFRules();
            ticker.AddGameRule(ForcedRoller);
            var added = FoFRules().Except(before).ToList();
            Assert.That(added, Has.Count.EqualTo(1), "the forced roll adds one FoF rule");
            Assert.That(entities.HasComponent<AntagSelectionComponent>(added[0]), "which can still select");
            Assert.That(entities.HasComponent<EndedGameRuleComponent>(added[0]), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GhostRoleIsSyntheticWhitelistOnly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var protos = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            Assert.That(protos.Index<EntityPrototype>(GhostSpawner).TryGetComponent(out GhostRoleComponent? role, entities.ComponentFactory));
            Assert.Multiple(() =>
            {
                Assert.That(role!.JobProto, Is.EqualTo(new ProtoId<JobPrototype>(ColonyJoeJob)), "the job brings the synthetic whitelist");
                Assert.That(role.Requirements, Is.Not.Null, "an override, not the job's requirements");
                Assert.That(role.Requirements, Is.Empty, "with no trait requirements");
                Assert.That(protos.Index<JobPrototype>(ColonyJoeJob).JobPreviewEntity?.Id, Is.EqualTo(ColonyJoePreview),
                    "the raffle previews a Working Joe");
            });
        });

        await pair.CleanReturnAsync();
    }
}
