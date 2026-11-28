using System.Linq;
using Content.Server.CMU14.Threats.GrandMachine;
using Content.Server.CMU14.Threats.Mobs.WorkingJoe.Shipment;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Mind;
using Content.Shared.Actions.Components;
using Content.Shared.CMU14.Combat.CQC;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Threats.Mobs.WorkingJoe.Hijack;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Threats.WorkingJoes;

/// <summary>
/// At deploy the shipment's leader is always promoted to an Early Warning Joe, and seated members make up the rest
/// of the rule's target: with N rolled Early Warning Joes, max(1, target - N) - 1 extras. Admin-made Early Warning
/// Joes do not reduce the count. An unclaimed leader is promoted when a mind arrives. Every Early Warning Joe,
/// natural ones included, gets the conversion Offer; promoted bodies stop being hijacked but stay threat units.
/// </summary>
[TestFixture]
public sealed class WorkingJoesEWPromotionTest
{
    private const string Rule = "CMUWorkingJoesShipmentRule";
    private const string Leader = "CMUMobHijackedJoeLeader";
    private const string Member = "CMUMobHijackedJoe";
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string EWJoeMindRole = "MindRoleEWJoe";
    private const int Members = 4;

    /// <summary>The group-less actions a hijacked body carries; the Early Warning Joe uses its own wheel versions.</summary>
    private static readonly string?[] HijackActions = { "CMUActionDrown", "CMUActionOpticalZoom", "CMUActionOffer" };

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public async Task TheLeaderAndEnoughMembersArePromoted(int rolled)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        EntityUid leader = default;
        var members = new List<EntityUid>();
        var naturals = new List<EntityUid>();

