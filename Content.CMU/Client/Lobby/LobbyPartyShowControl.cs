using System.Numerics;
using Content.Shared.CMU14.Lobby;
using Robust.Client.Audio;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Audio;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Lobby;

/// <summary>Temporary lobby theatre. Crew previews belong to their cards; scenery belongs to the show.</summary>
public sealed partial class LobbyPartyShowControl : Control
{
    [Dependency] private IEntityManager _entities = default!;
    private readonly LobbyPartyShow _show;
    private readonly int _seed;
    private readonly bool _reduced;
    private readonly Random _random;
    private readonly List<(LobbyLineupCard Card, SpriteView View)> _actors = new();
    private readonly List<EntityUid> _sounds = new();
    private readonly Dictionary<(string Path, string State), IRsiStateLike> _art = new();
    private readonly Label _caption;
    private readonly Button _hideShow;
    private float _elapsed;
    private bool _released;
    public bool SoundEnabled { get; set; }
    public bool Finished => _released || _elapsed >= LobbyPartyChoreography.Duration(_show, _reduced);
    public LobbyPartyShow Show => _show;
    public float Elapsed => _elapsed;
    private float Unit => Math.Clamp(Size.Y / 900, 0.55f, 1.4f);
    private float Fade => LobbyLineupChoreography.Envelope(_elapsed,
        LobbyPartyChoreography.Duration(_show, _reduced), 0.6f, 0.8f);

    public LobbyPartyShowControl(LobbyPartyShowEvent show, IEnumerable<LobbyLineupCard> cards, bool reduced, bool sound)
    {
        IoCManager.InjectDependencies(this);
        _show = show.Show;
        _seed = show.Seed;
        _random = new Random(show.Seed);
        _reduced = reduced;
        SoundEnabled = sound;
        MouseFilter = MouseFilterMode.Ignore;
        RectClipContent = true;
        InitializeTargets();
        foreach (var card in cards)
        {
            if (card.StageEntity is not { } entity)
                continue;
            var view = new SpriteView
            {
                Stretch = SpriteView.StretchMode.None,
                MouseFilter = MouseFilterMode.Ignore,
                RectClipContent = false,
                OverrideDirection = Direction.East,
            };
            view.SetEntity(entity);
            AddChild(view);
            _actors.Add((card, view));
            card.SetStageMoving(true);
        }
        if (_show == LobbyPartyShow.Parade)
            InitializeParade();
        else
            InitializeBattlefield();
        AddChild(new Foreground(this) { MouseFilter = MouseFilterMode.Ignore });
        _caption = new Label
        {
            Text = Loc.GetString(_show == LobbyPartyShow.Flyby ? "cmu-lobby-party-flyby-title" : "cmu-lobby-party-parade-title"),
            Align = Label.AlignMode.Center,
            ClipText = true,
            FontColorOverride = Color.FromHex("#E5EFCB"),
            MouseFilter = MouseFilterMode.Ignore,
        };
        AddChild(_caption);
        _hideShow = new Button
        {
            Text = Loc.GetString("cmu-lobby-party-stop"),
            MouseFilter = MouseFilterMode.Stop,
        };
        _hideShow.OnPressed += _ => Release();
        AddChild(_hideShow);
    }

    public void Advance(float delta)
    {
        if (_released || Parent == null || Size.X <= 0 || Size.Y <= 0)
            return;
        var previous = _elapsed;
        _elapsed += Math.Max(0, delta);
        if (!_reduced)
        {
            if (_show == LobbyPartyShow.Flyby)
                AdvanceFlyby(previous);
            else
                AdvanceParade(previous);
        }
        PositionActors();
        if (_show == LobbyPartyShow.Parade)
            PositionFlags();
        Modulate = Color.White.WithAlpha(Fade);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        base.MeasureOverride(availableSize);
        return Vector2.Zero;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        foreach (var child in Children)
            child.Arrange(new UIBox2(Vector2.Zero, finalSize));
        var width = Math.Min(finalSize.X - 32, 940 * Unit);
        _caption.Arrange(new UIBox2((finalSize.X - width) / 2 + 12, finalSize.Y * 0.13f,
            (finalSize.X + width) / 2 - 125 * Unit, finalSize.Y * 0.13f + 32));
        _hideShow.Arrange(new UIBox2((finalSize.X + width) / 2 - 118 * Unit, finalSize.Y * 0.13f,
            (finalSize.X + width) / 2 - 4, finalSize.Y * 0.13f + 32));
        PositionActors();
        if (_show == LobbyPartyShow.Parade)
            PositionFlags();
        return finalSize;
    }

