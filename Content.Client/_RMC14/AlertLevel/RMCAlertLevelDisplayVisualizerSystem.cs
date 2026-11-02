using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.AlertLevel;
using Content.Shared.Clock;
using Content.Shared.GameTicking;
using Robust.Client.GameObjects;

namespace Content.Client._RMC14.AlertLevel;

public sealed partial class RMCAlertLevelDisplayVisualizerSystem : EntitySystem
{

    [Dependency] private SharedGameTicker _ticker = default!;
    [Dependency] private RMCAlertLevelSystem _alertLevel = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private static readonly string[] CMUDigits = ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9"]; // CMU14

    // CMU14 method: resolve time once per pass and reuse finite digit state names.
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        TimeSpan? worldTime = null;

        var query = EntityQueryEnumerator<RMCAlertLevelDisplayComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var comp, out var sprite))
        {
            // CMU14: each display reads its own warship's alert level
            if (_alertLevel.Get(uid) > RMCAlertLevels.Green)
                continue;

            if (!_sprite.LayerMapTryGet((uid, sprite), RMCAlertLevelDisplayVisualLayers.HourTens, out var hourTensLayer, false) ||
                !_sprite.LayerMapTryGet((uid, sprite), RMCAlertLevelDisplayVisualLayers.HourOnes, out var hourOnesLayer, false) ||
                !_sprite.LayerMapTryGet((uid, sprite), RMCAlertLevelDisplayVisualLayers.Separator, out var separatorLayer, false) ||
                !_sprite.LayerMapTryGet((uid, sprite), RMCAlertLevelDisplayVisualLayers.MinuteTens, out var minuteTensLayer, false) ||
                !_sprite.LayerMapTryGet((uid, sprite), RMCAlertLevelDisplayVisualLayers.MinuteOnes, out var minuteOnesLayer, false)
                )
                continue;

            if (worldTime == null)
            {
                var timeQuery = EntityQueryEnumerator<GlobalTimeManagerComponent>();
                worldTime = (timeQuery.MoveNext(out var time) ? time.TimeOffset : TimeSpan.Zero) + _ticker.RoundDuration();
            }

            // TimeSpan's custom hh:mm format wraps at 24 hours and omits the sign.
            var hours = Math.Abs(worldTime.Value.Hours);
            var minutes = Math.Abs(worldTime.Value.Minutes);
            var hourTensState = CMUDigits[hours / 10];
            var hourOnesState = CMUDigits[hours % 10];
            var separatorState = "~";
            var minuteTensState = CMUDigits[minutes / 10];
            var minuteOnesState = CMUDigits[minutes % 10];

            _sprite.LayerSetOffset((uid, sprite), hourTensLayer, new Vector2(0.11f, -0.4375f));
            _sprite.LayerSetOffset((uid, sprite), hourOnesLayer, new Vector2(0.28f, -0.4375f));
            _sprite.LayerSetOffset((uid, sprite), separatorLayer, new Vector2(0.406f, -0.4375f));
            _sprite.LayerSetOffset((uid, sprite), minuteTensLayer, new Vector2(0.56f, -0.4375f));
            _sprite.LayerSetOffset((uid, sprite), minuteOnesLayer, new Vector2(0.73f, -0.4375f));

            _sprite.LayerSetRsiState((uid, sprite), hourTensLayer, hourTensState);
            _sprite.LayerSetRsiState((uid, sprite), hourOnesLayer, hourOnesState);
            _sprite.LayerSetRsiState((uid, sprite), separatorLayer, separatorState);
            _sprite.LayerSetRsiState((uid, sprite), minuteTensLayer, minuteTensState);
            _sprite.LayerSetRsiState((uid, sprite), minuteOnesLayer, minuteOnesState);
        }
    }
}
