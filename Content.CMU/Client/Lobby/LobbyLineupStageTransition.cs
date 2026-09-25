using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Log;

namespace Content.Client.CMU14.Lobby;

/// <summary>A temporary screen-sized layer borrowing the cards' preview entities during arrival/departure.</summary>
public sealed class LobbyLineupStageTransition : Control
{
    [Dependency] private IEntityManager _entities = default!;
    private readonly List<(LobbyLineupCard Card, TransitionSpriteView View)> _actors = new();
    private readonly bool _leaving;
    private readonly bool _reducedMotion;
    private float _elapsed;
    private float _duration = 3.6f;
    public float Progress => Math.Clamp(_elapsed / (_reducedMotion ? 0.3f : _duration), 0, 1);
    public bool Finished => Progress >= 1;

    public LobbyLineupStageTransition(IEnumerable<LobbyLineupCard> cards, bool leaving, bool reducedMotion)
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Ignore;
        _leaving = leaving;
        _reducedMotion = reducedMotion;
        foreach (var card in cards)
        {
            if (card.StageEntity is not { } entity)
                continue;
            var view = new TransitionSpriteView
            {
                Stretch = SpriteView.StretchMode.None,
                Scale = card.StageScale,
                MouseFilter = MouseFilterMode.Ignore,
                RectClipContent = false,
            };
            view.SetEntity(entity);
            AddChild(view);
            _actors.Add((card, view));
            card.SetStageMoving(true);
        }
    }

    public void Advance(float delta)
    {
        // Do not spend the entrance clock loading 60 profiles or waiting for the root's layout.
        if (Parent == null || Size.X <= 0 || Size.Y <= 0)
            return;
        _elapsed += Math.Min(delta, 0.05f);
        PositionActors();
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        base.MeasureOverride(availableSize);
        return Vector2.Zero;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        PositionActors();
        return finalSize;
    }

    private void PositionActors()
    {
        var duration = 0f;
        for (var i = 0; i < _actors.Count; i++)
        {
            var (card, view) = _actors[i];
            if (card.Parent == null || card.StageEntity == null)
                continue;
            var bounds = card.StageBounds;
            var target = bounds.Center - GlobalPosition;
            var size = bounds.Size;
            if (view.Scale != card.StageScale)
                view.Scale = card.StageScale;
            var edge = i % 3;
            var outside = edge switch
            {
                0 => new Vector2(-size.X, target.Y + size.Y * 0.5f),
                1 => new Vector2(Size.X + size.X, target.Y - size.Y * 0.5f),
                _ => new Vector2(target.X + size.X, Size.Y + size.Y),
            };
            var delay = 0.25f + i % 13 * 0.04f;
            var travel = Math.Clamp(Vector2.Distance(target, outside) / 1000, 1.35f, 2.4f);
            var settle = _leaving ? 0 : 0.5f;
            duration = Math.Max(duration, delay + travel + settle);
            var t = Math.Clamp((_elapsed - delay) / travel, 0, 1);
            // Near-linear travel reads as running. Reserve easing for accelerating/braking.
            var progress = _leaving ? t * (0.45f + 0.55f * t) : t + 0.12f * MathF.Sin(t * MathF.PI);
            var start = _leaving ? target : outside;
            var end = _leaving ? outside : target;
            var position = Vector2.Lerp(start, end, progress);
            var running = MathF.Sin((_elapsed - delay) * 20 + i);
            var moving = t > 0 && t < 1 ? 1f : 0f;
            position.Y -= MathF.Abs(running) * size.Y * 0.065f * moving;
            // A final overshoot, lean, and correction before joining the formation.
            var settling = Math.Clamp((_elapsed - delay - travel) / 0.5f, 0, 1);
            var stumble = !_leaving && t >= 1 ? MathF.Sin(settling * MathF.Tau) * (1 - settling) : 0;
            var sign = end.X > start.X ? 1 : -1;
            position.X += stumble * size.X * 0.15f * sign;
            var rotation = moving * (sign * -0.10f + running * 0.045f) + stumble * sign * -0.32f;
            view.OverrideDirection = !_leaving && settling >= 1 ? Direction.South : sign > 0 ? Direction.East : Direction.West;
            view.Visible = !_leaving || t < 1;
            if (_reducedMotion)
            {
                position = target;
                rotation = 0;
                view.OverrideDirection = Direction.South;
                view.Modulate = Color.White.WithAlpha(_leaving ? 1 - Progress : Progress);
            }
            if (view.Entity is { } entity && !_entities.Deleted(entity.Owner))
                _entities.System<SpriteSystem>().SetRotation((entity.Owner, entity.Comp1), new Angle(rotation));
            view.Measure(size);
            view.Arrange(new UIBox2(position - size / 2, position + size / 2));
        }
        _duration = Math.Max(0.3f, duration);
    }

    public void Release()
    {
        var drawn = 0;
        var moved = 0;
        foreach (var (card, view) in _actors)
        {
            if (view.DrawCount > 0)
                drawn++;
            if (view.DrawTravel > 1)
                moved++;
            if (view.Entity is { } entity && !_entities.Deleted(entity.Owner))
                _entities.System<SpriteSystem>().SetRotation((entity.Owner, entity.Comp1), Angle.Zero);
            if (card.Parent != null)
                card.SetStageMoving(false);
            // SpriteView does not own/delete the entity; the card remains its only owner.
            view.SetEntity((EntityUid?) null);
        }
        Logger.DebugS("lobby_party", $"{(_leaving ? "Departure" : "Arrival")}: {_actors.Count} previews, " +
            $"{drawn} rendered, {moved} moved while rendered, {_elapsed:F2}s elapsed, reducedMotion={_reducedMotion}.");
        _actors.Clear();
        Visible = false;
        UserInterfaceManager.DeferAction(() =>
        {
            Orphan();
            Dispose();
        });
    }

    /// <summary>Track actual render callbacks, so a running but obscured transition can be diagnosed.</summary>
    private sealed class TransitionSpriteView : SpriteView
    {
        public int DrawCount { get; private set; }
        public float DrawTravel { get; private set; }
        private Vector2 _lastDrawPosition;

        protected override void Draw(IRenderHandle renderHandle)
        {
            if (Entity is not { } entity || EntMan.Deleted(entity.Owner))
                return;
            if (DrawCount > 0)
                DrawTravel += Vector2.Distance(_lastDrawPosition, GlobalPosition);
            _lastDrawPosition = GlobalPosition;
            DrawCount++;
            base.Draw(renderHandle);
        }
    }
}
