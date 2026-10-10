#pragma warning disable RA0002 // Seed and reset authoritative atmos state between ignition updates.

using Content.IntegrationTests.Fixtures;
using Content.Server.IgnitionSource;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.IgnitionSource;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Atmos;

[TestFixture]
[TestOf(typeof(IgnitionSourceSystem))]
public sealed class IgnitionSourceUpdateRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  id: IgnitionUpdateInitiallyLit
  components:
  - type: IgnitionSource
    ignited: true
    temperature: 700
""";

    [Test]
    public async Task ActiveSourcesExposeAtSustainCadenceAndStopWhenExtinguished()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var system = Server.System<IgnitionSourceSystem>();
            var timing = Server.ResolveDependency<IGameTiming>();
            var tile = AddCombustibleTile(map.Grid.Owner, map.Tile.GridIndices);
            var source = SEntMan.SpawnEntity(null, map.GridCoords);
            var component = SEntMan.AddComponent<IgnitionSourceComponent>(source);

            system.Update(0f);
            Assert.That(tile.Hotspot.Valid, Is.False);
            system.SetIgnited(source);
            system.Update(0f);
            Assert.Multiple(() =>
            {
                Assert.That(tile.Hotspot.Valid, Is.True);
                Assert.That(tile.Hotspot.Temperature, Is.EqualTo(700f));
                Assert.That(tile.Hotspot.Volume, Is.EqualTo(1250f), "Initial exposure retains the original volume.");
            });

            // Keep master's one-second sustain cadence while skipping unlit sources.
            tile.Hotspot.Temperature = 400f;
            tile.Hotspot.Volume = 10f;
            system.Update(0f);
            Assert.Multiple(() =>
            {
                Assert.That(tile.Hotspot.Temperature, Is.EqualTo(400f));
                Assert.That(tile.Hotspot.Volume, Is.EqualTo(10f));
                Assert.That(component.NextExpose, Is.EqualTo(timing.CurTime + TimeSpan.FromSeconds(1)));
            });

            component.NextExpose = timing.CurTime;
            system.Update(0f);
            Assert.Multiple(() =>
            {
                Assert.That(tile.Hotspot.Temperature, Is.EqualTo(700f));
                Assert.That(tile.Hotspot.Volume, Is.EqualTo(50f));
            });

            system.SetIgnited(source, false);
            tile.Hotspot.Temperature = 400f;
            system.Update(0f);
            Assert.That(tile.Hotspot.Temperature, Is.EqualTo(400f));
            system.SetIgnited(source);
            system.Update(0f);
            Assert.That(tile.Hotspot.Temperature, Is.EqualTo(400f), "Relighting retains the sustain deadline.");
            component.NextExpose = timing.CurTime;
            system.Update(0f);
            Assert.That(tile.Hotspot.Temperature, Is.EqualTo(700f));
        });
    }

    [Test]
    public async Task PausedSourceResumesWithoutAnotherIgnitionChange()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var system = Server.System<IgnitionSourceSystem>();
            var metadata = Server.System<MetaDataSystem>();
            var tile = AddCombustibleTile(map.Grid.Owner, map.Tile.GridIndices);
            var source = SEntMan.SpawnEntity("IgnitionUpdateInitiallyLit", map.GridCoords);
            metadata.SetEntityPaused(source, true);

            system.Update(0f);
            Assert.That(tile.Hotspot.Valid, Is.False);

            metadata.SetEntityPaused(source, false);
            system.Update(0f);
            Assert.That(tile.Hotspot.Valid, Is.True);

            metadata.SetEntityPaused(source, true);
            tile.Hotspot.Temperature = 400f;
            system.Update(0f);
            Assert.That(tile.Hotspot.Temperature, Is.EqualTo(400f));
            metadata.SetEntityPaused(source, false);
        });
    }

    [Test]
    public async Task ComponentLifecycleImmediatelyChangesExposure()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var system = Server.System<IgnitionSourceSystem>();
            var tile = AddCombustibleTile(map.Grid.Owner, map.Tile.GridIndices);
            system.Update(0f);

            var source = SEntMan.SpawnEntity("IgnitionUpdateInitiallyLit", map.GridCoords);
            system.Update(0f);
            Assert.That(tile.Hotspot.Valid, Is.True, "Prototype-lit sources must expose on their first update.");

            SEntMan.RemoveComponent<IgnitionSourceComponent>(source);
            tile.Hotspot.Temperature = 400f;
            system.Update(0f);
            Assert.That(tile.Hotspot.Temperature, Is.EqualTo(400f));

            SEntMan.AddComponent<IgnitionSourceComponent>(source);
            system.SetIgnited(source);
            system.Update(0f);
            Assert.That(tile.Hotspot.Temperature, Is.EqualTo(700f));

            SEntMan.DeleteEntity(source);
            tile.Hotspot.Temperature = 400f;
            system.Update(0f);
            Assert.That(tile.Hotspot.Temperature, Is.EqualTo(400f));
        });
    }

    [Test]
    public async Task DirectAdminFieldChangesAreObservedWithoutCallingSetIgnited()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var system = Server.System<IgnitionSourceSystem>();
            var tile = AddCombustibleTile(map.Grid.Owner, map.Tile.GridIndices);
            EntityUid source = default;
            for (var i = 0; i < 256; i++)
            {
                source = SEntMan.SpawnEntity(null, map.GridCoords);
                SEntMan.AddComponent<IgnitionSourceComponent>(source);
            }

            system.Update(0f);
            Assert.That(tile.Hotspot.Valid, Is.False);

            // Legacy VV writes the field directly and dirties the component. It does
            // not call SharedIgnitionSourceSystem.SetIgnited or raise a state event.
            var component = SEntMan.GetComponent<IgnitionSourceComponent>(source);
            var field = typeof(IgnitionSourceComponent).GetField(nameof(IgnitionSourceComponent.Ignited))!;
            field.SetValue(component, true);
            SEntMan.Dirty(source, component);
            system.Update(0f);
            Assert.That(tile.Hotspot.Valid, Is.True);

            field.SetValue(component, false);
            SEntMan.Dirty(source, component);
            tile.Hotspot.Temperature = 400f;
            system.Update(0f);
            Assert.That(tile.Hotspot.Temperature, Is.EqualTo(400f));
        });
    }

    private TileAtmosphere AddCombustibleTile(EntityUid grid, Vector2i indices)
    {
        var atmos = SEntMan.EnsureComponent<GridAtmosphereComponent>(grid);
        var mixture = new GasMixture(Atmospherics.CellVolume);
        mixture.SetMoles(Gas.Oxygen, 100f);
        mixture.SetMoles(Gas.Plasma, 10f);
        var tile = new TileAtmosphere(grid, indices, mixture);
        atmos.Tiles[indices] = tile;
        return tile;
    }
}
