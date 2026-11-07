using System.Numerics;
using Content.Shared._RMC14.AlertLevel;
using Content.Shared.CMU14.Clock;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Clock;

public sealed class CMUMilitaryClockVisualizerSystem : EntitySystem
{
    [Dependency] private CMUMilitaryClockSystem _clock = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private float _updateAccumulator = 1f;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateAccumulator += frameTime;
        if (_updateAccumulator < 1f)
            return;

        _updateAccumulator %= 1f;
        var query = EntityQueryEnumerator<CMUMilitaryClockComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var clock, out var sprite))
        {
            if (!_sprite.LayerMapTryGet((uid, sprite), RMCAlertLevelDisplayVisualLayers.HourTens, out var hourTensLayer, false) ||
                !_sprite.LayerMapTryGet((uid, sprite), RMCAlertLevelDisplayVisualLayers.HourOnes, out var hourOnesLayer, false) ||
                !_sprite.LayerMapTryGet((uid, sprite), RMCAlertLevelDisplayVisualLayers.Separator, out var separatorLayer, false) ||
                !_sprite.LayerMapTryGet((uid, sprite), RMCAlertLevelDisplayVisualLayers.MinuteTens, out var minuteTensLayer, false) ||
                !_sprite.LayerMapTryGet((uid, sprite), RMCAlertLevelDisplayVisualLayers.MinuteOnes, out var minuteOnesLayer, false))
            {
                continue;
            }

            var time = _clock.GetTime(clock).ToString(@"hh\:mm");
            _sprite.LayerSetOffset((uid, sprite), hourTensLayer, new Vector2(0.11f, -0.4375f));
            _sprite.LayerSetOffset((uid, sprite), hourOnesLayer, new Vector2(0.28f, -0.4375f));
            _sprite.LayerSetOffset((uid, sprite), separatorLayer, new Vector2(0.406f, -0.4375f));
            _sprite.LayerSetOffset((uid, sprite), minuteTensLayer, new Vector2(0.56f, -0.4375f));
            _sprite.LayerSetOffset((uid, sprite), minuteOnesLayer, new Vector2(0.73f, -0.4375f));
            _sprite.LayerSetRsiState((uid, sprite), hourTensLayer, time[0].ToString());
            _sprite.LayerSetRsiState((uid, sprite), hourOnesLayer, time[1].ToString());
            _sprite.LayerSetRsiState((uid, sprite), separatorLayer, "~");
            _sprite.LayerSetRsiState((uid, sprite), minuteTensLayer, time[3].ToString());
            _sprite.LayerSetRsiState((uid, sprite), minuteOnesLayer, time[4].ToString());
        }
    }
}
