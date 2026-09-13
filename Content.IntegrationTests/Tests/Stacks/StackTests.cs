using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Stack;
using Content.Shared._RMC14.Xenonids.Acid;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using static Content.IntegrationTests.Tests.Stacks.StackTestPrototypes;

namespace Content.IntegrationTests.Tests.Stacks;

[TestFixture]
[TestOf(typeof(StackSystem))]
public sealed class StackTest : GameTest
{
    [SidedDependency(Side.Server)] private readonly StackSystem _sStackSystem = default!;
    [SidedDependency(Side.Server)] private readonly StackMergedProbeSystem _mergedProbe = default!;

    [Test]
    [Description("Tests for SharedStackSystem.SetCount.")]
    public async Task SetTest()
    {
        Assume.That(SEntMan.EntityCount, Is.Zero, "Unexpected entities at the start of the test.");

        var stack = await Spawn(StackEnt1);

        // Raising the count
        await Server.WaitPost(() => _sStackSystem.SetCount((stack, null), 2));
        Assert.That(_sStackSystem.GetCount(stack), Is.EqualTo(2));

        // Lowering the count
        await Server.WaitPost(() => _sStackSystem.SetCount((stack, null), 1));
        Assert.That(_sStackSystem.GetCount(stack), Is.EqualTo(1));

        // Setting above the max count clamps to max
        await Server.WaitPost(() => _sStackSystem.SetCount((stack, null), 31));
        Assert.That(_sStackSystem.GetCount(stack), Is.EqualTo(30));

        // Setting to 0 deletes the stack
        Server.Post(() => _sStackSystem.SetCount((stack, null), 0));
        await Server.WaitRunTicks(1);
        Assert.That(SEntMan.EntityCount, Is.Zero);
    }

    [Test]
    [Description("Tests that SharedStackSystem.MergeStacks functions as expected with small numbers.")]
    public async Task MergeTest()
    {
        Assume.That(SEntMan.EntityCount, Is.Zero, "Unexpected entities at the start of the test.");

        var stacks = new HashSet<EntityUid>();

        await Server.WaitPost(() =>
        {
            stacks =
            [
                SSpawn(StackEnt1),
                SSpawn(StackEnt2),
            ];

            _sStackSystem.MergeStacks(ref stacks);

            // Need to wait for the queue deletion of the empty stacks
            Server.RunTicks(1);
        });

        using (Assert.EnterMultipleScope())
        {
            // Assert that only one entity was returned
            // And that it has the correct count
            Assert.That(stacks, Has.Count.EqualTo(1));
            Assert.That(_sStackSystem.GetCount(stacks.First()), Is.EqualTo(3));

            // Assert that the other stack was set to zero and deleted
            Assert.That(SEntMan.EntityCount, Is.EqualTo(1));
        }
    }

    [Test]
    [Description("Tests that SharedStackSystem.MergeStacks functions as expected with large numbers.")]
    public async Task MergeOverflowTest()
    {
        Assume.That(SEntMan.EntityCount, Is.Zero, "Unexpected entities at the start of the test.");

        var stacks = new HashSet<EntityUid>();

        await Server.WaitPost(() =>
        {
            stacks =
            [
                SSpawn(StackEnt1),
                SSpawn(StackEnt2),
                SSpawn(StackEnt30),
            ];

            _sStackSystem.MergeStacks(ref stacks);

            // Wait for the queue deletion of the empty stacks
            Server.RunTicks(1);
        });

        var count = 0;
        await Server.WaitPost(() =>
        {
            foreach (var stack in stacks)
            {
                count += _sStackSystem.GetCount(stack);
            }
        });

        using (Assert.EnterMultipleScope())
        {
            // Assert that both stacks were returned
            // And that the empty stack was deleted
            Assert.That(stacks, Has.Count.EqualTo(2));
            Assert.That(SEntMan.EntityCount, Is.EqualTo(2));

            // Assert we have the same count as what we spawned
            Assert.That(count, Is.EqualTo(33));
        }
    }

