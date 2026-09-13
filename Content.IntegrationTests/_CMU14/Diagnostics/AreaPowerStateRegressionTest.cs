using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Power;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class AreaPowerStateRegressionTest
{
    private static readonly string[] MemberFields =
    [
        nameof(RMCAreaPowerComponent.Apcs), nameof(RMCAreaPowerComponent.EquipmentReceivers),
        nameof(RMCAreaPowerComponent.LightingReceivers), nameof(RMCAreaPowerComponent.EnvironmentReceivers),
    ];

    [Test]
    public async Task RemovingDetachedReceiverReplicatesReleasedLoad()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var map = await pair.CreateTestMap();
        EntityUid receiver = default;
        NetEntity areaNet = default;
        await pair.Server.WaitPost(() =>
        {
            var entities = pair.Server.EntMan;
            var grid = entities.EnsureComponent<AreaGridComponent>(map.Grid.Owner);
            entities.System<AreaSystem>().ReplaceArea(grid, Vector2i.Zero, "RMCAreaSpace");
            receiver = entities.SpawnEntity(null, new EntityCoordinates(map.Grid.Owner, Vector2i.Zero));
            var component = entities.AddComponent<RMCPowerReceiverComponent>(receiver);
            SetField(component, nameof(component.Mode), RMCPowerMode.Idle);
            SetField(component, nameof(component.IdleLoad), 17);
        });
        await pair.RunTicksSync(2);
        await pair.Server.WaitPost(() =>
        {
            var entities = pair.Server.EntMan;
            areaNet = entities.GetNetEntity(entities.GetComponent<RMCPowerReceiverComponent>(receiver).Area!.Value);
        });
        await pair.RunUntilSynced();
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.EntMan;
            var area = entities.GetComponent<RMCAreaPowerComponent>(entities.GetEntity(areaNet));
            Assert.That(area.Load[(int) RMCPowerChannel.Equipment], Is.GreaterThan(0));
        });
        await pair.Server.WaitPost(() =>
        {
            var entities = pair.Server.EntMan;
            entities.System<SharedTransformSystem>().DetachEntity(receiver);
            entities.DeleteEntity(receiver);
        });
        await pair.RunUntilSynced();
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.EntMan;
            var area = entities.GetComponent<RMCAreaPowerComponent>(entities.GetEntity(areaNet));
            Assert.That(area.Load[(int) RMCPowerChannel.Equipment], Is.Zero);
            Assert.That(area.EquipmentReceivers, Is.Empty);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AreaStateStillReachesClientAfterMemberDeletion()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var map = await pair.CreateTestMap();
        EntityUid owner = default;
        EntityUid deleted = default;
        NetEntity ownerNet = default;
        NetEntity liveNet = default;
        NetEntity deletedNet = default;
        await pair.Server.WaitPost(() =>
        {
            var entities = pair.Server.EntMan;
            owner = entities.SpawnEntity(null, map.GridCoords);
            var live = entities.SpawnEntity(null, map.GridCoords);
            deleted = entities.SpawnEntity(null, map.GridCoords);
            ownerNet = entities.GetNetEntity(owner);
            liveNet = entities.GetNetEntity(live);
            deletedNet = entities.GetNetEntity(deleted);
            var component = entities.AddComponent<RMCAreaPowerComponent>(owner);
            foreach (var field in MemberFields)
                SetField(component, field, new HashSet<EntityUid> { live, deleted });
            SetField(component, nameof(component.Load), new[] { 17, 29, 41 });
            entities.Dirty(owner, component);
        });

        async Task AssertClientMembers(bool afterDeletion)
        {
            await pair.RunUntilSynced();
            await pair.Client.WaitAssertion(() =>
            {
                var entities = pair.Client.EntMan;
                var component = entities.GetComponent<RMCAreaPowerComponent>(entities.GetEntity(ownerNet));
                var live = entities.GetEntity(liveNet);
                var expected = afterDeletion ? new[] { live } : new[] { live, entities.GetEntity(deletedNet) };
                foreach (var field in MemberFields)
                    Assert.That(typeof(RMCAreaPowerComponent).GetField(field)!.GetValue(component), Is.EquivalentTo(expected));
                Assert.That(component.Load, Is.EqualTo(new[] { 17, 29, 41 }));
            });
        }

        await AssertClientMembers(false);
        await pair.Server.WaitPost(() =>
        {
            var entities = pair.Server.EntMan;
            entities.DeleteEntity(deleted);
            entities.Dirty(owner, entities.GetComponent<RMCAreaPowerComponent>(owner));
        });
        await AssertClientMembers(true);
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AreaStateKeepsLiveMembersWhenAnOldMemberWasDeleted()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var owner = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var live = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var deleted = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var liveNet = entities.GetNetEntity(live);
            var deletedNet = entities.GetNetEntity(deleted);
            var component = entities.AddComponent<RMCAreaPowerComponent>(owner);
            foreach (var field in MemberFields)
                SetField(component, field, new HashSet<EntityUid> { live, deleted });
            SetField(component, nameof(component.Load), new[] { 17, 29, 41 });

            var original = entities.GetComponentState(entities.EventBus, component, null, GameTick.Zero)!;
            foreach (var field in MemberFields)
                Assert.That(ReadState(original, field), Is.EquivalentTo(new[] { liveNet, deletedNet }));

            entities.DeleteEntity(deleted);
            var state = entities.GetComponentState(entities.EventBus, component, null, GameTick.Zero)!;
            foreach (var field in MemberFields)
            {
                Assert.That(ReadState(state, field), Is.EquivalentTo(new[] { liveNet }));
                Assert.That(ReadState(original, field), Is.EquivalentTo(new[] { liveNet, deletedNet }),
                    "Building a new state must not mutate a previously captured state.");
                Assert.That((HashSet<EntityUid>) typeof(RMCAreaPowerComponent).GetField(field)!.GetValue(component)!,
                    Does.Contain(deleted), "State serialization must not mutate gameplay collections on PVS worker threads.");
            }
            Assert.That(ReadState(state, nameof(component.Load)), Is.EqualTo(new[] { 17, 29, 41 }));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ReceiverRemovalUsesRegisteredAreaAndReleasesLoad(
        [Values] RMCPowerChannel channel, [Values(false, true)] bool detachFirst)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var map = await pair.CreateTestMap();
        EntityUid receiver = default;
        await pair.Server.WaitPost(() =>
        {
            var entities = pair.Server.EntMan;
            var grid = entities.EnsureComponent<AreaGridComponent>(map.Grid.Owner);
            entities.System<AreaSystem>().ReplaceArea(grid, Vector2i.Zero, "RMCAreaSpace");
            receiver = entities.SpawnEntity(null, new EntityCoordinates(map.Grid.Owner, Vector2i.Zero));
            var component = entities.AddComponent<RMCPowerReceiverComponent>(receiver);
            SetField(component, nameof(component.Channel), channel);
            SetField(component, nameof(component.Mode), RMCPowerMode.Idle);
            SetField(component, nameof(component.IdleLoad), 17);
        });
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var component = entities.GetComponent<RMCPowerReceiverComponent>(receiver);
            Assert.That(component.Area, Is.Not.Null, "The real power update must register this receiver first.");
            var area = entities.GetComponent<RMCAreaPowerComponent>(component.Area!.Value);
            Assert.That(area.Load[(int) channel], Is.GreaterThan(0));
            if (detachFirst)
                entities.System<SharedTransformSystem>().DetachEntity(receiver);
            entities.DeleteEntity(receiver);
            var field = channel switch
            {
                RMCPowerChannel.Equipment => nameof(area.EquipmentReceivers),
                RMCPowerChannel.Lighting => nameof(area.LightingReceivers),
                _ => nameof(area.EnvironmentReceivers),
            };
            Assert.Multiple(() =>
            {
                Assert.That((HashSet<EntityUid>) typeof(RMCAreaPowerComponent).GetField(field)!.GetValue(area)!,
                    Does.Not.Contain(receiver), "Deletion must remove the registered member even after detaching it.");
                Assert.That(area.Load[(int) channel], Is.Zero, "Termination and component removal must release its load exactly once.");
            });
            entities.GetComponentState(entities.EventBus, area, null, GameTick.Zero);
        });
        await pair.CleanReturnAsync();
    }

    private static object ReadState(object state, string field) =>
        state.GetType().GetField(field)?.GetValue(state) ?? state.GetType().GetProperty(field)!.GetValue(state);

    private static void SetField<T>(T component, string field, object value) =>
        typeof(T).GetField(field)!.SetValue(component, value);
}
