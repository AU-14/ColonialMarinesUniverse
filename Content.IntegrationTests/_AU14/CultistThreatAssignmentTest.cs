#nullable enable

using System.Collections.Generic;
using System.Linq;
using Content.Server.AU14.Threats;
using Content.Server.Radio.Components;
using Content.Shared.AU14;
using Content.Shared.AU14.Threats;
using Content.Shared.Chat;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared._AU14.Xeno;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Vendors;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._AU14;

[TestFixture]
public sealed class CultistThreatAssignmentTest
{
    private static readonly ProtoId<JobPrototype> ThreatMemberJob = "AU14JobThreatMember";
    private static readonly ProtoId<JobPrototype> CultistJob = "AU14JobCultist";
    private static readonly ProtoId<ThreatPrototype> CultistThreatOnMarker = "cultistThreatOnMarker";

    [Test]
    public async Task AssignedCultistThreatMemberKeepsCultistJobAndMindRole()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var player = server.PlayerMan.Sessions.Single();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            entMan.SpawnEntity("cultistcfthreatmemberspawnmarker", map.GridCoords);

            var threat = server.ProtoMan.Index(CultistThreatOnMarker);
            var assignedJobs = new Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>
            {
                [player.UserId] = (ThreatMemberJob, EntityUid.Invalid),
            };

            entMan.System<AuThreatSystem>().SpawnThreatFromVote(
                threat,
                map.MapId,
                assignedJobs,
                [player.UserId]);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mindSystem = entMan.System<SharedMindSystem>();
            var jobSystem = entMan.System<SharedJobSystem>();
            var roleSystem = entMan.System<SharedRoleSystem>();

            var mindId = mindSystem.GetMind(player.UserId);
            Assert.That(mindId, Is.Not.Null);

            var mind = entMan.GetComponent<MindComponent>(mindId!.Value);
            Assert.That(mind.OwnedEntity, Is.Not.Null);
            var cultist = mind.OwnedEntity!.Value;

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<CultistComponent>(cultist), Is.True);
                Assert.That(entMan.HasComponent<XenoComponent>(cultist), Is.False);
                Assert.That(entMan.HasComponent<HasKnowledgeOfXenoLanguageComponent>(cultist), Is.True);
                Assert.That(entMan.GetComponent<IntrinsicRadioTransmitterComponent>(cultist).Channels, Does.Contain(SharedChatSystem.HivemindChannel.Id));
                Assert.That(entMan.GetComponent<ActiveRadioComponent>(cultist).Channels, Does.Contain(SharedChatSystem.HivemindChannel.Id));
                Assert.That(entMan.GetComponent<CMVendorUserComponent>(cultist).Id, Is.EqualTo(CultistJob));
                Assert.That(jobSystem.MindTryGetJobId(mindId.Value, out var job) ? job : null, Is.EqualTo(CultistJob));
                Assert.That(MindHasRolePrototype(entMan, mind, "MindRoleCultist"), Is.True);
                Assert.That(roleSystem.MindHasRole<JobRoleComponent>(mindId.Value), Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    private static bool MindHasRolePrototype(IEntityManager entMan, MindComponent mind, string prototype)
    {
        foreach (var role in mind.MindRoles)
        {
            if (entMan.TryGetComponent(role, out MetaDataComponent? meta) &&
                meta.EntityPrototype?.ID == prototype)
            {
                return true;
            }
        }

        return false;
    }
}