        await server.WaitAssertion(() =>
        {
            var minds = entities.System<MindSystem>();
            var roles = entities.System<SharedRoleSystem>();
            var shipment = entities.System<WorkingJoesShipmentRuleSystem>();

            for (var i = 0; i < rolled; i++)
            {
                naturals.Add(NaturalJoe(entities, minds, roles, map.GridCoords, EWJoeSource.Rolled));
            }

            // an admin-made Early Warning Joe is not a rolled one
            naturals.Add(NaturalJoe(entities, minds, roles, map.GridCoords, EWJoeSource.Admin));

            leader = Seated(entities, minds, Leader, map.GridCoords);
            for (var i = 0; i < Members; i++)
            {
                members.Add(Seated(entities, minds, Member, map.GridCoords));
            }

            var ruleEntity = entities.SpawnEntity(Rule, MapCoordinates.Nullspace);
            var rule = entities.GetComponent<CMUWorkingJoesShipmentRuleComponent>(ruleEntity);
            Assert.That(shipment.CountRolledJoes(), Is.EqualTo(rolled), "only rolled Early Warning Joes count");

            shipment.Deploy(rule, new[] { leader }, members);

            var expectedExtras = Math.Max(1, rule.EWJoeTarget - rolled) - 1;
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<EWJoeComponent>(leader), "the leader is always promoted");
                Assert.That(minds.TryGetMind(leader, out var mindId, out _)
                    && roles.MindHasRole<EWJoeRoleComponent>(mindId, out var role)
                    && role.Value.Comp2.Source == EWJoeSource.Threat, "as a threat-made Early Warning Joe");
                Assert.That(members.Count(m => entities.HasComponent<EWJoeComponent>(m)), Is.EqualTo(expectedExtras),
                    $"{rolled} rolled: max(1, target - rolled) - 1 extras");

                foreach (var joe in naturals.Append(leader).Concat(members.Where(m => entities.HasComponent<EWJoeComponent>(m))))
                {
                    Assert.That(entities.HasComponent<CMUJoeConversionComponent>(joe), "every Early Warning Joe can convert");
                    Assert.That(entities.System<GrandMachineRadioSystem>().HasChannel(joe),
                        "and every Early Warning Joe, natural ones included, is on the Grand Machine channel");
                }
            });
        });

        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<CMUHijackedJoeComponent>(leader), Is.False, "a promoted Joe is no longer hijacked");
                Assert.That(entities.HasComponent<CMUJoeThreatUnitComponent>(leader), "but still a threat unit");

                // the Early Warning kit replaces the hijack kit: the full CQC list, not the shipment's single
                // sequence, and none of the hijack's own actions next to the Joe's
                Assert.That(entities.GetComponent<CMUCqcComponent>(leader).Sequences, Has.Count.GreaterThan(1),
                    "the promoted Joe carries the Early Warning CQC list");
                var leaderActions = entities.GetComponent<ActionsComponent>(leader).Actions
                    .Select(action => entities.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID)
                    .ToList();
                Assert.That(leaderActions.Intersect(HijackActions), Is.Empty, "the hijack's actions are gone");
                Assert.That(entities.System<MindSystem>().TryGetMind(leader, out var leaderMind, out _)
                    && entities.System<SharedRoleSystem>().MindHasRole<CMUHijackedJoeRoleComponent>(leaderMind),
                    Is.False, "and so is the hijacked mind role");
                Assert.That(entities.HasComponent<CMUHijackedJoeComponent>(members.First(m => !entities.HasComponent<EWJoeComponent>(m))),
                    "members left alone stay hijacked");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AnUnclaimedLeaderIsPromotedWhenAMindArrives()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        EntityUid leader = default;

        await server.WaitAssertion(() =>
        {
            var minds = entities.System<MindSystem>();
            var shipment = entities.System<WorkingJoesShipmentRuleSystem>();

            leader = entities.SpawnEntity(Leader, map.GridCoords);
            var member = entities.SpawnEntity(Member, map.GridCoords);
            var rule = entities.GetComponent<CMUWorkingJoesShipmentRuleComponent>(entities.SpawnEntity(Rule, MapCoordinates.Nullspace));

            shipment.Deploy(rule, new[] { leader }, new[] { member });
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<EWJoeComponent>(leader), Is.False, "nobody plays the leader yet");
                Assert.That(entities.HasComponent<CMUPendingEWJoeComponent>(leader), "so it waits for a mind");
                Assert.That(entities.HasComponent<EWJoeComponent>(member), Is.False, "a mindless member is never promoted");
            });

            minds.TransferTo(minds.CreateMind(null, "Leader"), leader);
            Assert.That(entities.HasComponent<EWJoeComponent>(leader), "the leader is promoted as its mind arrives");
        });

        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.HasComponent<CMUPendingEWJoeComponent>(leader), Is.False);
            Assert.That(entities.HasComponent<CMUJoeConversionComponent>(leader));
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>A shipment body with a mind, as the seating leaves it.</summary>
    private static EntityUid Seated(IEntityManager entities, MindSystem minds, string proto, EntityCoordinates coords)
    {
        var body = entities.SpawnEntity(proto, coords);
        entities.RemoveComponent<GhostTakeoverAvailableComponent>(body);
        entities.RemoveComponent<GhostRoleComponent>(body);
        minds.TransferTo(minds.CreateMind(null, proto), body);
        return body;
    }

    /// <summary>An Early Warning Joe already in the round, with its source as the antag rule or an admin set it.</summary>
    private static EntityUid NaturalJoe(IEntityManager entities, MindSystem minds, SharedRoleSystem roles,
        EntityCoordinates coords, EWJoeSource source)
    {
        var body = entities.SpawnEntity(ColonyJoe, coords);
        entities.RemoveComponent<GhostTakeoverAvailableComponent>(body);
        entities.RemoveComponent<GhostRoleComponent>(body);
        var mind = minds.CreateMind(null, "Early Warning Joe");
        minds.TransferTo(mind, body);
        roles.MindAddRole(mind, EWJoeMindRole, silent: true);
        Assert.That(roles.MindHasRole<EWJoeRoleComponent>(mind.Owner, out var role));
        role!.Value.Comp2.Source = source;
        entities.EnsureComponent<EWJoeComponent>(body);
        return body;
    }
}
