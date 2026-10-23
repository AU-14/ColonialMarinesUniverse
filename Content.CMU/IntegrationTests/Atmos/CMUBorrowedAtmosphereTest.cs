using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Atmos.EntitySystems;
using Content.Server.CMU14.Atmos;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Maps;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.Atmos;

/// <summary>
///     A dropship deck with no air of its own, like the Mohawk's lowered ramp, breathes the air of the grid it's parked
///     over instead of reading as vacuum.
/// </summary>
[TestFixture]
public sealed class CMUBorrowedAtmosphereTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task DeckBreathesTheHangarItIsOver()
    {
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            var atmos = SEntMan.System<AtmosphereSystem>();
            var tiles = Server.ResolveDependency<ITileDefinitionManager>();
            var plating = new Tile(tiles["Plating"].TileId);

            // A vacuum map, like a warship's.
            var map = maps.CreateMap(out var mapId, runMapInit: true);

            var hangar = maps.CreateGridEntity(mapId);
            maps.SetTile(hangar, Vector2i.Zero, plating);
            SEntMan.EnsureComponent<GasTileOverlayComponent>(hangar);
            var hangarAtmos = SEntMan.EnsureComponent<GridAtmosphereComponent>(hangar);
            atmos.RebuildGridAtmosphere((hangar.Owner, hangarAtmos, hangar.Comp));

            var deck = maps.CreateGridEntity(mapId);
            maps.SetTile(deck, Vector2i.Zero, plating);
            SEntMan.RemoveComponent<GridAtmosphereComponent>(deck);

            var hangarAir = atmos.GetTileMixture((hangar.Owner, null, null), map, Vector2i.Zero);
            Assert.That(hangarAir, Is.Not.Null);
            Assert.That(hangarAir!.Pressure, Is.GreaterThan(50f), "The hangar should have air to share.");

            Assert.That(atmos.GetTileMixture((deck.Owner, null, null), map, Vector2i.Zero)!.Pressure, Is.EqualTo(0f).Within(0.01f),
                "Without the marker an airless deck reads as vacuum, which is the bug.");

            SEntMan.EnsureComponent<CMUBorrowedAtmosphereComponent>(deck);
            Assert.That(atmos.GetTileMixture((deck.Owner, null, null), map, Vector2i.Zero), Is.SameAs(hangarAir),
                "The deck should share the hangar's air rather than read as vacuum.");

            SEntMan.DeleteEntity(map);
        });
    }
}
