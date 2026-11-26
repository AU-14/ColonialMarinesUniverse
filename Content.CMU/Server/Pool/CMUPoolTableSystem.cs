using System.Collections.Generic;
using System.Numerics;
using Content.Shared.ActionBlocker;
using Content.Shared.CMU14.Pool;
using Content.Shared.CombatMode;
using Content.Shared.Interaction;
using Content.Shared.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.CMU14.Pool;

public sealed partial class CMUPoolTableSystem : EntitySystem
{
    [Dependency] private ActivatableUISystem _activation = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private SharedCombatModeSystem _combat = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedCMUPoolInteractionSystem _poolInteraction = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    private const float SnapshotInterval = 0.05f;
    private const float MinimumAudibleImpact = 8f;
    private static readonly SoundPathSpecifier StickStrike = new("/Audio/CMU14/Pool/stick_strike.ogg");
    private static readonly SoundPathSpecifier BallClack = new("/Audio/CMU14/Pool/ball_clack.ogg");
    private static readonly SoundPathSpecifier RailThud = new("/Audio/CMU14/Pool/rail_thud.ogg");
    private static readonly SoundPathSpecifier PocketDrop = new("/Audio/CMU14/Pool/pocket_drop.ogg");
    private static readonly AudioParams TableAudio = AudioParams.Default
        .WithReferenceDistance(2f)
        .WithMaxDistance(9f);
    private readonly Dictionary<EntityUid, Session> _games = new();
    private readonly Dictionary<EntityUid, EntityUid> _players = new();
    private readonly List<(EntityUid Table, EntityUid Player)> _playersWithoutStick = new();
    private readonly HashSet<EntityUid> _rolling = new();
    private readonly List<EntityUid> _finished = new();

    private sealed class Session
    {
        public readonly CMUPoolSimulation Game = new();
        public float SnapshotElapsed;
        public float BallImpact;
        public float RailImpact;
        public bool Pocketed;

        public Session()
        {
            Game.Contact += (contact, speed) =>
            {
                switch (contact)
                {
                    case CMUPoolContact.Ball:
                        BallImpact = MathF.Max(BallImpact, speed);
                        break;
                    case CMUPoolContact.Rail:
                        RailImpact = MathF.Max(RailImpact, speed);
                        break;
                    case CMUPoolContact.Pocket:
                        Pocketed = true;
                        break;
                }
            };
        }
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUPoolTableComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CMUPoolTableComponent, InteractUsingEvent>(OnUseStick);
        SubscribeLocalEvent<CMUPoolTableComponent, BoundUIOpenedEvent>(OnOpened);
        SubscribeLocalEvent<CMUPoolTableComponent, BoundUIClosedEvent>(OnClosed);
        SubscribeLocalEvent<CMUPoolTableComponent, ComponentShutdown>(OnShutdown);
        Subs.BuiEvents<CMUPoolTableComponent>(CMUPoolUiKey.FreePlay, subs =>
        {
            subs.Event<CMUPoolMessage>(OnAction);
        });
    }

    private void OnStartup(Entity<CMUPoolTableComponent> ent, ref ComponentStartup args)
    {
        var session = new Session();
        _games.Add(ent.Owner, session);
        Publish(ent, session.Game);
    }

    private void OnUseStick(Entity<CMUPoolTableComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<CMUPoolStickComponent>(args.Used) || _combat.IsInCombatMode(args.User))
            return;

