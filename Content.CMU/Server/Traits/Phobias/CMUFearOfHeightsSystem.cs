using System.Numerics;
using Content.Shared.CMU14.Traits.Phobias;
using Content.Shared.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Maps;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Traits.Phobias;

/// <summary>
///     Someone afraid of heights gets dizzy within a couple of tiles of a drop that's more than one story deep.
/// </summary>
public sealed class CMUFearOfHeightsSystem : EntitySystem
{
    private static readonly EntProtoId Woozy = "StatusEffectWoozy";
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);
    private const int ScaredMessages = 4;

    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private ITileDefinitionManager _tileDefinitions = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUFearOfHeightsComponent>();
        while (query.MoveNext(out var uid, out var fear))
        {
            if (now < fear.NextCheck)
                continue;

            fear.NextCheck = now + CheckInterval;

            var scared = !_mobState.IsIncapacitated(uid) &&
                         !_container.IsEntityInContainer(uid) &&
                         NearBigDrop(uid, fear.Range);

            if (scared)
            {
                _status.TryUpdateStatusEffectDuration(uid, Woozy, fear.Dizziness);
                if (!fear.WasScared || now >= fear.NextMessage)
                {
                    fear.NextMessage = now + fear.MessageCooldown;
                    var message = Loc.GetString($"cmu-fear-of-heights-scared-{_random.Next(1, ScaredMessages + 1)}");
                    _popup.PopupEntity(message, uid, uid, PopupType.MediumCaution);
                }
            }
            else if (fear.WasScared)
            {
                _popup.PopupEntity(Loc.GetString("cmu-fear-of-heights-calm"), uid, uid);
            }

            fear.WasScared = scared;
        }
    }

    /// <summary>
    ///     Whether there's open air within <paramref name="range"/> tiles that drops more than one story: open on
    ///     this level and on the level below it too.
    /// </summary>
    private bool NearBigDrop(EntityUid uid, int range)
    {
        var xform = Transform(uid);
        if (xform.MapUid is not { } map ||
            !TryComp(map, out CMUZLevelMapComponent? zMap) ||
            !TryComp(map, out MapGridComponent? grid) ||
            !_zLevels.TryMapOffset((map, zMap), -1, out var below) ||
            !TryComp(below.Value.Owner, out MapGridComponent? belowGrid))
        {
            return false;
        }

        var position = _transform.GetWorldPosition(xform);
        for (var x = -range; x <= range; x++)
        {
            for (var y = -range; y <= range; y++)
            {
                if (x * x + y * y > range * range + 1)
                    continue;

                var point = position + new Vector2(x, y);
                if (IsOpen(map, grid, point) && IsOpen(below.Value.Owner, belowGrid, point))
                    return true;
            }
        }

        return false;
    }

    private bool IsOpen(EntityUid map, MapGridComponent grid, Vector2 point)
    {
        return !_map.TryGetTileRef(map, grid, point, out var tile) ||
               CMUZLevelOpeningCache.IsFallThroughTile(tile.Tile, _tileDefinitions);
    }
}
