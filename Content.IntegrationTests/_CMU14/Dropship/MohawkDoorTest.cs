using System.Linq;
using System.Numerics;
using Content.Server.Wires;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.Prying.Components;
using Content.Shared.Prying.Systems;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Content.Shared.Wires;
using Robust.Client.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class MohawkDoorTest
{
    [TestCase("omaha")]
    [TestCase("midway")]
    [TestCase("omaha_navy")]
    [TestCase("midway_navy")]
    public async Task XenosPryPoweredHatchesAndWeldingBlocksPrying(string variant)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true, Connected = true });
        EntityUid ship = default;
        var doors = new List<(EntityUid Door, EntityUid Xeno, NetEntity Net)>();
        double delay = 0;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            entities.System<SharedMapSystem>().CreateMap(out var mapId);
            Assert.That(entities.System<MapLoaderSystem>().TryLoadGrid(mapId,
                new ResPath($"/Maps/CMU14/ShuttlesDropships/Mohawk/{variant}.yml"), out var loaded), Is.True);
            ship = loaded!.Value.Owner;
            foreach (var door in entities.EntityQuery<DoorComponent>()
                         .Where(d => entities.GetComponent<TransformComponent>(d.Owner).GridUid == ship))
            {
                Assert.That(entities.HasComponent<WeldableComponent>(door.Owner), Is.True);
                Assert.That(entities.HasComponent<WiresComponent>(door.Owner), Is.True);
                Assert.That(entities.HasComponent<WiresPanelComponent>(door.Owner), Is.True);
                Assert.That(door.ChangeAirtight, Is.True);
                var position = entities.GetComponent<TransformComponent>(door.Owner).LocalPosition;
                position += door.Location switch
                {
                    DoorLocation.Port => new Vector2(1.1f, 0f),
                    DoorLocation.Starboard => new Vector2(-1.1f, 0f),
                    _ => new Vector2(0f, -1.1f),
                };
                var xeno = entities.SpawnEntity("CMXenoDrone", new EntityCoordinates(ship, position));
                doors.Add((door.Owner, xeno, entities.GetNetEntity(door.Owner)));
            }
            Assert.That(doors.Count, Is.EqualTo(variant.StartsWith("omaha") ? 5 : 1));
        });
        await pair.RunSeconds(1);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var prying = entities.System<PryingSystem>();
            foreach (var (door, xeno, _) in doors)
            {
                Assert.That(entities.GetComponent<AirlockComponent>(door).Powered, Is.True);
                var label = entities.GetComponent<MetaDataComponent>(door).EntityName;
                Assert.That(entities.GetComponent<DoorComponent>(door).State, Is.EqualTo(DoorState.Closed), label);
                var claw = entities.GetComponent<PryingComponent>(xeno);
                var attempt = new BeforePryEvent(xeno, claw.PryPowered, claw.Force, true);
                entities.EventBus.RaiseLocalEvent(door, ref attempt);
                Assert.That(attempt.Cancelled, Is.False, $"{label}: {attempt.Message}");
                Assert.That(entities.System<SharedVerbSystem>().GetLocalVerbs(door, xeno, typeof(AlternativeVerb), force: true)
                    .Any(v => v.Text == Loc.GetString("door-pry")), Is.True);
                Assert.That(prying.TryPry(door, xeno, out var id, xeno), Is.True);
                Assert.That(id, Is.Not.Null, "A xeno must enter the normal airlock pry do-after.");
                delay = Math.Max(delay, entities.GetComponent<DoAfterComponent>(xeno).DoAfters[id!.Value.Index].Args.Delay.TotalSeconds);
            }
        });
        await pair.RunSeconds((float) delay + 0.8f);
        await pair.RunUntilSynced();
        await pair.Server.WaitAssertion(() =>
        {
            foreach (var (door, _, _) in doors)
            {
                Assert.That(pair.Server.EntMan.GetComponent<DoorComponent>(door).State, Is.EqualTo(DoorState.Open));
                Assert.That(pair.Server.EntMan.GetComponent<PhysicsComponent>(door).CanCollide, Is.False);
            }
        });
        await pair.Client.WaitAssertion(() =>
        {
            // Headless integration ticks do not advance render animations.
            pair.Client.EntMan.System<AppearanceSystem>().FrameUpdate(0f);
            pair.Client.EntMan.System<AnimationPlayerSystem>().FrameUpdate(1f);
            foreach (var (_, _, net) in doors)
            {
                var door = pair.Client.EntMan.GetEntity(net);
                Assert.That(pair.Client.EntMan.GetComponent<SpriteComponent>(door)[DoorVisualLayers.Base].RsiState.ToString(),
                    Is.EqualTo("door_open"), "Prying must open the displayed hatch as well as its collision.");
            }
        });
        await pair.RunSeconds(6);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            foreach (var (door, xeno, _) in doors)
            {
                Assert.That(entities.GetComponent<DoorComponent>(door).State, Is.EqualTo(DoorState.Closed));
                entities.System<WeldableSystem>().SetWeldedState(door, true);
                Assert.That(entities.GetComponent<DoorComponent>(door).State, Is.EqualTo(DoorState.Welded));
                entities.System<PryingSystem>().TryPry(door, xeno, out var id, xeno);
                Assert.That(id, Is.Null, "A welded hatch must block normal prying.");
            }
        });
        await pair.RunUntilSynced();
        await pair.Client.WaitAssertion(() =>
        {
            pair.Client.EntMan.System<AppearanceSystem>().FrameUpdate(0f);
            foreach (var (_, _, net) in doors)
                Assert.That(pair.Client.EntMan.GetComponent<SpriteComponent>(pair.Client.EntMan.GetEntity(net))
                    [WeldableLayers.BaseWelded].Visible, Is.True);
        });
        await pair.Server.WaitAssertion(() => pair.Server.EntMan.DeleteEntity(ship));
        await pair.CleanReturnAsync();
    }
}
