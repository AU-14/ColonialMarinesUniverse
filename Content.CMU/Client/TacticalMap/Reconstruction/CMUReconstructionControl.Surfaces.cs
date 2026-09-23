using System.Numerics;
using System.Linq;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Content.Shared.Maps;
using Content.Client.IconSmoothing;
using Content.Shared.Doors.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Prototypes;
using Robust.Shared.Graphics;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client.CMU14.TacticalMap.Reconstruction;

public sealed partial class CMUReconstructionControl
{
    private readonly Rgba32[] _cellUpload = new Rgba32[CMUReconGeometry.ChunkSize * CMUReconGeometry.ChunkSize];
    private readonly Rgba32[] _styleUpload = new Rgba32[CMUReconGeometry.ChunkSize * CMUReconGeometry.ChunkSize];
    private readonly List<UIBox2> _labelBoxes = [];
    private readonly List<(CMUReconLabel Label, Vector2 Point, float Distance)> _labelCandidates = [];

    private void UploadChunk(int x, int y, int level)
    {
        if (Scene is not { } scene || _terrain == null || _appearance == null || _occupancy == null)
            return;
        const int size = CMUReconGeometry.ChunkSize;
        var cells = _cellUpload.AsSpan();
        var styles = _styleUpload.AsSpan();
        var occupied = false;
        for (var dy = 0; dy < size; dy++)
        for (var dx = 0; dx < size; dx++)
        {
            var index = CMUReconGeometry.Index(x * size + dx, y * size + dy, level, scene.Width, scene.Height);
            var local = dy * size + dx;
            var style = scene.Appearance[index];
            cells[local] = new Rgba32(scene.Cells[index], scene.Directions[index], 0, 255);
            styles[local] = new Rgba32((byte) style, (byte) (style >> 8), (byte) (style >> 16), (byte) (style >> 24));
            occupied |= scene.Cells[index] != 0 || (style & 0xffff) != 0;
        }
        var offset = new Vector2i(level % 4 * scene.Width + x * size, level / 4 * scene.Height + y * size);
        _terrain.SetSubImage(offset, new Vector2i(size, size), cells);
        _appearance.SetSubImage(offset, new Vector2i(size, size), styles);
        var chunk = offset / size;
        _chunkPixels[chunk.Y * _occupancy.Width + chunk.X] = new Rgba32(occupied ? (byte) 255 : (byte) 0, 0, 0, 255);
    }

    private void AddSurfaces(CMUReconSurface[] surfaces)
    {
        foreach (var surface in surfaces) _pendingSurfaces.Enqueue(surface);
    }

    private void LoadSurfaces(CMUReconSurface[] surfaces)
    {
        var sprites = _entities.System<SpriteSystem>();
        foreach (var surface in surfaces)
        {
            if (surface.Id == 0 || surface.Id >= CMUReconGeometry.MaxSurfaces || _surfaceTextures.ContainsKey(surface.Id))
                continue;
            Texture[] textures;
            UIBox2? region = null;
            var tint = Color.White;
            if (surface.Entity)
            {
                if (!_prototypes.TryIndex<EntityPrototype>(surface.Prototype, out var prototype) ||
                    !prototype.Components.ContainsKey("Sprite") && !prototype.Components.ContainsKey("Icon"))
                    continue;
                if (prototype.TryComp(out SpriteComponent? appearance, _components))
                    tint = appearance.Color;
                // Border-rock placement icons contain editor markings. Compose the connected
                // corner frames used by the actual map instead of projecting those icons.
                if (prototype.TryComp(out DoorComponent? door, _components) &&
                    appearance?.BaseRSI?.TryGetState((surface.Variant & 4) != 0 ? door.OpenSpriteState : door.ClosedSpriteState, out var doorState) == true)
                {
                    var provider = (IDirectionalTextureProvider) doorState;
                    var direction = (new Angle((surface.Variant & 3) * Math.PI / 2)).GetCardinalDir();
                    textures = [provider.TextureFor(direction)];
                }
                else if (prototype.TryComp(out IconSmoothComponent? smooth, _components) &&
                    smooth.Mode == IconSmoothingMode.Corners &&
                    prototype.TryComp(out SpriteComponent? sprite, _components) &&
                    sprite.BaseRSI?.TryGetState(smooth.StateBase + "7", out var state) == true)
                {
                    var provider = (IDirectionalTextureProvider) state;
                    textures = [provider.TextureFor(Direction.South), provider.TextureFor(Direction.North),
                        provider.TextureFor(Direction.East), provider.TextureFor(Direction.West)];
                }
                else textures = [sprites.GetPrototypeIcon(prototype).Default];
            }
            else
            {
                if (!_prototypes.TryIndex<ContentTileDefinition>(surface.Prototype, out var tile) || tile.Sprite is not { } path ||
                    !_resources.TryGetResource<TextureResource>(path, out var resource))
                    continue;
                var texture = resource.Texture;
                var variant = Math.Min(surface.Variant, Math.Max(0, texture.Width / 32 - 1));
                region = UIBox2.FromDimensions(new Vector2(variant * 32, 0), new Vector2(32));
                textures = [texture];
            }
            _surfaceTextures.Add(surface.Id, (textures, region, tint));
            _newSurfaces.Add(surface.Id);
            _redraw = true;
        }
    }

