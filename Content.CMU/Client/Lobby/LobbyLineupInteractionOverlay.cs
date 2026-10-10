using System.Numerics;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Lobby;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Lobby;

/// <summary>One bounded outgoing interaction per card; impacts layer over the recipient's own action.</summary>
public sealed partial class LobbyLineupInteractionOverlay : Control
{
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private IEntityManager _entities = default!;
    private readonly List<Interaction> _active = new();
    private Texture? _rifle;
    public int ActiveCount => _active.Count;

    private sealed class Interaction(LobbyLineupCard source, LobbyLineupCard target, LobbyLineupEmote move, int seed, float delay)
    {
        public readonly LobbyLineupCard Source = source;
        public readonly LobbyLineupCard Target = target;
        public readonly LobbyLineupEmote Move = move;
        public readonly int Seed = seed;
        public readonly int Revision = source.EmoteRevision;
        public float Age = -delay;
        public LobbyLineupReaction Response;
    }

    public LobbyLineupInteractionOverlay()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Ignore;
        RectClipContent = false;
    }

    public void Add(LobbyLineupCard source, LobbyLineupCard target, LobbyLineupEmote move, int seed, float delay = 0)
    {
        if (!LobbyLineupInteractions.CanTarget(move) || source.StageMoving || target.StageMoving)
            return;
        _active.RemoveAll(effect => effect.Source == source);
        _active.Add(new Interaction(source, target, move, seed, delay));
        source.AimAt(target);
    }

    public void Clear()
    {
        foreach (var effect in _active)
        {
            effect.Source.ClearInteractions();
            effect.Target.ClearInteractions();
        }
        _active.Clear();
    }

    public void Advance(float delta)
    {
        for (var i = _active.Count - 1; i >= 0; i--)
        {
            var effect = _active[i];
            if (effect.Source.Parent == null || effect.Target.Parent == null ||
                effect.Source.StageMoving || effect.Target.StageMoving || effect.Source.EmoteRevision != effect.Revision)
            {
                _active.RemoveAt(i);
                continue;
            }
            var previous = effect.Age;
            effect.Age += Math.Max(0, delta);
            for (var cue = 0; cue < LobbyLineupInteractionChoreography.CueCount(effect.Move); cue++)
            {
                var impact = LobbyLineupInteractionChoreography.CueTime(effect.Move, cue) +
                    LobbyLineupInteractionChoreography.FlightTime(effect.Move);
                if (previous < impact && effect.Age >= impact &&
                    (!LobbyLineupInteractionChoreography.IsGun(effect.Move) || cue % 3 == 0))
                    effect.Response = effect.Target.React(effect.Move, effect.Seed + cue / 3);
            }
            if (effect.Age > LobbyLineupChoreography.Duration(effect.Move) + LobbyLineupInteractionChoreography.ReactionDuration)
                _active.RemoveAt(i);
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var transform = handle.GetTransform();
        handle.SetTransform(Matrix3x2.CreateScale(UIScale) * transform);
        var reduced = _configuration.GetCVar(CCVars.ReducedMotion);
        foreach (var effect in _active)
        {
            if (effect.Age < 0 || effect.Source.Parent == null || effect.Target.Parent == null ||
                effect.Source.StageMoving || effect.Target.StageMoving)
                continue;
            var start = effect.Source.InteractionPosition - GlobalPosition;
            var end = effect.Target.InteractionPosition - GlobalPosition;
            var unit = Math.Clamp(Math.Min(effect.Target.StageBounds.Width, effect.Target.StageBounds.Height) / 70, 0.3f, 1.2f);
            var gun = LobbyLineupInteractionChoreography.IsGun(effect.Move);
            var flight = LobbyLineupInteractionChoreography.FlightTime(effect.Move);
            if (gun && effect.Age < LobbyLineupChoreography.Duration(effect.Move))
            {
                _rifle ??= _entities.System<SpriteSystem>().RsiStateLike(new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/CMU14/Weapons/Guns/USCM/m41mk2.rsi"), "wielded-inhand-right"))
                    .GetFrame(RsiDirection.East, 0);
                var heading = end - start;
                var angle = MathF.Atan2(heading.Y, heading.X);
                var size = effect.Source.StageScale.X * 16;
                var previous = handle.GetTransform();
                handle.SetTransform(Matrix3x2.CreateScale(1, heading.X < 0 ? -1 : 1) *
                    Matrix3x2.CreateRotation(angle) * Matrix3x2.CreateTranslation(start) * previous);
                handle.DrawTextureRect(_rifle, new UIBox2(new Vector2(-size), new Vector2(size)));
                handle.SetTransform(previous);
                if (heading.LengthSquared() > 0.01f)
                    start += Vector2.Normalize(heading) * size * 0.85f;
            }
            for (var cue = 0; cue < LobbyLineupInteractionChoreography.CueCount(effect.Move); cue++)
            {
                var age = effect.Age - LobbyLineupInteractionChoreography.CueTime(effect.Move, cue);
                if (age < 0 || age > flight + LobbyLineupInteractionChoreography.ReactionDuration)
                    continue;
                if (age < flight && !reduced)
                {
                    var t = age / flight;
                    var at = Vector2.Lerp(start, end, t);
                    if (gun)
                    {
                        handle.DrawLine(Vector2.Lerp(start, end, Math.Max(0, t - 0.2f)), at, Color.FromHex("#FFD993"));
                        if (age < 0.06f)
                            handle.DrawCircle(start, 3 * unit, Color.FromHex("#FFE8A1"));
                    }
                    else
                    {
                        at.Y -= MathF.Sin(t * MathF.PI) * Math.Min(80, Vector2.Distance(start, end) * 0.25f + 12);
                        DrawProp(handle, effect.Move, at, unit);
                    }
                }
                else if (age >= flight && (!gun || cue % 3 == 0))
                {
                    var life = (age - flight) / LobbyLineupInteractionChoreography.ReactionDuration;
                    var color = effect.Move == LobbyLineupEmote.PieToss ? Color.FromHex("#FFF2D6") :
                        effect.Move == LobbyLineupEmote.BananaPeel ? Color.Yellow : Color.FromHex("#88E4DA");
                    if (reduced)
                    {
                        handle.DrawCircle(end, 10 * unit, color.WithAlpha(0.5f), false);
                        continue;
                    }
                    if (effect.Move == LobbyLineupEmote.PieToss)
                    {
                        if (effect.Response == LobbyLineupReaction.Duck)
                        {
                            // A successful dodge sends the pie sailing past, instead of painting cream on the face.
                            if (life < 0.4f)
                                DrawProp(handle, effect.Move, end + new Vector2(60 * life, -12 + life * 30) * unit, unit);
                        }
                        else
                        {
                            for (var blob = 0; blob < 5; blob++)
                                handle.DrawCircle(end + new Vector2(MathF.Sin(blob * 2) * 7, -8 + MathF.Cos(blob * 3) * 5) * unit,
                                    5 * unit, color.WithAlpha(1 - life));
                        }
                    }
                    else if (effect.Move == LobbyLineupEmote.BananaPeel)
                        DrawProp(handle, effect.Move, end + new Vector2(0, 22 * unit), unit);
                    else
                        for (var particle = 0; particle < 8; particle++)
                        {
                            var angle = particle * MathF.Tau / 8 + effect.Seed % 7;
                            var at = end + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (8 + life * 32) * unit;
                            at.Y += life * life * 25 * unit;
                            var tint = effect.Move == LobbyLineupEmote.ConfettiCannon
                                ? ConfettiColor(particle) : color;
                            handle.DrawRect(new UIBox2(at - new Vector2(1.5f * unit), at + new Vector2(1.5f * unit)), tint.WithAlpha(1 - life));
                        }
                }
            }
        }
        handle.SetTransform(transform);
    }

    private static Color ConfettiColor(int index) => (index % 3) switch
    {
        0 => Color.FromHex("#FF83AA"), 1 => Color.FromHex("#F4DF79"), _ => Color.FromHex("#7CDEE5"),
    };

    private static void DrawProp(DrawingHandleScreen handle, LobbyLineupEmote move, Vector2 at, float unit)
    {
        if (move == LobbyLineupEmote.BananaPeel)
        {
            for (var side = -1; side <= 1; side++)
            {
                var tip = at + new Vector2(side * 9, 5) * unit;
                handle.DrawLine(at - new Vector2(0, 6 * unit), tip, Color.Yellow);
                handle.DrawLine(tip, at + new Vector2(side * 3, 0) * unit, Color.FromHex("#E7B943"));
            }
        }
        else if (move == LobbyLineupEmote.PieToss)
        {
            handle.DrawCircle(at, 8 * unit, Color.FromHex("#BA8B57"));
            handle.DrawCircle(at - new Vector2(0, 2 * unit), 6 * unit, Color.FromHex("#FFF2D6"));
            handle.DrawCircle(at - new Vector2(0, 5 * unit), 2 * unit, Color.Red);
        }
        else
            for (var i = 0; i < 6; i++)
            {
                var p = at + new Vector2(MathF.Sin(i * 5), MathF.Cos(i * 5)) * 8 * unit;
                handle.DrawRect(new UIBox2(p - new Vector2(2 * unit), p + new Vector2(2 * unit)), ConfettiColor(i));
            }
    }
}
