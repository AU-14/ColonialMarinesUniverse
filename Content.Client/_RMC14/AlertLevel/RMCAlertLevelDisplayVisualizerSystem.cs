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

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var current = _alertLevel.Get();

        if (current > RMCAlertLevels.Green)
            return;

        var query = EntityQueryEnumerator<RMCAlertLevelDisplayComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var comp, out var sprite))
        {
            if (!_sprite.LayerMapTryGet((uid, sprite), RMCAlertLevelDisplayVisualLayers.HourTens, out var hourTensLayer, false) ||
                !_sprite.LayerMapTryGet((uid, sprite), RMCAlertLevelDisplayVisualLayers.HourOnes, out var hourOnesLayer, false) ||
                !_sprite.LayerMapTryGet((uid, sprite), RMCAlertLevelDisplayVisualLayers.Separator, out var separatorLayer, false) ||
                !_sprite.LayerMapTryGet((uid, sprite), RMCAlertLevelDisplayVisualLayers.MinuteTens, out var minuteTensLayer, false) ||
                !_sprite.LayerMapTryGet((uid, sprite), RMCAlertLevelDisplayVisualLayers.MinuteOnes, out var minuteOnesLayer, false)
                )
                continue;

            var worldTime = (EntityQuery<GlobalTimeManagerComponent>().FirstOrDefault()?.TimeOffset ?? TimeSpan.Zero) + _ticker.RoundDuration();
            var timeString = worldTime.ToString(@"hh\:mm");
            var hourTensState = $"{timeString[0]}";
            var hourOnesState = $"{timeString[1]}";
            var separatorState = "~";
            var minuteTensState = $"{timeString[3]}";
            var minuteOnesState = $"{timeString[4]}";

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
