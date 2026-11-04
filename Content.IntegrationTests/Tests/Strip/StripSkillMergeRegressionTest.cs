#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.Strip;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Strip;
using Content.Shared.Strip.Components;
using Robust.Shared.GameObjects;
using DoAfterData = Content.Shared.DoAfter.DoAfter;

namespace Content.IntegrationTests.Tests.Strip;

[TestFixture]
[TestOf(typeof(SharedStrippableSystem))]
public sealed class StripSkillMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: StripMergeProtectedHead
          components:
          - type: Item
          - type: Clothing
            slots: [head]
          - type: RMCUnstrippable
            policeCanStrip: true

        - type: entity
          id: StripMergeHead
          components:
          - type: Item
          - type: Clothing
            slots: [head]

        - type: entity
          id: StripMergeMask
          components:
          - type: Item
          - type: Clothing
            slots: [mask]

        - type: entity
          id: StripMergeHandItem
          components:
          - type: Item
          - type: RMCUnstrippable
            policeCanStrip: true
        """;

    [Test]
    public async Task WornProtectionNeedsPoliceOneButHeldProtectionRemainsRemovable()
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        EntityUid target = default;
        EntityUid protectedHead = default;
        EntityUid handItem = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
                target = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
                protectedHead = SEntMan.SpawnEntity("StripMergeProtectedHead", map.GridCoords);
                handItem = SEntMan.SpawnEntity("StripMergeHandItem", map.GridCoords);

                var inventory = Server.System<InventorySystem>();
                Assert.That(inventory.TryEquip(target, protectedHead, "head", force: true), Is.True);

                SetPoliceSkill(user, 0);
                StartInventoryRemoval(user, target, protectedHead, "head");
                Assert.That(Running(user), Is.Empty,
                    "level zero must not bypass RMCUnstrippable on a worn inventory item");

                SetPoliceSkill(user, 1);
                StartInventoryRemoval(user, target, protectedHead, "head");
                var allowed = Running(user).Single();
                Assert.Multiple(() =>
                {
                    Assert.That(allowed.Args.DuplicateCondition, Is.EqualTo(DuplicateConditions.SameEvent));
                    Assert.That(allowed.Args.ForceVisible, Is.True);
                    Assert.That(allowed.Args.BreakOnHandChange, Is.False);
                    Assert.That(allowed.Args.NeedHand, Is.True);
                });

                SetPoliceSkill(user, 0);
            });
            await Server.WaitRunTicks(2);

            await Server.WaitAssertion(() =>
            {
                Assert.That(Running(user), Is.Empty,
                    "the every-tick terminal validation must cancel after police skill is lost");

                var hands = Server.System<SharedHandsSystem>();
                var targetHands = SEntMan.GetComponent<HandsComponent>(target);
                var handId = targetHands.Hands.Keys.First();
                Assert.That(hands.TryPickup(target, handItem, handId, checkActionBlocker: false), Is.True);

                StartHandRemoval(user, target, handItem, handId);
                var heldRemoval = Running(user).Single();
                Assert.Multiple(() =>
                {
                    Assert.That(heldRemoval.Args.DuplicateCondition, Is.EqualTo(DuplicateConditions.SameEvent));
                    Assert.That(heldRemoval.Args.ForceVisible, Is.True);
                    Assert.That(heldRemoval.Args.Used, Is.EqualTo(handItem));
                });

                Assert.That(hands.TryDrop(target, handItem, checkActionBlocker: false), Is.True);
            });
            await Server.WaitRunTicks(2);

            await Server.WaitAssertion(() =>
            {
                Assert.That(Running(user), Is.Empty,
                    "hand-slot mutation must still cancel the historically permitted hand removal");
            });
        }
        finally
        {
            await Delete(user, target, protectedHead, handItem);
        }
    }

    [Test]
    public async Task PoliceTwoUsesSameToolAndCanStripDistinctSlotsInParallel()
    {
        var map = await Pair.CreateTestMap();
        EntityUid lowUser = default;
        EntityUid lowTarget = default;
        EntityUid lowHead = default;
        EntityUid lowMask = default;
        EntityUid highUser = default;
        EntityUid highTarget = default;
        EntityUid highHead = default;
        EntityUid highMask = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var inventory = Server.System<InventorySystem>();
                lowUser = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
                lowTarget = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
                lowHead = SEntMan.SpawnEntity("StripMergeHead", map.GridCoords);
                lowMask = SEntMan.SpawnEntity("StripMergeMask", map.GridCoords);
                Assert.That(inventory.TryEquip(lowTarget, lowHead, "head", force: true), Is.True);
                Assert.That(inventory.TryEquip(lowTarget, lowMask, "mask", force: true), Is.True);
                SetPoliceSkill(lowUser, 1);

                StartInventoryRemoval(lowUser, lowTarget, lowHead, "head");
                var first = Running(lowUser).Single();
                StartInventoryRemoval(lowUser, lowTarget, lowMask, "mask");
                Assert.Multiple(() =>
                {
                    Assert.That(first.Cancelled, Is.True,
                        "SameEvent must cancel the lower-skill user's first distinct removal");
                    Assert.That(Running(lowUser), Is.Empty,
                        "cancel-existing/block-new prevents lower-skill parallel stripping");
                });

                highUser = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
                highTarget = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
                highHead = SEntMan.SpawnEntity("StripMergeHead", map.GridCoords);
                highMask = SEntMan.SpawnEntity("StripMergeMask", map.GridCoords);
                Assert.That(inventory.TryEquip(highTarget, highHead, "head", force: true), Is.True);
                Assert.That(inventory.TryEquip(highTarget, highMask, "mask", force: true), Is.True);
                SetPoliceSkill(highUser, 2);

                StartInventoryRemoval(highUser, highTarget, highHead, "head");
                StartInventoryRemoval(highUser, highTarget, highMask, "mask");
                var parallel = Running(highUser);
                Assert.Multiple(() =>
                {
                    Assert.That(parallel, Has.Count.EqualTo(2));
                    Assert.That(parallel.Select(doAfter => doAfter.Args.DuplicateCondition),
                        Is.All.EqualTo(DuplicateConditions.SameTool));
                    Assert.That(parallel.Select(doAfter => doAfter.Args.ForceVisible), Is.All.True);
                    Assert.That(parallel.Select(doAfter => doAfter.Args.BreakOnHandChange), Is.All.False);
                    Assert.That(parallel.Select(doAfter => doAfter.Args.Used),
                        Is.EquivalentTo(new EntityUid?[] { highHead, highMask }));
                });

                Assert.That(inventory.TryUnequip(highTarget, "mask", force: true), Is.True);
            });
            await Server.WaitRunTicks(2);

            await Server.WaitAssertion(() =>
            {
                var running = Running(highUser);
                Assert.Multiple(() =>
                {
                    Assert.That(running, Has.Count.EqualTo(1),
                        "terminal slot validation cancels only the removal whose item left its slot");
                    Assert.That(running.Single().Args.Used, Is.EqualTo(highHead));
                });
            });
        }
        finally
        {
            await Delete(lowUser, lowTarget, lowHead, lowMask, highUser, highTarget, highHead, highMask);
        }
    }

    private void SetPoliceSkill(EntityUid user, int level)
    {
        var skills = SEntMan.EnsureComponent<SkillsComponent>(user);
        skills.Preset = null;
        skills.Skills["RMCSkillPolice"] = level;
        SEntMan.Dirty(user, skills);
    }

    private void StartInventoryRemoval(EntityUid user, EntityUid target, EntityUid item, string slot)
    {
        typeof(SharedStrippableSystem)
            .GetMethod("StartStripRemoveInventory", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(Server.System<StrippableSystem>(), new object[] { user, target, item, slot });
    }

    private void StartHandRemoval(EntityUid user, EntityUid target, EntityUid item, string handId)
    {
        Entity<HandsComponent?> userHands = (user, SEntMan.GetComponent<HandsComponent>(user));
        Entity<HandsComponent?> targetHands = (target, SEntMan.GetComponent<HandsComponent>(target));
        var strippable = SEntMan.GetComponent<StrippableComponent>(target);
        typeof(SharedStrippableSystem)
            .GetMethod("StartStripRemoveHand", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(Server.System<StrippableSystem>(), new object?[]
            {
                userHands,
                targetHands,
                item,
                handId,
                strippable
            });
    }

    private List<DoAfterData> Running(EntityUid user)
    {
        if (!SEntMan.TryGetComponent(user, out DoAfterComponent? component))
            return new List<DoAfterData>();

        return component.DoAfters.Values
            .Where(doAfter => !doAfter.Cancelled && !doAfter.Completed)
            .OrderBy(doAfter => doAfter.Index)
            .ToList();
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
