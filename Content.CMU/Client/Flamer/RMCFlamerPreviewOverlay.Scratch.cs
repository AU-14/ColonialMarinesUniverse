using Content.Shared._RMC14.Line;

namespace Content.Client._RMC14.Weapons.Ranged.Flamer;

public sealed partial class RMCFlamerPreviewOverlay
{
    private readonly List<LineTile> _cmuPreviewTiles = new();
    private readonly Dictionary<EntityUid, TileSet> _cmuTilesByGrid = new();
    private readonly List<TileSet> _cmuTileSets = new();
}
