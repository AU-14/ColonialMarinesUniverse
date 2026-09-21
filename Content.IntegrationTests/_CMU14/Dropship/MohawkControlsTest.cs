using System.Linq;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared._RMC14.Dropship;
using Content.Shared.Doors.Components;
using Content.Shared.Interaction;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class MohawkControlsTest
{
    [TestCase("omaha")]
    [TestCase("midway")]
    public async Task SideButtonsOperateTheirOwnHatch(string variant)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        EntityUid ship = default;
        EntityUid user = default;
        EntityUid port = default;
        EntityUid starboard = default;
        EntityUid starboardButton = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            entities.System<SharedMapSystem>().CreateMap(out var mapId);
            Assert.That(entities.System<MapLoaderSystem>().TryLoadGrid(mapId,
                new ResPath($"/Maps/CMU14/ShuttlesDropships/Mohawk/{variant}.yml"), out var loaded), Is.True);
            ship = loaded!.Value.Owner;
            var doors = entities.EntityQuery<DoorComponent>()
                .Where(d => entities.GetComponent<TransformComponent>(d.Owner).GridUid == ship).ToArray();
            Assert.That(doors.Count(d => d.Location == DoorLocation.Port), Is.EqualTo(1));
            Assert.That(doors.Count(d => d.Location == DoorLocation.Starboard), Is.EqualTo(1));
            port = doors.Single(d => d.Location == DoorLocation.Port).Owner;
            starboard = doors.Single(d => d.Location == DoorLocation.Starboard).Owner;
            Assert.That(entities.GetComponent<TransformComponent>(port).LocalRotation.Theta,
                Is.EqualTo(Angle.FromDegrees(-90).Theta).Within(0.0001));
            Assert.That(entities.GetComponent<TransformComponent>(starboard).LocalRotation.Theta,
                Is.EqualTo(Angle.FromDegrees(90).Theta).Within(0.0001));

            var dropships = entities.System<SharedDropshipSystem>();
            var controls = entities.EntityQuery<MohawkControlComponent>().ToArray();
            Assert.That(controls, Has.Length.EqualTo(5));
            foreach (var control in controls)
            {
                Assert.That(entities.GetComponent<TransformComponent>(control.Owner).Anchored, Is.True,
                    "Every interior and exterior button must be fixed to its deck.");
                Assert.That(dropships.TryGetGridDropship(control.Owner, out var owner), Is.True);
                Assert.That(owner.Owner, Is.EqualTo(ship));
            }
            starboardButton = controls.Single(c => c.Group == MohawkControlGroup.Starboard).Owner;
            user = entities.SpawnEntity(null, new EntityCoordinates(ship, 0.5f, 0.5f));
            var use = new InteractHandEvent(user, starboardButton);
            entities.EventBus.RaiseLocalEvent(starboardButton, use);
            Assert.That(use.Handled, Is.True);
        });
        await pair.RunSeconds(1);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            Assert.That(entities.GetComponent<DoorComponent>(starboard).State, Is.EqualTo(DoorState.Open));
            Assert.That(entities.GetComponent<DoorComponent>(port).State, Is.EqualTo(DoorState.Closed),
                "A starboard command must not operate the port hatch.");
            var use = new InteractHandEvent(user, starboardButton);
            entities.EventBus.RaiseLocalEvent(starboardButton, use);
        });
        await pair.RunSeconds(1);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<DoorComponent>(starboard).State, Is.EqualTo(DoorState.Closed));
            server.EntMan.DeleteEntity(ship);
        });
        await pair.CleanReturnAsync();
    }
}