    [Test]
    [Description("Test for SharedStackSystem.TryMergeToContacts.")]
    public async Task MergeContactsTest()
    {
        var map = await Pair.CreateTestMap();

        // Spawn two stacks at the same position so they're contacting
        EntityUid donor = EntityUid.Invalid;
        EntityUid receiver = EntityUid.Invalid;

        await Server.WaitPost(() =>
        {
            donor = SSpawnAtPosition(StackEnt1, map.GridCoords);
            receiver = SSpawnAtPosition(StackEnt1, map.GridCoords);

            _sStackSystem.TryMergeToContacts(donor);

            // Wait for queue deletion
            Server.RunTicks(1);
        });

        using (Assert.EnterMultipleScope())
        {
            // Assert that the receiver has the total count
            // And that the donor was deleted
            Assert.That(_sStackSystem.GetCount(receiver), Is.EqualTo(2));
            Assert.That(SEntMan.EntityExists(donor), Is.False);
        }

        // Now test for when there's more count than the receiver can hold
        await Server.WaitPost(() =>
        {
            donor = SSpawnAtPosition(StackEnt30, map.GridCoords);
            _sStackSystem.TryMergeToContacts(donor);
        });

        using (Assert.EnterMultipleScope())
        {
            // Assert that the receiver is at its maximum count
            // And that the donor has the remainder of the spawned count
            Assert.That(_sStackSystem.GetCount(receiver), Is.EqualTo(30));
            Assert.That(_sStackSystem.GetCount(donor), Is.EqualTo(2));
        }
    }

    [Test]
    [Description("Acided stacks cannot merge or split.")]
    public async Task AcidedStacksCannotMergeOrSplitTest()
    {
        Assume.That(SEntMan.EntityCount, Is.Zero, "Entities remain from an earlier test.");

        var donor = await Spawn(StackEnt2);
        var recipient = await Spawn(StackEnt1);
        var merged = true;
        EntityUid? split = EntityUid.Invalid;

        await Server.WaitPost(() =>
        {
            SEntMan.EnsureComponent<DamageableCorrodingComponent>(donor);
            merged = _sStackSystem.TryMergeStacks((donor, null), (recipient, null), out _);
            split = _sStackSystem.Split((donor, null), 1, new EntityCoordinates(donor, default));
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(merged, Is.False);
            Assert.That(split, Is.Null);
            Assert.That(_sStackSystem.GetCount(donor), Is.EqualTo(2));
            Assert.That(_sStackSystem.GetCount(recipient), Is.EqualTo(1));
        }
    }

    [Test]
    [Description("The RMC merged event runs after both stack counts change.")]
    public async Task StackMergedEventOrderingTest()
    {
        Assume.That(SEntMan.EntityCount, Is.Zero, "Entities remain from an earlier test.");

        var donor = await Spawn(StackEnt2);
        var recipient = await Spawn(StackEnt1);

        await Server.WaitPost(() =>
        {
            _mergedProbe.Reset();
            Assert.That(_sStackSystem.TryMergeStacks((donor, null), (recipient, null), out var transferred), Is.True);
            Assert.That(transferred, Is.EqualTo(2));
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_mergedProbe.Events, Is.EqualTo(1));
            Assert.That(_mergedProbe.Donor, Is.EqualTo(donor));
            Assert.That(_mergedProbe.Recipient, Is.EqualTo(recipient));
            Assert.That(_mergedProbe.Transferred, Is.EqualTo(2));
            Assert.That(_mergedProbe.DonorCountAtEvent, Is.Zero);
            Assert.That(_mergedProbe.RecipientCountAtEvent, Is.EqualTo(3));
        }
    }
}

public sealed partial class StackMergedProbeSystem : EntitySystem
{
    public int Events { get; private set; }
    public EntityUid Donor { get; private set; }
    public EntityUid Recipient { get; private set; }
    public int Transferred { get; private set; }
    public int DonorCountAtEvent { get; private set; }
    public int RecipientCountAtEvent { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StackComponent, StackMergedEvent>(OnStackMerged);
    }

    public void Reset()
    {
        Events = 0;
        Donor = default;
        Recipient = default;
        Transferred = 0;
        DonorCountAtEvent = 0;
        RecipientCountAtEvent = 0;
    }

    private void OnStackMerged(Entity<StackComponent> ent, ref StackMergedEvent args)
    {
        Events++;
        Donor = args.Donor;
        Recipient = args.Recipient;
        Transferred = args.Transferred;
        DonorCountAtEvent = TryComp(args.Donor, out StackComponent? donor) ? donor.Count : -1;
        RecipientCountAtEvent = ent.Comp.Count;
    }
}
