using System.Collections.Generic;
using System.Linq;
using Content.Server.AU14.Roles;
using Content.Server.Jobs;
using Content.Server.Station.Systems;
using Content.Shared._CMU14.Round.Roles;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.Inventory;
using Content.Shared.NPC.Components;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._AU14.Roles;

[TestFixture]
public sealed class RoundJobProfileTest
{
    private const string EarsSlot = "ears";

    private static readonly ProtoId<JobPrototype> GovforSquadRifleman = "AU14JobGOVFORSquadRifleman";
    private static readonly ProtoId<JobPrototype> OpforSquadRifleman = "AU14JobOPFORSquadRifleman";
    private static readonly ProtoId<JobPrototype> WypmcSquadRifleman = "AU14JobGOVFORSquadRiflemanWYPMC";
    private static readonly ProtoId<JobPrototype> GovforLogisticsTechnician = "AU14JobGOVFORAuxTech";
    private static readonly ProtoId<JobPrototype> OpforLogisticsTechnician = "AU14JobOPFORAuxTech";
    private static readonly ProtoId<JobPrototype> GovforNurse = "AU14JobGOVFORNurse";
    private static readonly ProtoId<JobPrototype> GovforWorkingJoe = "AU14JobGOVFORWorkingJoe";
    private static readonly ProtoId<JobPrototype> OpforWorkingJoe = "AU14JobOPFORWorkingJoe";
    private static readonly EntProtoId GovforAuxiliaryHeadset = "AU14HeadsetGovforAuxiliary";
    private static readonly EntProtoId OpforAuxiliaryHeadset = "AU14HeadsetOpforAuxiliary";
    private static readonly EntProtoId GovforAuxiliarySquad = "SquadGovforIntel";
    private static readonly EntProtoId OpforAuxiliarySquad = "SquadOpforIntel";

    [Test]
    public async Task JobsWithRoundProfilesDeclareMetadataAndResolveProfiles()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var missing = new List<string>();

            foreach (var job in prototypes.EnumeratePrototypes<JobPrototype>())
            {
                if (job.RoundProfiles.Count == 0)
                    continue;

                if (job.RoundSide == RoundJobSide.None)
                    missing.Add($"{job.ID} uses roundProfiles but has no roundSide");

                if (string.IsNullOrWhiteSpace(job.RoundForce))
                    missing.Add($"{job.ID} uses roundProfiles but has no roundForce");

                if (string.IsNullOrWhiteSpace(job.RoundRole))
                    missing.Add($"{job.ID} uses roundProfiles but has no roundRole");

                foreach (var profileId in job.RoundProfiles)
                {
                    if (!prototypes.HasIndex<RoundJobProfilePrototype>(profileId))
                        missing.Add($"{job.ID} references missing roundProfile {profileId}");
                }
            }

            Assert.That(missing, Is.Empty, string.Join("\n", missing));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task Au14JobsDoNotKeepAddComponentSpecials()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var jobsWithLegacySpecials = new List<string>();

            foreach (var job in prototypes.EnumeratePrototypes<JobPrototype>())
            {
                if (!job.ID.StartsWith("AU14Job") ||
                    job.Special.OfType<AddComponentSpecial>().Any() == false)
                {
                    continue;
                }

                jobsWithLegacySpecials.Add(job.ID);
            }

            Assert.That(jobsWithLegacySpecials, Is.Empty, string.Join("\n", jobsWithLegacySpecials));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SquadRiflemanProfileResolvesSideAndForceComponents()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var profiles = server.System<RoundJobProfileSystem>();

            var govfor = prototypes.Index(GovforSquadRifleman);
            var opfor = prototypes.Index(OpforSquadRifleman);
            var wypmc = prototypes.Index(WypmcSquadRifleman);

            Assert.That(govfor.RoundProfiles.Select(id => id.ToString()), Does.Contain("AU14RoundJobProfileSquadRifleman"));
            Assert.That(wypmc.RoundProfiles.Select(id => id.ToString()), Is.EquivalentTo(govfor.RoundProfiles.Select(id => id.ToString())));
            Assert.That(wypmc.RoundForce, Is.EqualTo("WYPMC"));

