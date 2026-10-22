using Content.Shared.CMU14.Vehicle.Open;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Draws an open vehicle's damage stages over its parts, on the vehicle and its overlay: each at
/// the highest level its part has reached in its damage type, and only while its part shows as it
/// was drawn on (fitted, and a panel shut).
/// </summary>
public sealed class CMUVehicleDamageStateVisualSystem : EntitySystem
{
    [Dependency] private CMUVehicleDamageStatesSystem _damageStates = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    // What each vehicle's stages last showed: the level, or -1 for nothing. A stage's layers are only
    // touched when that changes.
    private readonly Dictionary<EntityUid, int[]> _shown = new();

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(CMUOpenVehicleVisualSystem));
        SubscribeLocalEvent<CMUVehicleDamageStatesComponent, ComponentShutdown>((uid, _, _) => _shown.Remove(uid));
    }

    public override void FrameUpdate(float frameTime)
    {
        var vehicles = EntityQueryEnumerator<CMUVehicleDamageStatesComponent, SpriteComponent>();
        while (vehicles.MoveNext(out var uid, out var damage, out var sprite))
        {
            // Out of view: nothing to draw until it comes back.
            if ((MetaData(uid).Flags & MetaDataFlags.Detached) != 0)
                continue;

            Entity<SpriteComponent>? overlay = null;
            if (TryComp(uid, out CMUVehicleOverlayComponent? over) &&
                TryComp(over.Overlay, out SpriteComponent? overSprite))
            {
                overlay = (over.Overlay.Value, overSprite);
            }

            if (!_shown.TryGetValue(uid, out var last) || last.Length != damage.Stages.Count)
            {
                last = new int[damage.Stages.Count];
                Array.Fill(last, int.MinValue);
                _shown[uid] = last;
            }

            for (var i = 0; i < damage.Stages.Count; i++)
            {
                var stage = damage.Stages[i];
                var level = _damageStates.GetLevel(uid, damage, stage);
                var shown = level != null && AtRest((uid, sprite), stage);
                var now = shown ? level!.Value : -1;
                if (last[i] == now)
                    continue;

                // Until the overlay is there, nothing is kept: it still has to be drawn on when it comes.
                last[i] = overlay != null ? now : int.MinValue;
                Show((uid, sprite), stage.Layer, stage.Part, shown ? $"{stage.Layer}_{level}" : null);
                if (overlay != null)
                    Show(overlay.Value, stage.Layer, stage.OverlayPart ?? stage.Part, shown ? $"{stage.Layer}_overlay_{level}" : null);
            }
        }
    }

    /// <summary>
    /// Whether a stage's part shows on the vehicle as the stage was drawn on it.
    /// </summary>
    private bool AtRest(Entity<SpriteComponent> vehicle, CMUVehicleDamageStage stage)
    {
        if (!_sprite.LayerMapTryGet(vehicle.AsNullable(), stage.Part, out var index, false) ||
            !vehicle.Comp[index].Visible)
        {
            return false;
        }

        if (stage.RestStates.Count == 0)
            return true;

        var state = _sprite.LayerGetRsiState(vehicle.AsNullable(), index).Name ?? string.Empty;
        foreach (var rest in stage.RestStates)
        {
            if (state.StartsWith(rest))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Shows a stage's layer at a state, while the part's layer under it shows; hides it for none.
    /// </summary>
    private void Show(Entity<SpriteComponent> ent, string key, string part, string? state)
    {
        if (!_sprite.LayerMapTryGet(ent.AsNullable(), key, out var index, false))
            return;

        var visible = state != null &&
                      _sprite.LayerMapTryGet(ent.AsNullable(), part, out var partIndex, false) &&
                      ent.Comp[partIndex].Visible;
        if (visible && _sprite.LayerGetRsiState(ent.AsNullable(), index) != state)
            _sprite.LayerSetRsiState(ent.AsNullable(), index, state!);

        _sprite.LayerSetVisible(ent.AsNullable(), index, visible);
    }
}
