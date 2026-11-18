using System.Collections.Generic;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.CMU14.Round.Antags.EarlyWarning;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Mind;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.GameTicking.Components;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// FLOW-07: the ewjoemechanics switch only stops the antag being rolled. An admin's "Make Early Warning Joe"
/// runs inside <see cref="EWJoeRuleSystem.AdminOverride"/> and makes a working Joe with the switch off; the
/// same call without the override still makes nobody, and switching off again leaves the admin's Joe alone.
/// </summary>
[TestFixture]
public sealed class EWJoeAdminBypassTest
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";

    [Test]
    public async Task AnAdminMakesAJoeWhileTheSwitchIsOff()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var player = pair.Player!;
        EntityUid joe = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var mindSys = entities.System<MindSystem>();
                var antag = entities.System<AntagSelectionSystem>();
                var rule = entities.System<EWJoeRuleSystem>();
                var roles = entities.System<SharedRoleSystem>();

                // a colony Working Joe played by the test player
                joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
                entities.RemoveComponent<GhostTakeoverAvailableComponent>(joe);
                entities.RemoveComponent<GhostRoleComponent>(joe);
                var mind = mindSys.CreateMind(player.UserId, "Colony Joe");
                mindSys.TransferTo(mind, joe);
                Assert.That(player.AttachedEntity, Is.EqualTo(joe));

                cfg.SetCVar(CCVars.CMUEWJoeEnabled, false);
                Assert.That(rule.Enabled, Is.False);

                // without the override the switch wins: the rule is stripped as it is added and nobody is made
                // (upstream ForceGetGameRuleEnt then trips over the removed selection component)
                try
                {
                    antag.ForceMakeAntag<GameRuleComponent>(player, EWJoeRuleSystem.AntagId, EWJoeRuleSystem.AntagId);
                }
                catch (KeyNotFoundException)
                {
                }

                Assert.That(entities.HasComponent<EWJoeComponent>(joe), Is.False, "no override, no Joe");

                using (rule.AdminOverride())
                {
                    antag.ForceMakeAntag<GameRuleComponent>(player, EWJoeRuleSystem.AntagId, EWJoeRuleSystem.AntagId);
                }

                Assert.That(entities.TryGetComponent(joe, out EWJoeComponent? comp), "the admin's Joe is made");
                Assert.Multiple(() =>
                {
                    Assert.That(comp!.Level, Is.EqualTo(0), "at level 0");
                    Assert.That(mindSys.TryGetMind(joe, out var mindId, out _) && roles.MindHasRole<EWJoeRoleComponent>(mindId),
                        "with the mind role");
                    Assert.That(LiveAdminRule(entities), "and a live, admin-forced rule");
                });
            });

            // switching it on and off again only withdraws untaken Distress ghost roles
            await server.WaitPost(() =>
            {
                cfg.SetCVar(CCVars.CMUEWJoeEnabled, true);
                cfg.SetCVar(CCVars.CMUEWJoeEnabled, false);
            });

            await pair.RunTicksSync(5);

            await server.WaitAssertion(() =>
            {
                Assert.That(entities.HasComponent<EWJoeComponent>(joe), "the admin's Joe survives the switch");
                Assert.That(LiveAdminRule(entities), "and so does its rule");
            });
        }
        finally
        {
            await server.WaitPost(() => cfg.SetCVar(CCVars.CMUEWJoeEnabled, true));
        }

        await pair.CleanReturnAsync();
    }

    private static bool LiveAdminRule(IEntityManager entities)
    {
        var query = entities.EntityQueryEnumerator<EWJoeRuleComponent, AntagSelectionComponent>();
        while (query.MoveNext(out var uid, out var rule, out _))
        {
            if (rule.AdminForced && !entities.HasComponent<EndedGameRuleComponent>(uid))
                return true;
        }

        return false;
    }
}
