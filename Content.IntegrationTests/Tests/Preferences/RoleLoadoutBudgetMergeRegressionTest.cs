using Content.IntegrationTests.Fixtures;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;

namespace Content.IntegrationTests.Tests.Preferences;

[TestFixture]
[TestOf(typeof(RoleLoadout))]
public sealed class RoleLoadoutBudgetMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: loadout
          id: RoleLoadoutBudgetCostOne
          cost: 1

        - type: loadout
          id: RoleLoadoutBudgetCostTwo
          cost: 2

        - type: loadout
          id: RoleLoadoutBudgetCostThree
          cost: 3

        - type: loadout
          id: RoleLoadoutBudgetCostFour
          cost: 4

        - type: loadoutGroup
          id: RoleLoadoutBudgetDefaultFirst
          name: generic-unknown
          minLimit: 0
          maxLimit: 1
          defaultSelected: 1
          loadouts:
          - RoleLoadoutBudgetCostThree

        - type: loadoutGroup
          id: RoleLoadoutBudgetDefaultSecond
          name: generic-unknown
          minLimit: 0
          maxLimit: 1
          defaultSelected: 1
          loadouts:
          - RoleLoadoutBudgetCostThree

        - type: loadoutGroup
          id: RoleLoadoutBudgetExistingTwo
          name: generic-unknown
          minLimit: 0
          maxLimit: 1
          loadouts:
          - RoleLoadoutBudgetCostTwo

        - type: loadoutGroup
          id: RoleLoadoutBudgetExistingFour
          name: generic-unknown
          minLimit: 0
          maxLimit: 1
          loadouts:
          - RoleLoadoutBudgetCostFour

        - type: loadoutGroup
          id: RoleLoadoutBudgetUnlimited
          name: generic-unknown
          minLimit: 0
          maxLimit: 0
          defaultSelected: 3
          loadouts:
          - RoleLoadoutBudgetCostOne
          - RoleLoadoutBudgetCostTwo
          - RoleLoadoutBudgetCostThree

        - type: roleLoadout
          id: RoleLoadoutBudgetCumulative
          points: 5
          groups:
          - RoleLoadoutBudgetDefaultFirst
          - RoleLoadoutBudgetDefaultSecond

        - type: roleLoadout
          id: RoleLoadoutBudgetPreserved
          points: 5
          groups:
          - RoleLoadoutBudgetDefaultFirst
          - RoleLoadoutBudgetExistingTwo

        - type: roleLoadout
          id: RoleLoadoutBudgetRejected
          points: 5
          groups:
          - RoleLoadoutBudgetDefaultFirst
          - RoleLoadoutBudgetExistingFour

        - type: roleLoadout
          id: RoleLoadoutBudgetUnlimitedRole
          points: 10
          groups:
          - RoleLoadoutBudgetUnlimited

        - type: loadout
          id: RoleLoadoutBudgetRepeatable
          cost: 3
          maxSelections: 2

        - type: loadoutGroup
          id: RoleLoadoutBudgetRepeatableUnlimited
          name: generic-unknown
          minLimit: 0
          maxLimit: 0
          loadouts:
          - RoleLoadoutBudgetRepeatable

        - type: roleLoadout
          id: RoleLoadoutBudgetRepeatableRole
          points: 10
          groups:
          - RoleLoadoutBudgetRepeatableUnlimited
        """;

    [Test]
    public async Task DefaultsShareBudgetAndExistingSelectionsAreRecalculatedFirst()
    {
        await Server.WaitAssertion(() =>
        {
            var profile = new HumanoidCharacterProfile();

            var cumulative = new RoleLoadout("RoleLoadoutBudgetCumulative")
            {
                Points = 999,
            };
            cumulative.SetDefault(profile, null, SProtoMan);
            Assert.Multiple(() =>
            {
                Assert.That(cumulative.SelectedLoadouts["RoleLoadoutBudgetDefaultSecond"], Has.Count.EqualTo(1),
                    "the reverse group traversal may spend the shared budget on the second group first");
                Assert.That(cumulative.SelectedLoadouts["RoleLoadoutBudgetDefaultFirst"], Is.Empty,
                    "a later default must not reset the point budget and overspend it");
                Assert.That(cumulative.Points, Is.EqualTo(2));
            });

            var preserved = new RoleLoadout("RoleLoadoutBudgetPreserved")
            {
                Points = 999,
                SelectedLoadouts =
                {
                    ["RoleLoadoutBudgetExistingTwo"] =
                    [
                        new Loadout { Prototype = "RoleLoadoutBudgetCostTwo" },
                    ],
                },
            };
            preserved.SetDefault(profile, null, SProtoMan);
            Assert.Multiple(() =>
            {
                Assert.That(PrototypesIn(preserved, "RoleLoadoutBudgetExistingTwo"),
                    Is.EqualTo(new[] { "RoleLoadoutBudgetCostTwo" }));
                Assert.That(PrototypesIn(preserved, "RoleLoadoutBudgetDefaultFirst"),
                    Is.EqualTo(new[] { "RoleLoadoutBudgetCostThree" }),
                    "the exact remaining budget must remain available after preserving the existing selection");
                Assert.That(preserved.Points, Is.Zero);
            });

            var rejected = new RoleLoadout("RoleLoadoutBudgetRejected")
            {
                Points = 999,
                SelectedLoadouts =
                {
                    ["RoleLoadoutBudgetExistingFour"] =
                    [
                        new Loadout { Prototype = "RoleLoadoutBudgetCostFour" },
                    ],
                },
            };
            rejected.SetDefault(profile, null, SProtoMan);
            Assert.Multiple(() =>
            {
                Assert.That(PrototypesIn(rejected, "RoleLoadoutBudgetExistingFour"),
                    Is.EqualTo(new[] { "RoleLoadoutBudgetCostFour" }));
                Assert.That(rejected.SelectedLoadouts["RoleLoadoutBudgetDefaultFirst"], Is.Empty,
                    "an over-budget default must be rejected after existing selections are charged");
                Assert.That(rejected.Points, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task UnlimitedGroupKeepsDefaultsAndAddRemoveRecalculatesPoints()
    {
        await Server.WaitAssertion(() =>
        {
            var profile = new HumanoidCharacterProfile();
            var defaults = new RoleLoadout("RoleLoadoutBudgetUnlimitedRole");
            defaults.SetDefault(profile, null, SProtoMan);
            Assert.Multiple(() =>
            {
                Assert.That(PrototypesIn(defaults, "RoleLoadoutBudgetUnlimited"), Is.EqualTo(new[]
                {
                    "RoleLoadoutBudgetCostOne",
                    "RoleLoadoutBudgetCostTwo",
                    "RoleLoadoutBudgetCostThree",
                }));
                Assert.That(defaults.Points, Is.EqualTo(4));
            });

            var edited = new RoleLoadout("RoleLoadoutBudgetUnlimitedRole")
            {
                Points = 10,
                SelectedLoadouts =
                {
                    ["RoleLoadoutBudgetUnlimited"] = [],
                },
            };

            Assert.That(edited.AddLoadout(
                "RoleLoadoutBudgetUnlimited",
                "RoleLoadoutBudgetCostOne",
                SProtoMan), Is.True);
            Assert.That(edited.AddLoadout(
                "RoleLoadoutBudgetUnlimited",
                "RoleLoadoutBudgetCostTwo",
                SProtoMan), Is.True);
            Assert.That(edited.AddLoadout(
                "RoleLoadoutBudgetUnlimited",
                "RoleLoadoutBudgetCostThree",
                SProtoMan), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(edited.SelectedLoadouts["RoleLoadoutBudgetUnlimited"], Has.Count.EqualTo(3),
                    "MaxLimit zero must not evict earlier selections");
                Assert.That(edited.Points, Is.EqualTo(4));
            });

            Assert.That(edited.RemoveLoadout(
                "RoleLoadoutBudgetUnlimited",
                "RoleLoadoutBudgetCostTwo",
                SProtoMan), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(PrototypesIn(edited, "RoleLoadoutBudgetUnlimited"), Is.EqualTo(new[]
                {
                    "RoleLoadoutBudgetCostOne",
                    "RoleLoadoutBudgetCostThree",
                }));
                Assert.That(edited.Points, Is.EqualTo(6));
            });
        });
    }

    [Test]
    public async Task UnlimitedGroupValidatesRepeatableSelectionCapAndChargesEachCopy()
    {
        await Server.WaitAssertion(() =>
        {
            var loadout = new RoleLoadout("RoleLoadoutBudgetRepeatableRole")
            {
                Points = 999,
                SelectedLoadouts =
                {
                    ["RoleLoadoutBudgetRepeatableUnlimited"] =
                    [
                        new Loadout { Prototype = "RoleLoadoutBudgetRepeatable" },
                        new Loadout { Prototype = "RoleLoadoutBudgetRepeatable" },
                        new Loadout { Prototype = "RoleLoadoutBudgetRepeatable" },
                    ],
                },
            };

            loadout.EnsureValid(new HumanoidCharacterProfile(), null, Server.InstanceDependencyCollection);
            Assert.Multiple(() =>
            {
                Assert.That(loadout.SelectedLoadouts["RoleLoadoutBudgetRepeatableUnlimited"], Has.Count.EqualTo(2));
                Assert.That(loadout.Points, Is.EqualTo(4));
            });

            Assert.That(loadout.AddLoadout(
                "RoleLoadoutBudgetRepeatableUnlimited", "RoleLoadoutBudgetRepeatable", SProtoMan), Is.False);
            Assert.That(loadout.Points, Is.EqualTo(4));
            Assert.That(loadout.RemoveLoadout(
                "RoleLoadoutBudgetRepeatableUnlimited", "RoleLoadoutBudgetRepeatable", SProtoMan), Is.True);
            Assert.That(loadout.Points, Is.EqualTo(7));
            Assert.That(loadout.AddLoadout(
                "RoleLoadoutBudgetRepeatableUnlimited", "RoleLoadoutBudgetRepeatable", SProtoMan), Is.True);
            Assert.That(loadout.Points, Is.EqualTo(4));
        });
    }

    private static string[] PrototypesIn(RoleLoadout loadout, string group)
    {
        return loadout.SelectedLoadouts[group]
            .Select(selected => selected.Prototype.Id)
            .ToArray();
    }
}
