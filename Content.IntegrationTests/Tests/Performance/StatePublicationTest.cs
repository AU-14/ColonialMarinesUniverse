#pragma warning disable RA0002 // Controlled component setup exercises replication and invalidation.
using System.Collections;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._RMC14.Sentry.Laptop;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Explosion.Components;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Performance;

[TestFixture]
public sealed class StatePublicationTest : GameTest
{
    [SetUp]
    public void ReportFailure() => PreFinalizeHook += () => TestContext.Out.WriteLine(TestContext.CurrentContext.Result.Message);

    private IComponentState State(Component comp, GameTick from = default) =>
        SEntMan.GetComponentState(SEntMan.EventBus, comp, ServerSession!, from)!;

    [Test]
    public async Task ExplosionSnapshotsAreSharedAcrossRecipientsAndRefreshedOnGridDeletion()
    {
        var map = await Pair.CreateTestMap();
        EntityUid visual = default;
        ExplosionVisualsComponent comp = null;
        ExplosionVisualsState first = null;
        EntityUid grid = default;
        NetEntity netGrid = default;
        await Server.WaitAssertion(() =>
        {
            grid = Server.System<SharedMapSystem>().CreateGridEntity(map.MapUid).Owner;
            netGrid = SEntMan.GetNetEntity(grid);
            visual = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            comp = SEntMan.AddComponent<ExplosionVisualsComponent>(visual);
            comp.Tiles[grid] = new() { [0] = [Vector2i.Zero] };
            comp.Intensity = [1f];
            Server.System<ExplosionSystem>().PublishVisualState((visual, comp));
            first = (ExplosionVisualsState)State(comp);
            var states = new IComponentState[32];
            Parallel.For(0, states.Length, i => states[i] = State(comp));
            Assert.That(states.All(state => ReferenceEquals(first, state)), Is.True);
            Assert.That(first.Tiles.Keys, Does.Contain(netGrid));
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            var before = comp.LastModifiedTick;
            SEntMan.DeleteEntity(grid);
            var after = (ExplosionVisualsState)State(comp);
            Assert.That(after, Is.Not.SameAs(first));
            Assert.That(after.Tiles, Is.Empty);
            Assert.That(first.Tiles.Keys, Does.Contain(netGrid), "Already published states must retain their grid keys.");
            Assert.That(comp.LastModifiedTick, Is.GreaterThan(before));
            Assert.That(State(comp), Is.SameAs(after));
            // Re-publication must work even when two changes occur in the same tick.
            comp.Tiles[map.Grid.Owner] = new() { [0] = [Vector2i.One] };
            Server.System<ExplosionSystem>().PublishVisualState((visual, comp));
            Assert.That(State(comp), Is.Not.SameAs(after));
            Assert.That(after.Tiles, Is.Empty);
            SEntMan.RemoveComponent<ExplosionVisualsComponent>(visual);
            comp = SEntMan.AddComponent<ExplosionVisualsComponent>(visual);
            Assert.That(((ExplosionVisualsState)State(comp)).Tiles, Is.Empty, "Removed components must release their snapshots.");
            SEntMan.DeleteEntity(visual);
        });
    }

