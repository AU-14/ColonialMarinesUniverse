using Content.Server.CMU14.Threats.GrandMachine;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Mind;
using Content.Shared._RMC14.Synth;
using Content.Shared.CMU14.Threats;
using Content.Shared.CMU14.Threats.Mobs.WorkingJoe.Hijack;
using Content.Shared.Mind;
using Content.Shared.NPC.Systems;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Threats.WorkingJoes;

/// <summary>
/// A hijacked Joe reads as a colony Joe: the body opts out of the THREAT faction and the generic threat mind role,
/// a player in it gets the hijacked role and never the threat one, it is not handed guns for being an antagonist,
/// and it hears the Grand Machine's channel with the Early Warning Joes.
/// </summary>
[TestFixture]
public sealed class WorkingJoesIdentityTest
{
    private const string Member = "CMUMobHijackedJoe";
    private const string Leader = "CMUMobHijackedJoeLeader";
    private static readonly string ThreatFaction = "THREAT";
    private const string ThreatMindRole = "MindRoleThreat";

    [Test]
    public async Task AHijackedJoeIsNoThreatOnPaperAndHasNoGuns()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var player = pair.Player!;
        EntityUid body = default;

        await server.WaitAssertion(() =>
        {
            foreach (var id in new[] { Member, Leader })
            {
                Assert.That(prototypes.Index<EntityPrototype>(id)
                    .TryGetComponent<CMUThreatBodyOptionsComponent>(out var options, entities.ComponentFactory), id);
                Assert.Multiple(() =>
                {
                    Assert.That(options!.SkipThreatFaction, $"{id} skips the THREAT faction");
                    Assert.That(options.SkipThreatMindRole, $"{id} skips the generic threat mind role");
                });
            }

            body = entities.SpawnEntity(Member, map.GridCoords);
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var identifier = entities.GetComponent<GhostRoleComponent>(body).Identifier;
            Assert.That(entities.System<GhostRoleSystem>().Takeover(player, identifier));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var minds = entities.System<MindSystem>();
            var roles = entities.System<SharedRoleSystem>();
            var factions = entities.System<NpcFactionSystem>();
            var radio = entities.System<GrandMachineRadioSystem>();

            Assert.That(minds.TryGetMind(body, out var mindId, out var mind));
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<ThreatComponent>(body), Is.False, "no threat marker");
                Assert.That(factions.IsMember(body, ThreatFaction), Is.False, "no THREAT faction");
                Assert.That(roles.MindHasRole<CMUHijackedJoeRoleComponent>(mindId), "the hijacked role");
                Assert.That(HasRoleFrom(entities, mind!, ThreatMindRole), Is.False, "and never the generic threat role");
                Assert.That(entities.GetComponent<SynthComponent>(body).CanUseGuns, Is.False,
                    "an antagonist role does not hand a hijacked Joe guns");
                Assert.That(radio.HasChannel(body), "the Grand Machine radio");
            });
        });

        await pair.CleanReturnAsync();
    }

    private static bool HasRoleFrom(IEntityManager entities, MindComponent mind, string prototype)
    {
        foreach (var role in mind.MindRoleContainer.ContainedEntities)
        {
            if (entities.GetComponent<MetaDataComponent>(role).EntityPrototype?.ID == prototype)
                return true;
        }

        return false;
    }
}