    private void PositionActors()
    {
        for (var i = 0; i < _actors.Count; i++)
        {
            var (card, view) = _actors[i];
            if (card.Parent == null || view.Entity is not { } entity || _entities.Deleted(entity.Owner) ||
                card.StageEntity != entity.Owner)
            {
                view.SetEntity((EntityUid?) null);
                continue;
            }
            var pose = _show == LobbyPartyShow.Parade
                ? LobbyPartyChoreography.March(_elapsed, i, _actors.Count, Size, _reduced, _seed)
                : HumanTargetPose(i);
            view.Scale = new Vector2(_show == LobbyPartyShow.Parade
                ? LobbyPartyChoreography.ActorScale(_actors.Count) * Unit
                : LobbyPartyChoreography.FlybyActorScale(_actors.Count, Size));
            view.OverrideDirection = _reduced ? Direction.South : pose.Backwards ? Direction.West : Direction.East;
            if (_show == LobbyPartyShow.Flyby && !_reduced)
            {
                var aim = GroundTarget(i) - pose.Position;
                view.OverrideDirection = MathF.Abs(aim.Y) > MathF.Abs(aim.X) * 1.5f
                    ? aim.Y < 0 ? Direction.North : Direction.South
                    : aim.X < 0 ? Direction.West : Direction.East;
            }
            _entities.System<SpriteSystem>().SetRotation((entity.Owner, entity.Comp1), new Angle(pose.Rotation));
            var half = new Vector2(50 * Unit);
            view.Measure(half * 2);
            view.Arrange(new UIBox2(pose.Position - half, pose.Position + half));
        }
    }

    private void Sound(string file, float volume)
    {
        if (!SoundEnabled || _reduced)
            return;
        _sounds.RemoveAll(uid => _entities.Deleted(uid));
        var path = file.StartsWith('/') ? file : "/Audio/CMU14/Fighter/" + file;
        if (_entities.System<AudioSystem>().PlayGlobal(path,
                Filter.Local(), false, AudioParams.Default.WithVolume(volume)) is { } stream)
            _sounds.Add(stream.Entity);
    }

    public void StopSounds()
    {
        foreach (var sound in _sounds)
        {
            if (!_entities.Deleted(sound))
                _entities.System<AudioSystem>().Stop(sound);
        }
        _sounds.Clear();
    }

    public void Release()
    {
        if (_released)
            return;
        _released = true;
        StopSounds();
        foreach (var (card, view) in _actors)
        {
            if (view.Entity is { } entity && !_entities.Deleted(entity.Owner))
                _entities.System<SpriteSystem>().SetRotation((entity.Owner, entity.Comp1), Angle.Zero);
            if (card.Parent != null)
                card.SetStageMoving(false);
            view.SetEntity((EntityUid?) null);
        }
        _actors.Clear();
        foreach (var (entity, view) in _flags)
        {
            view.SetEntity((EntityUid?) null);
            if (!_entities.Deleted(entity))
                _entities.DeleteEntity(entity);
        }
        _flags.Clear();
        Visible = false;
        UserInterfaceManager.DeferAction(() =>
        {
            Orphan();
            Dispose();
        });
    }

    private Texture Texture(string path, string state, RsiDirection direction)
    {
        if (!_art.TryGetValue((path, state), out var art))
        {
            art = _entities.System<SpriteSystem>().RsiStateLike(new SpriteSpecifier.Rsi(new ResPath(path), state));
            _art.Add((path, state), art);
        }
        var frame = 0;
        if (!_reduced && art.IsAnimated)
        {
            var length = 0f;
            for (var i = 0; i < art.AnimationFrameCount; i++)
                length += art.GetDelay(i);
            var remaining = length > 0 ? _elapsed % length : 0;
            while (frame < art.AnimationFrameCount - 1 && remaining >= art.GetDelay(frame))
                remaining -= art.GetDelay(frame++);
        }
        return art.GetFrame(art.RsiDirections == RsiDirectionType.Dir1 ? RsiDirection.South : direction, frame);
    }

    private void Sprite(DrawingHandleScreen handle, string path, string state, Vector2 at, float width,
        float rotation = 0, RsiDirection direction = RsiDirection.East, Color? color = null, Vector2? stretch = null)
    {
        var texture = Texture(path, state, direction);
        var half = new Vector2(width, width * texture.Height / texture.Width) / 2;
        var previous = handle.GetTransform();
        handle.SetTransform(Matrix3x2.CreateScale(stretch ?? Vector2.One) *
            Matrix3x2.CreateRotation(rotation) * Matrix3x2.CreateTranslation(at) * previous);
        handle.DrawTextureRect(texture, new UIBox2(-half, half), color ?? Color.White);
        handle.SetTransform(previous);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var previous = handle.GetTransform();
        handle.SetTransform(Matrix3x2.CreateScale(UIScale) * previous);
        var width = Math.Min(Size.X - 32, 940 * Unit);
        var banner = new UIBox2((Size.X - width) / 2, Size.Y * 0.13f - 4, (Size.X + width) / 2, Size.Y * 0.13f + 36);
        handle.DrawRect(banner, Color.FromHex("#07121BF5"));
        handle.DrawLine(banner.BottomLeft, banner.BottomRight, Color.FromHex("#C6AB6C"));
        if (_show == LobbyPartyShow.Flyby)
        {
            DrawBattlefield(handle);
            DrawTargets(handle);
        }
        else
            DrawParade(handle);
        handle.SetTransform(previous);
    }

    private sealed class Foreground(LobbyPartyShowControl owner) : Control
    {
        protected override void Draw(DrawingHandleScreen handle)
        {
            var previous = handle.GetTransform();
            handle.SetTransform(Matrix3x2.CreateScale(UIScale) * previous);
            if (owner._show == LobbyPartyShow.Flyby)
                owner.DrawFlyby(handle);
            else
                owner.DrawConfetti(handle);
            handle.SetTransform(previous);
        }
    }
}
