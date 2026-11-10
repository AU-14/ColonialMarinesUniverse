using System.IO;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Round.Objectives.Type;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Round;

[TestFixture]
public sealed class CMUHotspotSerializationTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task HotspotAppearanceColorsSurviveServerToClientSerialization()
    {
        var map = await Pair.CreateTestMap();
        EntityUid hotspot = default;
        AppearanceComponent appearance = default!;
        await Server.WaitPost(() =>
        {
            hotspot = SEntMan.SpawnEntity(null, map.GridCoords);
            appearance = SEntMan.AddComponent<AppearanceComponent>(hotspot);
        });

        foreach (var color in new[] { "#FFB300", "#2979FF" })
        {
            byte[] payload = [];
            await Server.WaitPost(() =>
            {
                SEntMan.System<SharedAppearanceSystem>().SetData(hotspot, HotspotVisuals.ZoneColor, color);
                var state = SEntMan.GetComponentState(SEntMan.EventBus, appearance, ServerSession!, GameTick.Zero)!;
                var netId = Server.ResolveDependency<IComponentFactory>().GetRegistration<AppearanceComponent>().NetID!.Value;
                using var stream = new MemoryStream();
                Server.ResolveDependency<IRobustSerializer>().SerializeDirect(stream,
                    new ComponentChange(netId, state, appearance.LastModifiedTick));
                payload = stream.ToArray();
            });
            await Client.WaitPost(() =>
            {
                using var stream = new MemoryStream(payload);
                Client.ResolveDependency<IRobustSerializer>().DeserializeDirect<ComponentChange>(stream, out var change);
                Assert.That(change.State, Is.TypeOf<AppearanceComponentState>());
                Assert.That(((AppearanceComponentState) change.State!).Data[HotspotVisuals.ZoneColor], Is.EqualTo(color),
                    "The client must decode the initial and recolored objective using the actual appearance enum.");
            });
        }
    }
}