            Assert.That(profiles.GetRoundSide(govfor), Is.EqualTo(RoundJobSide.Govfor));
            Assert.That(profiles.GetRoundSide(opfor), Is.EqualTo(RoundJobSide.Opfor));
            Assert.That(profiles.GetRoundSide(wypmc), Is.EqualTo(RoundJobSide.Govfor));

            Assert.That(HasResolvedComponent(profiles, govfor, "MarineOrders"), Is.True);
            Assert.That(HasResolvedComponent(profiles, govfor, "Skills"), Is.True);
            Assert.That(HasResolvedComponent(profiles, govfor, "Marine"), Is.True);
            Assert.That(HasResolvedComponent(profiles, opfor, "UserIFF"), Is.True);
            Assert.That(HasResolvedComponent(profiles, opfor, "TacticalMapIcon"), Is.True);
            Assert.That(HasResolvedComponent(profiles, wypmc, "UserIFF"), Is.True);
            Assert.That(HasResolvedComponent(profiles, wypmc, "JobPrefix"), Is.True);
            Assert.That(HasResolvedComponent(profiles, wypmc, "Skills"), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WypmcPlatoonJobsResolveCorporateFactionAndIff()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var profiles = server.System<RoundJobProfileSystem>();
            var jobs = prototypes.EnumeratePrototypes<JobPrototype>()
                .Where(job => string.Equals(job.RoundForce, "WYPMC", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var failures = new List<string>();

            Assert.That(jobs, Is.Not.Empty);

            foreach (var job in jobs)
            {
                NpcFactionMemberComponent npcFaction = null!;
                UserIFFComponent userIff = null!;

                foreach (var resolved in profiles.GetProfileComponents(job))
                {
                    if (resolved.Components.TryGetValue("NpcFactionMember", out var npcEntry) &&
                        npcEntry.Component is NpcFactionMemberComponent npc)
                    {
                        npcFaction = npc;
                    }

                    if (resolved.Components.TryGetValue("UserIFF", out var iffEntry) &&
                        iffEntry.Component is UserIFFComponent iff)
                    {
                        userIff = iff;
                    }
                }

                if (npcFaction == null ||
                    !npcFaction.Factions.Select(faction => faction.Id).SequenceEqual(["AUWeYu"]))
                {
                    failures.Add($"{job.ID} does not resolve exclusively to the AUWeYu NPC faction");
                }

                if (userIff == null ||
                    !userIff.Factions.Select(faction => faction.Id).SequenceEqual(["FactionWEYU"]))
                {
                    failures.Add($"{job.ID} does not resolve exclusively to the FactionWEYU IFF");
                }
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SpawnedSideJobsSetMarineFactionForTacmap()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var stationSpawning = server.System<StationSpawningSystem>();
            var profile = new HumanoidCharacterProfile();
            var govfor = stationSpawning.SpawnPlayerMob(testMap.GridCoords, GovforSquadRifleman, profile, station: null);
            var opfor = stationSpawning.SpawnPlayerMob(testMap.GridCoords, OpforSquadRifleman, profile, station: null);

            Assert.Multiple(() =>
            {
                Assert.That(server.EntMan.GetComponent<MarineComponent>(govfor).Faction, Is.EqualTo("govfor"));
                Assert.That(server.EntMan.GetComponent<MarineComponent>(opfor).Faction, Is.EqualTo("opfor"));
            });

            server.EntMan.DeleteEntity(govfor);
            server.EntMan.DeleteEntity(opfor);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LogisticsTechniciansUseFactionAuxiliaryHeadsetsInGameAndPreview()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var stationSpawning = server.System<StationSpawningSystem>();
            var inventory = server.System<InventorySystem>();
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var profile = new HumanoidCharacterProfile();
            var govfor = stationSpawning.SpawnPlayerMob(
                testMap.GridCoords,
                GovforLogisticsTechnician,
                profile,
                station: null);
            var opfor = stationSpawning.SpawnPlayerMob(
                testMap.GridCoords,
                OpforLogisticsTechnician,
                profile,
                station: null);

            try
            {
                Assert.Multiple(() =>
                {
                    AssertEquippedPrototype(server.EntMan, inventory, govfor, EarsSlot, GovforAuxiliaryHeadset);
                    AssertEquippedPrototype(server.EntMan, inventory, opfor, EarsSlot, OpforAuxiliaryHeadset);
                    AssertDummyGearPrototype(prototypes, GovforLogisticsTechnician, EarsSlot, GovforAuxiliaryHeadset);
                    AssertDummyGearPrototype(prototypes, OpforLogisticsTechnician, EarsSlot, OpforAuxiliaryHeadset);
                });
            }
            finally
            {
                server.EntMan.DeleteEntity(govfor);
                server.EntMan.DeleteEntity(opfor);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MilitaryAuxiliaryRolesSpawnInFactionAuxiliarySquads()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var stationSpawning = server.System<StationSpawningSystem>();
            var profile = new HumanoidCharacterProfile();
            var jobs = new (ProtoId<JobPrototype> Job, EntProtoId Squad)[]
            {
                (GovforLogisticsTechnician, GovforAuxiliarySquad),
                (OpforLogisticsTechnician, OpforAuxiliarySquad),
                (GovforNurse, GovforAuxiliarySquad),
                (GovforWorkingJoe, GovforAuxiliarySquad),
                (OpforWorkingJoe, OpforAuxiliarySquad),
            };
            var members = new List<(EntityUid Entity, ProtoId<JobPrototype> Job, EntProtoId Squad)>();

            try
            {
                foreach (var candidate in jobs)
                {
                    var entity = stationSpawning.SpawnPlayerMob(testMap.GridCoords, candidate.Job, profile, station: null);
                    members.Add((entity, candidate.Job, candidate.Squad));
                }

                Assert.Multiple(() =>
                {
                    foreach (var member in members)
                        AssertSquadPrototype(server.EntMan, member.Entity, member.Squad, member.Job.Id);
                });
            }
            finally
            {
                foreach (var member in members)
                    server.EntMan.DeleteEntity(member.Entity);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static bool HasResolvedComponent(RoundJobProfileSystem profiles, JobPrototype job, string componentName)
    {
        foreach (var components in profiles.GetProfileComponents(job))
        {
            if (components.Components.ContainsKey(componentName))
                return true;
        }

        return false;
    }

    private static void AssertEquippedPrototype(
        IEntityManager entityManager,
        InventorySystem inventory,
        EntityUid wearer,
        string slot,
        EntProtoId expectedPrototype)
    {
        var found = inventory.TryGetSlotEntity(wearer, slot, out var equipped);
        Assert.That(found, Is.True, slot);
        Assert.That(equipped, Is.Not.Null, slot);
        if (!found || equipped == null)
            return;

        var metadata = entityManager.GetComponent<MetaDataComponent>(equipped.Value);
        Assert.That(metadata.EntityPrototype?.ID, Is.EqualTo(expectedPrototype.Id), slot);
    }

    private static void AssertDummyGearPrototype(
        IPrototypeManager prototypes,
        ProtoId<JobPrototype> jobId,
        string slot,
        EntProtoId expectedPrototype)
    {
        var job = prototypes.Index(jobId);
        Assert.That(job.DummyStartingGear, Is.Not.Null, $"{jobId} dummy gear");
        if (job.DummyStartingGear is not { } dummyGearId)
            return;

        var dummyGear = prototypes.Index<StartingGearPrototype>(dummyGearId);
        Assert.That(dummyGear.Equipment.TryGetValue(slot, out var equipped), Is.True, $"{dummyGearId} {slot}");
        Assert.That(equipped, Is.EqualTo(expectedPrototype), $"{dummyGearId} {slot}");
    }

    private static void AssertSquadPrototype(
        IEntityManager entityManager,
        EntityUid member,
        EntProtoId expectedPrototype,
        string context)
    {
        var squadMember = entityManager.GetComponent<SquadMemberComponent>(member);
        Assert.That(squadMember.Squad, Is.Not.Null, context);
        if (squadMember.Squad is not { } squad)
            return;

        var metadata = entityManager.GetComponent<MetaDataComponent>(squad);
        Assert.That(metadata.EntityPrototype?.ID, Is.EqualTo(expectedPrototype.Id), context);
    }
}
