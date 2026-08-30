using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Implants;
using Content.Shared.Clothing;
using Content.Shared.Implants;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Chameleon;

/// <summary>
/// Ensures all <see cref="IsProbablyRoundStartJob">"round start jobs"</see> have an associated chameleon loadout.
/// </summary>
[Ignore("Chameleon outfits are not used in RMC14")]
public sealed class ChameleonJobLoadoutTest : InteractionTest
{
    private static readonly List<ProtoId<JobPrototype>> JobBlacklist =
    [

    ];

    [Test]
    public Task CheckAllJobs()
    {
        var alljobs = ProtoMan.EnumeratePrototypes<JobPrototype>();

        // Job -> number of references
        Dictionary<ProtoId<JobPrototype>, int> validJobs = new();

        // Only add stuff that actually has clothing! We don't want stuff like AI or borgs.
        foreach (var job in alljobs)
        {
            if (!IsProbablyRoundStartJob(job) || JobBlacklist.Contains(job.ID))
                continue;

            validJobs.Add(job.ID, 0);
        }

        var chameleons = ProtoMan.EnumeratePrototypes<ChameleonOutfitPrototype>();

        foreach (var chameleon in chameleons)
        {
            if (chameleon.Job == null || !validJobs.ContainsKey(chameleon.Job.Value))
                continue;

            validJobs[chameleon.Job.Value] += 1;
        }

        Assert.Multiple(() =>
        {
            foreach (var job in validJobs)
            {
                Assert.That(job.Value, Is.Not.Zero,
                    $"{job.Key} has no chameleonOutfit prototype.");
            }
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Best guess at what a "round start" job is.
    /// </summary>
    private bool IsProbablyRoundStartJob(JobPrototype job)
    {
        var (key, proto) = LoadoutSystem.GetJobLoadoutInfo(job.ID, ProtoMan);
        return job.StartingGear != null && proto != null;
    }

}

/// <summary>
/// Covers the concrete profile-key/parent-role split used by the chameleon controller.
/// </summary>
[TestFixture]
[TestOf(typeof(ChameleonControllerSystem))]
public sealed class ChameleonLoadoutParentCompatibilityTest : GameTest
{
    private const string ChildJob = "ChameleonMergeChildJob";
    private const string ParentRole = "JobChameleonMergeParentJob";
    private const string ConcreteProfileKey = "JobChameleonMergeChildJob";
    private const string Group = "ChameleonMergeFootwear";
    private const string DefaultLoadout = "ChameleonMergeDefaultShoes";
    private const string CustomLoadout = "ChameleonMergeCustomShoes";

    [TestPrototypes]
    private const string Prototypes = @"
- type: loadout
  id: ChameleonMergeDefaultShoes
  cost: 1
  equipment:
    shoes: ClothingShoesColorBlack

- type: loadout
  id: ChameleonMergeCustomShoes
  cost: 2
  equipment:
    shoes: ClothingShoesColorBlue

- type: loadoutGroup
  id: ChameleonMergeFootwear
  name: generic-unknown
  minLimit: 0
  defaultSelected: 1
  maxLimit: 1
  loadouts:
  - ChameleonMergeDefaultShoes
  - ChameleonMergeCustomShoes

- type: roleLoadout
  id: JobChameleonMergeParentJob
  points: 3
  groups:
  - ChameleonMergeFootwear

- type: job
  id: ChameleonMergeParentJob
  parent: Passenger

- type: job
  id: ChameleonMergeChildJob
  parent: ChameleonMergeParentJob
";

    [Test]
    public async Task ConcreteProfileKeyUsesParentRoleDefaultsAndControllerGear()
    {
        await Server.WaitAssertion(() =>
        {
            var (key, rolePrototype) = LoadoutSystem.GetJobLoadoutInfo(ChildJob, SProtoMan);
            Assert.Multiple(() =>
            {
                Assert.That(key, Is.EqualTo(ConcreteProfileKey),
                    "the profile key must remain tied to the concrete fork job");
                Assert.That(rolePrototype, Is.Not.Null);
                Assert.That(rolePrototype!.ID, Is.EqualTo(ParentRole),
                    "a child without its own role loadout resolves its immediate parent's concrete role");
            });

            var profile = HumanoidCharacterProfile.DefaultWithSpecies();
            var defaultRoleLoadout = new RoleLoadout(rolePrototype!.ID);
            defaultRoleLoadout.SetDefault(profile, null, SProtoMan, force: true);

            Assert.Multiple(() =>
            {
                Assert.That(defaultRoleLoadout.Role.Id, Is.EqualTo(ParentRole));
                Assert.That(defaultRoleLoadout.SelectedLoadouts.ContainsKey(Group), Is.True);
                Assert.That(defaultRoleLoadout.SelectedLoadouts[Group].Select(loadout => loadout.Prototype.Id),
                    Is.EqualTo(new[] { DefaultLoadout }),
                    "defaultSelected must populate a zero-minimum group for the resolved parent role");
                Assert.That(defaultRoleLoadout.Points, Is.EqualTo(2),
                    "the fork role point budget is initialized before applying the default selection cost");
            });

            var generated = profile.GetLoadoutOrDefault(key, null, null, SEntMan, SProtoMan);
            Assert.Multiple(() =>
            {
                Assert.That(generated.Role.Id, Is.EqualTo(ParentRole));
                Assert.That(generated.SelectedLoadouts[Group].Select(loadout => loadout.Prototype.Id),
                    Is.EqualTo(new[] { DefaultLoadout }));
            });

            var customRoleLoadout = new RoleLoadout(rolePrototype.ID)
            {
                Points = 1,
                SelectedLoadouts =
                {
                    [Group] =
                    [
                        new Loadout { Prototype = CustomLoadout },
                    ],
                },
            };
            var storedProfile = profile.WithLoadout(key, customRoleLoadout);
            Assert.Multiple(() =>
            {
                Assert.That(storedProfile.Loadouts.Keys, Is.EquivalentTo(new[] { ConcreteProfileKey }));
                Assert.That(storedProfile.Loadouts[key].Role.Id, Is.EqualTo(ParentRole));
            });

            var controller = Server.System<ChameleonControllerSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(controller.GetGearForSlot(
                        chameleonOutfitPrototype: null,
                        customRoleLoadout: storedProfile.Loadouts[key],
                        defaultRoleLoadout: defaultRoleLoadout,
                        jobStartingGearPrototype: null,
                        startingGearPrototype: null,
                        slotName: "shoes"),
                    Is.EqualTo("ClothingShoesColorBlue"),
                    "the controller must read customization from the concrete fork profile key");
                Assert.That(controller.GetGearForSlot(
                        chameleonOutfitPrototype: null,
                        customRoleLoadout: null,
                        defaultRoleLoadout: defaultRoleLoadout,
                        jobStartingGearPrototype: null,
                        startingGearPrototype: null,
                        slotName: "shoes"),
                    Is.EqualTo("ClothingShoesColorBlack"),
                    "the controller falls back to SetDefault on the resolved parent role");
            });
        });
    }
}