        args.Handled = _activation.InteractUI(args.User, ent.Owner);
    }

    private void OnOpened(Entity<CMUPoolTableComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (!Equals(args.UiKey, CMUPoolUiKey.FreePlay))
            return;

        _players[ent.Owner] = args.Actor;
    }

    private void OnClosed(Entity<CMUPoolTableComponent> ent, ref BoundUIClosedEvent args)
    {
        if (Equals(args.UiKey, CMUPoolUiKey.FreePlay) && _players.TryGetValue(ent.Owner, out var player) && player == args.Actor)
            _players.Remove(ent.Owner);
    }

    private void OnShutdown(Entity<CMUPoolTableComponent> ent, ref ComponentShutdown args)
    {
        _players.Remove(ent.Owner);
        _rolling.Remove(ent.Owner);
        _games.Remove(ent.Owner);
    }

    private void OnAction(Entity<CMUPoolTableComponent> ent, ref CMUPoolMessage args)
    {
        if (args.Revision != ent.Comp.Revision || !_games.TryGetValue(ent.Owner, out var session)
            || !TryComp<ActivatableUIComponent>(ent.Owner, out var ui) || ui.CurrentSingleUser != args.Actor
            || !_poolInteraction.HasActiveStick(args.Actor)
            || !_blocker.CanInteract(args.Actor, ent.Owner) || !_interaction.InRangeUnobstructed(args.Actor, ent.Owner)
            || session.Game.Rolling)
            return;

        var game = session.Game;
        switch (args.Action)
        {
            case CMUPoolAction.Shoot:
                if (!float.IsFinite(args.Power) || args.Power < CMUPoolSimulation.MinimumShotPower || args.Power > 1
                    || !IsFinite(args.Vector)
                    || args.Vector.LengthSquared() is < 0.9f or > 1.1f
                    || game.CueBall.Pocketed || game.Remaining == 0)
                    return;

                game.Shoot(Vector2.Normalize(args.Vector), args.Power);
                _audio.PlayPvs(StickStrike, ent.Owner, TableAudio.WithVolume(ImpactVolume(game.CueBall.Velocity.Length()) - 2f));
                session.SnapshotElapsed = 0;
                _rolling.Add(ent.Owner);
                break;
            case CMUPoolAction.PlaceCueBall:
                if (!IsFinite(args.Vector) || !game.TryPlaceCueBall(args.Vector))
                    return;

                break;
            case CMUPoolAction.Rack:
                game.Rack();
                break;
            default:
                return;
        }

        ent.Comp.Revision++;
        Publish(ent, game);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        foreach (var (table, player) in _players)
        {
            if (!_poolInteraction.HasActiveStick(player))
                _playersWithoutStick.Add((table, player));
        }

        foreach (var (table, player) in _playersWithoutStick)
        {
            _players.Remove(table);
            _ui.CloseUi(table, CMUPoolUiKey.FreePlay, player);
        }
        _playersWithoutStick.Clear();

        foreach (var uid in _rolling)
        {
            if (!TryComp<CMUPoolTableComponent>(uid, out var component) || !_games.TryGetValue(uid, out var session))
            {
                _finished.Add(uid);
                continue;
            }

            var game = session.Game;
            game.Update(frameTime);
            PlayContacts(uid, session);
            session.SnapshotElapsed += frameTime;
            if (game.Rolling && session.SnapshotElapsed < SnapshotInterval)
                continue;

            session.SnapshotElapsed %= SnapshotInterval;
            if (!game.Rolling)
            {
                component.Revision++;
                _finished.Add(uid);
            }

            Publish((uid, component), game);
        }

        foreach (var uid in _finished)
            _rolling.Remove(uid);
        _finished.Clear();
    }

    private void PlayContacts(EntityUid table, Session session)
    {

        if (session.BallImpact >= MinimumAudibleImpact)
            _audio.PlayPvs(BallClack, table, TableAudio.WithVolume(ImpactVolume(session.BallImpact)));
        if (session.RailImpact >= MinimumAudibleImpact)
            _audio.PlayPvs(RailThud, table, TableAudio.WithVolume(ImpactVolume(session.RailImpact) - 8f));
        if (session.Pocketed)
            _audio.PlayPvs(PocketDrop, table, TableAudio.WithVolume(-10f));

        session.BallImpact = 0;
        session.RailImpact = 0;
        session.Pocketed = false;
    }

    private static float ImpactVolume(float speed)
        => -6f + 20f * MathF.Log10(Math.Clamp(speed / 900f, 0.025f, 1f));

    private void Publish(Entity<CMUPoolTableComponent> ent, CMUPoolSimulation game)
    {
        var positions = new Vector2[CMUPoolSimulation.BallCount];
        var pocketed = 0;
        for (var i = 0; i < positions.Length; i++)
        {
            positions[i] = game.Balls[i].Position;
            if (game.Balls[i].Pocketed)
                pocketed |= 1 << i;
        }

        ent.Comp.Positions = positions;
        ent.Comp.PocketedMask = pocketed;
        ent.Comp.Rolling = game.Rolling;
        Dirty(ent.Owner, ent.Comp);
    }

    private static bool IsFinite(Vector2 vector)
        => float.IsFinite(vector.X) && float.IsFinite(vector.Y);
}