    [Test]
    public async Task SentryStatesAreIndependentAndFilterDeletedReferences()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var laptop = SEntMan.SpawnEntity(null, map.GridCoords);
            var sentry = SEntMan.SpawnEntity(null, map.GridCoords);
            var stale = SEntMan.SpawnEntity(null, map.GridCoords);
            var comp = SEntMan.AddComponent<SentryLaptopComponent>(laptop);
            comp.LinkedSentries.UnionWith([sentry, stale]);
            comp.SentryCustomNames[sentry] = "north";
            comp.SentryCustomNames[stale] = "deleted";
            comp.Watchers.Add(sentry);
            comp.Watchers.Add(stale);
            SEntMan.DeleteEntity(stale);
            var first = (SentryLaptopComponentState)State(comp);
            var second = (SentryLaptopComponentState)State(comp);
            Assert.That(first.LinkedSentries, Is.EquivalentTo(new[] { SEntMan.GetNetEntity(sentry) }));
            Assert.That(first.SentryCustomNames.Values, Is.EquivalentTo(new[] { "north" }));
            Assert.That(first.Watchers, Is.EquivalentTo(first.LinkedSentries));
            first.LinkedSentries.Clear();
            first.SentryCustomNames.Clear();
            first.Watchers.Clear();
            Assert.That(second.LinkedSentries, Has.Count.EqualTo(1));
            Assert.That(second.SentryCustomNames, Has.Count.EqualTo(1));
            Assert.That(second.Watchers, Has.Count.EqualTo(1));
            Assert.That(comp.LinkedSentries, Does.Contain(stale), "State callbacks must not repair gameplay collections.");
            SEntMan.DeleteEntity(laptop);
            SEntMan.DeleteEntity(sentry);
        });
    }

    private static IDictionary Field(IComponentState state, string field) =>
        (IDictionary)state.GetType().GetField(field)!.GetValue(state)!;

    [Test]
    public async Task AtmosFullResetIsNotConsumedByFirstRecipient()
    {
        var map = await Pair.CreateTestMap();
        EntityUid console = default;
        AtmosMonitoringConsoleComponent comp = null;
        await Server.WaitPost(() =>
        {
            console = SEntMan.SpawnEntity(null, map.GridCoords);
            comp = SEntMan.AddComponent<AtmosMonitoringConsoleComponent>(console);
        });
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            comp.ForceFullUpdateTick = SGameTiming.CurTick;
            var from = SGameTiming.CurTick;
            var states = new IComponentState[32];
            Parallel.For(0, states.Length, i => states[i] = State(comp, from));
            Assert.That(states.All(s => s is not IComponentDeltaState), Is.True);
            Assert.That(State(comp, from - 1), Is.Not.InstanceOf<IComponentDeltaState>());
            Assert.That(State(comp, from + 1), Is.InstanceOf<IComponentDeltaState>());
            SEntMan.DeleteEntity(console);
        });
    }

    [Test]
    public async Task AtmosDeltaAddsRemovesAndCopiesChunksWithoutClearingAliasedDevices()
    {
        var map = await Pair.CreateTestMap();
        EntityUid console = default;
        AtmosMonitoringConsoleComponent comp = null;
        IComponentState initial = null;
        await Server.WaitPost(() =>
        {
            console = SEntMan.SpawnEntity(null, map.GridCoords);
            comp = SEntMan.AddComponent<AtmosMonitoringConsoleComponent>(console);
            // Own the dictionaries instead of changing the grid's shared data.
            comp.AtmosPipeChunks = new()
            {
                [Vector2i.Zero] = new(Vector2i.Zero),
                [Vector2i.One] = new(Vector2i.One),
            };
            comp.AtmosDevices = new() { [SEntMan.GetNetEntity(console)] = default };
            initial = State(comp);
        });
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            var added = new Vector2i(2, 2);
            comp.AtmosPipeChunks.Remove(Vector2i.Zero);
            comp.AtmosPipeChunks[added] = new(added) { LastUpdate = SGameTiming.CurTick };
            var delta = (IComponentDeltaState)State(comp, SGameTiming.CurTick);
            var copy = delta.CreateNewFullState(initial);
            Assert.That(Field(copy, "Chunks").Keys, Is.EquivalentTo(new[] { Vector2i.One, added }));
            Assert.That(Field(initial, "Chunks").Keys, Is.EquivalentTo(new[] { Vector2i.Zero, Vector2i.One }));
            Assert.That(Field(copy, "Chunks")[Vector2i.One], Is.Not.SameAs(Field(initial, "Chunks")[Vector2i.One]));
            Assert.That(Field(copy, "AtmosDevices"), Is.Not.SameAs(comp.AtmosDevices));
            delta.ApplyToFullState(initial);
            Assert.That(Field(initial, "Chunks").Keys, Is.EquivalentTo(Field(copy, "Chunks").Keys));
            Assert.That(Field(initial, "AtmosDevices"), Has.Count.EqualTo(1));
            Assert.That(comp.AtmosDevices, Has.Count.EqualTo(1), "The full state and delta originally share this dictionary.");
            SEntMan.DeleteEntity(console);
        });
    }

    [Test]
    public async Task AtmosConsoleLeavingGridClearsItsStateWithoutChangingOtherConsoles()
    {
        var map = await Pair.CreateTestMap();
        EntityUid console = default;
        AtmosMonitoringConsoleComponent comp = null;
        Dictionary<Vector2i, AtmosPipeChunk> shared = null;
        await Server.WaitPost(() =>
        {
            console = SEntMan.SpawnEntity(null, map.GridCoords);
            comp = SEntMan.AddComponent<AtmosMonitoringConsoleComponent>(console);
            shared = new() { [Vector2i.Zero] = new(Vector2i.Zero) };
            comp.AtmosPipeChunks = shared;
        });
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            var before = comp.LastModifiedTick;
            Server.System<SharedTransformSystem>().SetCoordinates(console, new EntityCoordinates(map.MapUid, new Vector2(1000)));
            Assert.That(comp.AtmosPipeChunks, Is.Empty);
            Assert.That(shared, Has.Count.EqualTo(1));
            Assert.That(comp.LastModifiedTick, Is.GreaterThan(before));
            Assert.That(State(comp, SGameTiming.CurTick), Is.Not.InstanceOf<IComponentDeltaState>());
            SEntMan.DeleteEntity(console);
        });
    }

    [TestCase("cooldown")]
    [TestCase("removeCooldown")]
    [TestCase("reduceDelay")]
    [TestCase("nullDelay")]
    [TestCase("inventory")]
    public async Task UnchangedActionsAndInventoryDoNotDirtyButRealChangesDo(string operation)
    {
        var map = await Pair.CreateTestMap();
        EntityUid uid = default;
        ActionComponent action = null;
        InventoryComponent inventory = null;
        var actions = Server.System<SharedActionsSystem>();
        await Server.WaitPost(() =>
        {
            uid = SEntMan.SpawnEntity(null, map.GridCoords);
            action = SEntMan.AddComponent<ActionComponent>(uid);
            inventory = SEntMan.AddComponent<InventoryComponent>(uid);
            action.UseDelay = operation == "nullDelay" ? null : TimeSpan.FromSeconds(5);
            if (operation == "cooldown")
                actions.SetCooldown(uid, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        });
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            var oldAction = action.LastModifiedTick;
            var oldInventory = inventory.LastModifiedTick;
            switch (operation)
            {
                case "cooldown": actions.SetCooldown(uid, TimeSpan.Zero, TimeSpan.FromSeconds(5)); break;
                case "removeCooldown": actions.RemoveCooldown(uid); break;
                case "reduceDelay": actions.ReduceUseDelay(uid, TimeSpan.Zero); break;
                case "nullDelay": actions.ReduceUseDelay(uid, TimeSpan.FromSeconds(1)); break;
                case "inventory": Server.System<InventorySystem>().SetTemplateId((uid, inventory), inventory.TemplateId); break;
            }
            Assert.That(action.LastModifiedTick, Is.EqualTo(oldAction));
            Assert.That(inventory.LastModifiedTick, Is.EqualTo(oldInventory));
            actions.SetCooldown(uid, TimeSpan.Zero, TimeSpan.FromSeconds(10));
            Assert.That(action.LastModifiedTick, Is.GreaterThan(oldAction));
            Assert.That(action.Cooldown!.Value.End, Is.EqualTo(TimeSpan.FromSeconds(10)));
            actions.ReduceUseDelay(uid, TimeSpan.FromSeconds(6));
            Assert.That(action.UseDelay, Is.Null, "Reducing below zero must still clear the delay.");
            SEntMan.DeleteEntity(uid);
        });
    }
}