    private void RenderSurfaces(DrawingHandleScreen handle)
    {
        if (_surfaceAtlas == null)
        {
            _surfaceAtlas = _clyde.CreateRenderTarget(new Vector2i(2048, 2048), RenderTargetColorFormat.Rgba8Srgb,
                new TextureSampleParameters { Filter = true }, "cmu-reconstruction-surfaces");
            _clearSurfaces = true;
        }
        if (!_clearSurfaces && _newSurfaces.Count == 0)
            return;
        var previous = handle.GetTransform();
        try
        {
            handle.RenderInRenderTarget(_surfaceAtlas, () =>
            {
                handle.SetTransform(Matrix3x2.Identity);
                foreach (var id in _newSurfaces)
                {
                    var (textures, region, tint) = _surfaceTextures[id];
                    var rect = UIBox2.FromDimensions(new Vector2(id % 64 * 32, id / 64 * 32), new Vector2(32));
                    foreach (var texture in textures)
                        handle.DrawTextureRectRegion(texture, rect, region, tint);
                }
            }, _clearSurfaces ? Color.Transparent : null);
        }
        finally { handle.SetTransform(previous); }
        _clearSurfaces = false;
        _newSurfaces.Clear();
    }

    private void DrawLabels(DrawingHandleScreen handle, CMUReconSnapshotMessage scene)
    {
        _labelBoxes.Clear();
        _labelCandidates.Clear();
        var detail = Math.Clamp(90f / Math.Max(1, _distance), 0, 1);
        var budget = 8 + (int) (detail * 24);
        var padding = new Vector2(24 - detail * 19, 14 - detail * 9);
        foreach (var label in scene.Labels)
        {
            if (label.Depth != scene.MinDepth + _selectedLevel)
                continue;
            var tile = label.Tile - scene.Origin;
            var p = Project(new Vector3(tile.X + 0.5f, tile.Y + 0.5f, _selectedLevel * CMUReconGeometry.LevelHeight + 0.22f));
            _labelCandidates.Add((label, p, Vector2.DistanceSquared(p, (Vector2) PixelSize / 2)));
        }
        _labelCandidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        foreach (var (label, p, _) in _labelCandidates)
        {
            if (_labelBoxes.Count >= budget) break;
            var dimensions = handle.GetDimensions(_font, label.Text, 1) + new Vector2(16, 8);
            var rect = UIBox2.FromDimensions(p - dimensions / 2, dimensions);
            if (rect.Left < 8 || rect.Right > PixelWidth - 8 || rect.Top < 36 || rect.Bottom > PixelHeight - 30 ||
                _labelBoxes.Any(other => rect.Intersects(other)))
                continue;
            _labelBoxes.Add(UIBox2.FromDimensions(rect.TopLeft - padding, rect.Size + padding * 2));
            handle.DrawRect(rect, Color.FromHex("#0B1826EC"));
            handle.DrawRect(rect, Color.FromHex("#41606C"), false);
            handle.DrawString(_font, rect.TopLeft + new Vector2(8, 4), label.Text, Color.FromHex("#E1EEEE"));
        }
    }
}
