using Content.IntegrationTests.Tests.Interaction;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.Tiles;

public sealed class TileConstructionTests : InteractionTest
{
    /// <summary>
    /// Test placing and cutting a single lattice.
    /// </summary>
    [Test]
    public async Task PlaceThenCutLattice()
    {
        await AssertTile(Plating);
        await AssertTile(Plating, PlayerCoords);
        AssertGridCount(1);
        await SetTile(null);
        await InteractUsing(Rod);
        await AssertTile(Lattice);
        Assert.That(HandSys.GetActiveItem((SEntMan.GetEntity(Player), Hands)), Is.Null);
        await InteractUsing(Cut);
        await AssertTile(null);
        await AssertEntityLookup((Rod, 1));
        AssertGridCount(1);
    }

    /// <summary>
    /// Test placing and cutting a single lattice in space (not adjacent to any existing grid.
    /// </summary>
    [Test]
    public async Task CutThenPlaceLatticeNewGrid()
    {
        await AssertTile(Plating);
        await AssertTile(Plating, PlayerCoords);
        AssertGridCount(1);

        // Remove grid
        await SetTile(null);
        await SetTile(null, PlayerCoords);
        Assert.That(MapData.Grid.Comp.Deleted);
        AssertGridCount(0);

        // Place Lattice
        var oldPos = TargetCoords;
        TargetCoords = SEntMan.GetNetCoordinates(new EntityCoordinates(MapData.MapUid, 1, 0));
        await InteractUsing(Rod);
        TargetCoords = oldPos;
        await AssertTile(Lattice);
        AssertGridCount(1);

        // Cut lattice
        Assert.That(HandSys.GetActiveItem((SEntMan.GetEntity(Player), Hands)), Is.Null);
        await InteractUsing(Cut);
        await AssertTile(null);
        AssertGridCount(0);

        await AssertEntityLookup((Rod, 1));
    }

    /// <summary>
    /// Test space -> floor -> plating
    /// </summary>
    [Test]
    public async Task FloorConstructDeconstruct()
    {
        await AssertTile(Plating);
        await AssertTile(Plating, PlayerCoords);
        AssertGridCount(1);

        // Remove grid
        await SetTile(null);
        await SetTile(null, PlayerCoords);
        Assert.That(MapData.Grid.Comp.Deleted);
        AssertGridCount(0);

        // Space -> Lattice
        var oldPos = TargetCoords;
        TargetCoords = SEntMan.GetNetCoordinates(new EntityCoordinates(MapData.MapUid, 1, 0));
        await InteractUsing(Rod);
        TargetCoords = oldPos;
        await AssertTile(Lattice);
        AssertGridCount(1);

        // Lattice -> Plating
        await InteractUsing(FloorItem);
        Assert.That(HandSys.GetActiveItem((SEntMan.GetEntity(Player), Hands)), Is.Null);
        await AssertTile(Plating);
        AssertGridCount(1);

        // CMU14: CM floors only tile over CM plating, so lay the matching subfloor first
        await SetTile(CMPlating);

        // CM plating -> Tile
        await InteractUsing(FloorItem);
        Assert.That(HandSys.GetActiveItem((SEntMan.GetEntity(Player), Hands)), Is.Null);
        await AssertTile(Floor);
        AssertGridCount(1);

        // Tile -> CM plating
        await InteractUsing(Pry);
        await AssertTile(CMPlating);
        AssertGridCount(1);

        await AssertTileItemReturned();
    }

    /// <summary>
    /// Test CM plating -> floor -> CM plating using tile stacking.
    /// CMU14: CM floor tiles only place over CM plating, so the snow plating case does not exist for them.
    /// </summary>
    [Test]
    public async Task BrassPlatingPlace()
    {
        await SetTile(CMPlating);

        // CM plating -> Tile
        await InteractUsing(FloorItem);
        Assert.That(HandSys.GetActiveItem((SEntMan.GetEntity(Player), Hands)), Is.Null);
        await AssertTile(Floor);
        AssertGridCount(1);

        // Tile -> CM plating
        await InteractUsing(Pry);
        await AssertTile(CMPlating);
        AssertGridCount(1);
        await AssertTileItemReturned();
    }

    // CMU14: the pried tile can end up in a hand (auto pickup) or on the floor
    // depending on pickup timing; require the tile item to exist either way.
    private async Task AssertTileItemReturned()
    {
        var player = SEntMan.GetEntity(Player);
        foreach (var held in HandSys.EnumerateHeld((player, Hands)))
        {
            if (SEntMan.GetComponent<MetaDataComponent>(held).EntityPrototype?.ID == FloorItem)
                return;
        }

        await AssertEntityLookup((FloorItem, 1));
    }
}
