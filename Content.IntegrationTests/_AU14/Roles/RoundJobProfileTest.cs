using System.Collections.Generic;
using System.Linq;
using Content.Server.AU14.Roles;
using Content.Server.Jobs;
using Content.Server.Station.Systems;
using Content.Shared._CMU14.Round.Roles;
using Content.Shared._RMC14.Marines;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._AU14.Roles;

[TestFixture]
public sealed class RoundJobProfileTest
{
    private static readonly ProtoId<JobPrototype> GovforSquadRifleman = "AU14JobGOVFORSquadRifleman";
    private static readonly ProtoId<JobPrototype> OpforSquadRifleman = "AU14JobOPFORSquadRifleman";
    private static readonly ProtoId<JobPrototype> WypmcSquadRifleman = "AU14JobGOVFORSquadRiflemanWYPMC";

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

    private static bool HasResolvedComponent(RoundJobProfileSystem profiles, JobPrototype job, string componentName)
    {
        foreach (var components in profiles.GetProfileComponents(job))
        {
            if (components.Components.ContainsKey(componentName))
                return true;
        }

        return false;
    }
}
