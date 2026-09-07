#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Collections;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.Antag.Components;
using Content.Server.Mind;
using Content.Server.Station.Systems;
using Content.Shared.Antag;
using Content.Shared.Mind.Components;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.Roles.Jobs;
using Robust.Server.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Roles;

[TestFixture]
public sealed class RoleSuccessorMergeRegressionTest : GameTest
{
    private static readonly ProtoId<JobPrototype> Alpha = "RoleMergeAlpha";
    private static readonly ProtoId<JobPrototype> Beta = "RoleMergeBeta";
    private static readonly ProtoId<JobPrototype> Allowed = "RoleMergeAllowed";
    private static readonly ProtoId<JobPrototype> Blocked = "RoleMergeBlocked";
    private static readonly ProtoId<JobPrototype> Other = "RoleMergeOther";

    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
        DummyTicker = false,
    };

    [TestPrototypes]
    private const string Prototypes = """
        - type: playTimeTracker
          id: RoleMergeTracker

        - type: playTimeTracker
          id: RoleMergeAllowedTracker

        - type: playTimeTracker
          id: RoleMergeBlockedTracker

        - type: playTimeTracker
          id: RoleMergeOtherTracker

        - type: job
          id: RoleMergeAlpha
          name: role merge alpha
          playTimeTracker: RoleMergeTracker
          canBeAntag: false
          weight: 7
          displayWeight: 11
          allegianceOverride: UA
          originwhitelist: [ UAMexico ]
          originblackist: [ UAColonies ]
          autoOpenGuides: [ Antagonists ]

        - type: job
          id: RoleMergeBeta
          name: role merge beta
          playTimeTracker: RoleMergeTracker
          weight: 3

        - type: job
          id: RoleMergeAllowed
          name: role merge allowed
          playTimeTracker: RoleMergeAllowedTracker

        - type: job
          id: RoleMergeBlocked
          name: role merge blocked
          playTimeTracker: RoleMergeBlockedTracker

        - type: job
          id: RoleMergeOther
          name: role merge other
          playTimeTracker: RoleMergeOtherTracker

        - type: department
          id: RoleMergeLowDepartment
          name: department-Cargo
          description: department-Cargo-description
          color: "#123456"
          weight: 5
          roles: [ RoleMergeAlpha ]

        - type: department
          id: RoleMergeHighDepartment
          name: department-Cargo
          description: department-Cargo-description
          color: "#654321"
          weight: 15
          roles: [ RoleMergeBeta ]

        - type: jobWeight
          id: RoleMergeMapWeights
          weights:
            RoleMergeAlpha: 40
            Captain: 99

        - type: antagSpecifier
          id: RoleMergeNoFilterSpec
          prefRoles: [ GenericAntagonist ]

        - type: antagSpecifier
          id: RoleMergeWhitelistSpec
          prefRoles: [ GenericAntagonist ]
          jobWhitelist: [ RoleMergeAllowed ]

        - type: antagSpecifier
          id: RoleMergeBlacklistSpec
          prefRoles: [ GenericAntagonist ]
          jobBlacklist: [ RoleMergeBlocked ]

        - type: entity
          id: RoleMergeNoFilterRule
          parent: BaseGameRule
          components:
          - type: GameRule
          - type: AntagSelection
            antags:
            - !type:FixedAntagCount
              proto: RoleMergeNoFilterSpec

        - type: entity
          id: RoleMergeWhitelistRule
          parent: BaseGameRule
          components:
          - type: GameRule
          - type: AntagSelection
            antags:
            - !type:FixedAntagCount
              proto: RoleMergeWhitelistSpec

        - type: entity
          id: RoleMergeBlacklistRule
          parent: BaseGameRule
          components:
          - type: GameRule
          - type: AntagSelection
            antags:
            - !type:FixedAntagCount
              proto: RoleMergeBlacklistSpec
        """;

    [Test]
    public async Task JobFieldsTrackerAndWeightsPreserveAllFallbacks()
    {
        await Server.WaitAssertion(() => AssertJobPrototypeContract(SProtoMan, Server.System<SharedJobSystem>(),
            Server.System<StationJobsSystem>()));
        await Client.WaitAssertion(() => AssertJobFields(CProtoMan.Index(Alpha)));
    }

    [Test]
    public async Task PreselectedAntagCannotReceiveImmuneJobThroughEitherCandidatePath()
    {
        var session = ServerSession!;
        EntityUid rule = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                rule = SpawnRule("RoleMergeNoFilterRule", "RoleMergeNoFilterSpec", session);
                var jobs = Server.System<StationJobsSystem>();
                var profile = HumanoidCharacterProfile.Random()
                    .WithJobPriorities(Array.Empty<KeyValuePair<ProtoId<JobPrototype>, JobPriority>>())
                    .WithJobPriority(Alpha, JobPriority.High)
                    .WithJobPriority(Beta, JobPriority.Medium);
                var profiles = new Dictionary<NetUserId, HumanoidCharacterProfile>
                {
                    [session.UserId] = profile,
                };
                var fallbackProfiles = new Dictionary<NetUserId, HumanoidCharacterProfile>
                {
                    [session.UserId] = HumanoidCharacterProfile.Random()
                        .WithJobPriorities(Array.Empty<KeyValuePair<ProtoId<JobPrototype>, JobPriority>>()),
                };

                var candidates = GetJobCandidates(jobs, profiles);
                Assert.Multiple(() =>
                {
                    Assert.That(candidates.Contains(Alpha), Is.False,
                        "normal preference assignment must reject a preselected antagonist from canBeAntag:false");
                    Assert.That(candidates.Contains(Beta), Is.True,
                        "the same antagonist remains eligible for an ordinary job without specifier filters");
                    Assert.That(PickIgnoringPreferences(jobs, Alpha, fallbackProfiles, out _), Is.False,
                        "minimum-role fallback must apply canBeAntag even though it ignores preferences");
                    Assert.That(PickIgnoringPreferences(jobs, Beta, fallbackProfiles, out var picked), Is.True,
                        "an eligible minimum role may use the same player even with no selected job preferences");
                    Assert.That(picked, Is.EqualTo(session.UserId));
                });
            });

            await Delete(rule);
            rule = default;

            await Server.WaitAssertion(() =>
            {
                rule = SpawnRule("RoleMergeWhitelistRule", "RoleMergeWhitelistSpec", session);
                var jobs = Server.System<StationJobsSystem>();
                var profiles = OneProfile(session,
                    (Allowed, JobPriority.High),
                    (Other, JobPriority.High));
                var candidates = GetJobCandidates(jobs, profiles);
                Assert.Multiple(() =>
                {
                    Assert.That(candidates.Contains(Allowed), Is.True);
                    Assert.That(candidates.Contains(Other), Is.False,
                        "upstream antag job whitelists remain authoritative");
                    Assert.That(PickIgnoringPreferences(jobs, Other, profiles, out _), Is.False);
                });
            });

            await Delete(rule);
            rule = default;

            await Server.WaitAssertion(() =>
            {
                rule = SpawnRule("RoleMergeBlacklistRule", "RoleMergeBlacklistSpec", session);
                var jobs = Server.System<StationJobsSystem>();
                var profiles = OneProfile(session,
                    (Allowed, JobPriority.High),
                    (Blocked, JobPriority.High));
                var candidates = GetJobCandidates(jobs, profiles);
                Assert.Multiple(() =>
                {
                    Assert.That(candidates.Contains(Allowed), Is.True);
                    Assert.That(candidates.Contains(Blocked), Is.False,
                        "upstream antag job blacklists remain authoritative");
                    Assert.That(PickIgnoringPreferences(jobs, Blocked, profiles, out _), Is.False);
                });
            });
        }
        finally
        {
            await Delete(rule);
        }
    }

    [Test]
    public async Task CurrentMindOwnerGetsImmediateRolePvsAndClientReplacement()
    {
        var oldOwners = await Server.AddDummySessions(1);
        var oldOwner = oldOwners.Single();
        var currentOwner = ServerSession!;
        var originalAttached = currentOwner.AttachedEntity;
        EntityUid originalMind = default;
        MindComponent? originalMindComp = null;
        EntityUid mind = default;
        EntityUid body = default;
        EntityUid role = default;
        NetEntity roleNet = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var minds = Server.System<MindSystem>();
                originalMind = minds.GetMind(currentOwner.UserId)!.Value;
                originalMindComp = SEntMan.GetComponent<MindComponent>(originalMind);
                body = SEntMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
                mind = minds.CreateMind(oldOwner.UserId, "role merge old owner");
                minds.TransferTo(mind, body);
                minds.SetUserId(mind, currentOwner.UserId);

                var mindComp = SEntMan.GetComponent<MindComponent>(mind);
                Assert.Multiple(() =>
                {
                    Assert.That(mindComp.OriginalOwnerUserId, Is.EqualTo(oldOwner.UserId));
                    Assert.That(mindComp.UserId, Is.EqualTo(currentOwner.UserId));
                });

                Server.System<SharedRoleSystem>().MindAddJobRole(mind, mindComp, jobPrototype: Alpha.Id);
                Assert.That(mindComp.MindRoleContainer.ContainedEntities, Has.Count.EqualTo(1));
                role = mindComp.MindRoleContainer.ContainedEntities.Single();
                roleNet = SEntMan.GetNetEntity(role);
                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.GetComponent<MindRoleComponent>(role).JobPrototype, Is.EqualTo(Alpha.Id));
                    Assert.That(HasSessionOverride(currentOwner, role), Is.True,
                        "new roles must be sent immediately to the mind's current owner");
                    Assert.That(HasSessionOverride(oldOwner, role), Is.False,
                        "OriginalOwnerUserId must not leak secret role PVS after reassignment");
                });
            });
            await Pair.RunTicksSync(4);

            await Client.WaitAssertion(() =>
            {
                var clientRole = CEntMan.GetEntity(roleNet);
                Assert.That(CEntMan.GetComponent<MindRoleComponent>(clientRole).JobPrototype, Is.EqualTo(Alpha.Id));
            });

            await Server.WaitAssertion(() =>
            {
                var mindComp = SEntMan.GetComponent<MindComponent>(mind);
                Server.System<SharedRoleSystem>().MindAddJobRole(mind, mindComp, jobPrototype: Beta.Id);
                Assert.Multiple(() =>
                {
                    Assert.That(mindComp.MindRoleContainer.ContainedEntities, Is.EqualTo(new[] { role }),
                        "job replacement mutates the existing contained role rather than spawning a duplicate");
                    Assert.That(SEntMan.GetComponent<MindRoleComponent>(role).JobPrototype, Is.EqualTo(Beta.Id));
                });
            });
            await Pair.RunTicksSync(4);

            await Client.WaitAssertion(() =>
            {
                var clientRole = CEntMan.GetEntity(roleNet);
                Assert.That(CEntMan.GetComponent<MindRoleComponent>(clientRole).JobPrototype, Is.EqualTo(Beta.Id),
                    "replacement must Dirty the auto-networked MindRoleComponent");
            });

            await Server.WaitAssertion(() =>
            {
                var pvs = Server.System<PvsOverrideSystem>();
                pvs.RemoveSessionOverride(role, currentOwner);
                Assert.That(HasSessionOverride(currentOwner, role), Is.False);
                Server.PlayerMan.SetAttachedEntity(currentOwner, null);
                Server.PlayerMan.SetAttachedEntity(currentOwner, body);
                Assert.That(HasSessionOverride(currentOwner, role), Is.True,
                    "reattaching to the mind container must restore every contained role override");
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                var minds = Server.System<MindSystem>();
                if (SEntMan.EntityExists(mind))
                    minds.SetUserId(mind, null);
                if (SEntMan.EntityExists(originalMind) && originalMindComp != null)
                    minds.SetUserId(originalMind, currentOwner.UserId, originalMindComp);
                Server.PlayerMan.SetAttachedEntity(currentOwner, originalAttached);
            });
            await Delete(body, mind);
            await Server.RemoveDummySession(oldOwner);
            await Pair.RunTicksSync(2);
        }
    }

    private void AssertJobPrototypeContract(
        IPrototypeManager prototypes,
        SharedJobSystem jobs,
        StationJobsSystem stationJobs)
    {
        var alpha = prototypes.Index(Alpha);
        var beta = prototypes.Index(Beta);
        var tracked = jobs.GetJobPrototypes("RoleMergeTracker");
        var duplicateTrackers = prototypes.EnumeratePrototypes<JobPrototype>()
            .Where(job => job.PlayTimeTracker.Id != "RoleMergeTracker")
            .GroupBy(job => job.PlayTimeTracker)
            .Where(group => group.Count() > 1)
            .ToList();
        var survivorJobs = jobs.GetJobPrototypes("CMJobSurvivor");
#pragma warning disable CS0618
        var legacyFirst = jobs.GetJobPrototype("RoleMergeTracker");
#pragma warning restore CS0618

        Assert.Multiple(() =>
        {
            Assert.That(tracked, Is.EquivalentTo(new[] { Alpha, Beta }));
            Assert.That(legacyFirst, Is.EqualTo(tracked[0]));
            Assert.That(survivorJobs, Does.Contain((ProtoId<JobPrototype>) "CMSurvivor"));
            Assert.That(survivorJobs, Does.Contain((ProtoId<JobPrototype>) "CMJobSurvivorSoroMiner"));
            Assert.That(jobs.TryGetListHighestWeightDepartment([Alpha, Beta], out var highest), Is.True);
            Assert.That(highest!.ID, Is.EqualTo("RoleMergeHighDepartment"));
        });
        foreach (var group in duplicateTrackers)
        {
            Assert.That(jobs.GetJobPrototypes(group.Key),
                Is.EquivalentTo(group.Select(job => (ProtoId<JobPrototype>) job.ID)),
                $"tracker {group.Key} must retain all {group.Count()} job prototypes");
        }
        AssertJobFields(alpha);

        var defaultWeights = prototypes.Index<JobWeightPrototype>(JobWeightPrototype.Default);
        var captain = prototypes.Index<JobPrototype>("Captain");
        var omittedForkJob = prototypes.Index<JobPrototype>("CMCommandingOfficer");
        Assert.Multiple(() =>
        {
            Assert.That(defaultWeights.Weights.ContainsKey(omittedForkJob.ID), Is.False,
                "the live fork job must actually exercise the legacy fallback");
            Assert.That(stationJobs.TryGetJobWeight(omittedForkJob, null, out var assignmentFork), Is.True);
            Assert.That(assignmentFork, Is.EqualTo(omittedForkJob.Weight).And.EqualTo(10));
            Assert.That(stationJobs.TryGetJobWeight(alpha, null, out var assignmentAlpha), Is.True);
            Assert.That(assignmentAlpha, Is.EqualTo(alpha.Weight).And.EqualTo(7));
            Assert.That(stationJobs.TryGetJobWeight(captain, null, out var assignmentCaptain), Is.True);
            Assert.That(assignmentCaptain, Is.EqualTo(defaultWeights.Weights[captain.ID]));
            Assert.That(stationJobs.TryGetJobWeight(alpha, "RoleMergeMapWeights", out var mapAlpha), Is.True);
            Assert.That(mapAlpha, Is.EqualTo(40));
            Assert.That(stationJobs.TryGetJobWeight(captain, "RoleMergeMapWeights", out var mapCaptain), Is.True);
            Assert.That(mapCaptain, Is.EqualTo(99));
            Assert.That(stationJobs.TryGetJobWeight(omittedForkJob, "RoleMergeMapWeights", out var mapFork), Is.True);
            Assert.That(mapFork, Is.EqualTo(10));
        });

        Assert.Multiple(() =>
        {
            Assert.That(JobUIComparer.Instance.GetWeight(alpha), Is.EqualTo(alpha.RealDisplayWeight).And.EqualTo(11));
            Assert.That(JobUIComparer.Instance.GetWeight(omittedForkJob),
                Is.EqualTo(omittedForkJob.RealDisplayWeight).And.EqualTo(10));
        });
        Assert.That(JobUIComparer.TryCreate(prototypes, null, out var defaults), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(defaults!.GetWeight(omittedForkJob), Is.EqualTo(omittedForkJob.RealDisplayWeight).And.EqualTo(10));
            Assert.That(defaults.GetWeight(alpha), Is.EqualTo(alpha.RealDisplayWeight).And.EqualTo(11));
            Assert.That(defaults.GetWeight(captain), Is.EqualTo(defaultWeights.Weights[captain.ID]));
        });

        Assert.That(JobUIComparer.TryCreate(prototypes, "RoleMergeMapWeights", out var mapped), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(mapped!.GetWeight(alpha), Is.EqualTo(40));
            Assert.That(mapped.GetWeight(captain), Is.EqualTo(99));
            Assert.That(mapped.GetWeight(omittedForkJob), Is.EqualTo(10));
            Assert.That(mapped.Compare(alpha, captain), Is.GreaterThan(0),
                "the larger map override sorts Captain ahead of Alpha");
        });
    }

    private static void AssertJobFields(JobPrototype job)
    {
        Assert.Multiple(() =>
        {
            Assert.That(job.CanBeAntag, Is.False);
            Assert.That(job.Weight, Is.EqualTo(7));
            Assert.That(job.DisplayWeight, Is.EqualTo(11));
            Assert.That(job.RealDisplayWeight, Is.EqualTo(11));
            Assert.That(job.AllegianceOverride?.Id, Is.EqualTo("UA"));
            Assert.That(job.IgnoreAllegiance, Is.False);
            Assert.That(job.OriginWhitelist?.Select(origin => origin.Id), Is.EquivalentTo(new[] { "UAMexico" }));
            Assert.That(job.OriginBlackist?.Select(origin => origin.Id), Is.EquivalentTo(new[] { "UAColonies" }));
            Assert.That(job.AutoOpenGuides?.Select(guide => guide.Id), Is.EqualTo(new[] { "Antagonists" }));
        });
    }

    private EntityUid SpawnRule(string prototype, ProtoId<AntagSpecifierPrototype> specifier, ICommonSession session)
    {
        var rule = SEntMan.SpawnEntity(prototype, MapCoordinates.Nullspace);
        var selection = SEntMan.GetComponent<AntagSelectionComponent>(rule);
        selection.PreSelectedSessions[specifier] = [session];
        return rule;
    }

    private static Dictionary<NetUserId, HumanoidCharacterProfile> OneProfile(
        ICommonSession session,
        params (ProtoId<JobPrototype> Job, JobPriority Priority)[] jobs)
    {
        var profile = HumanoidCharacterProfile.Random()
            .WithJobPriorities(Array.Empty<KeyValuePair<ProtoId<JobPrototype>, JobPriority>>());
        foreach (var (job, priority) in jobs)
        {
            profile = profile.WithJobPriority(job, priority);
        }

        return new Dictionary<NetUserId, HumanoidCharacterProfile>
        {
            [session.UserId] = profile,
        };
    }

    private static IDictionary GetJobCandidates(
        StationJobsSystem system,
        IReadOnlyDictionary<NetUserId, HumanoidCharacterProfile> profiles)
    {
        return (IDictionary) typeof(StationJobsSystem)
            .GetMethod("GetJobCandidates", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(system, [profiles])!;
    }

    private static bool PickIgnoringPreferences(
        StationJobsSystem system,
        ProtoId<JobPrototype> job,
        IReadOnlyDictionary<NetUserId, HumanoidCharacterProfile> profiles,
        out NetUserId player)
    {
        object?[] args = [job, profiles, default(NetUserId)];
        var result = (bool) typeof(StationJobsSystem)
            .GetMethod("TryPickCandidateIgnoringPreferences", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(system, args)!;
        player = (NetUserId) args[2]!;
        return result;
    }

    private bool HasSessionOverride(ICommonSession session, EntityUid entity)
    {
        var overrides = (IDictionary) typeof(PvsOverrideSystem)
            .GetField("SessionOverrides", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(Server.System<PvsOverrideSystem>())!;
        if (!overrides.Contains(session))
            return false;

        return ((IEnumerable<EntityUid>) overrides[session]!).Contains(entity);
    }

    private async Task Delete(params EntityUid[] entities)
    {
        await Server.WaitPost(() =>
        {
            foreach (var uid in entities)
            {
                if (SEntMan.EntityExists(uid))
                    SEntMan.DeleteEntity(uid);
            }
        });
    }
}

#pragma warning restore RA0002
