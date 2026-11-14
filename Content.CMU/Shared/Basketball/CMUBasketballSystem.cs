using Content.Shared._RMC14.Dialog;
using Content.Shared.Administration.Logs;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.Basketball;

/// <summary>
///     Hoops and reset buttons drive the scoreboards they are linked to with a multitool.
///     Device links only exist on the server, so scoring is server-authoritative.
/// </summary>
public sealed partial class CMUBasketballSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedChatSystem _chat = default!;
    [Dependency] private SharedDeviceLinkSystem _deviceLink = default!;
    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly List<Entity<CMUBasketballScoreboardComponent>> _scoreboards = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUBasketballHoopComponent, ComponentInit>(OnHoopInit);
        SubscribeLocalEvent<CMUBasketballHoopComponent, StartCollideEvent>(OnHoopStartCollide);
        SubscribeLocalEvent<CMUBasketballHoopComponent, InteractUsingEvent>(OnHoopInteractUsing);

        SubscribeLocalEvent<CMUBasketballScoreboardComponent, ComponentInit>(OnScoreboardInit);
        SubscribeLocalEvent<CMUBasketballScoreboardComponent, GetVerbsEvent<Verb>>(OnScoreboardGetVerbs);
        SubscribeLocalEvent<CMUBasketballScoreboardComponent, CMUBasketballTeamNameEvent>(OnScoreboardTeamName);
        SubscribeLocalEvent<CMUBasketballScoreboardComponent, CMUBasketballTargetScoreEvent>(OnScoreboardTargetScore);
        SubscribeLocalEvent<CMUBasketballScoreboardComponent, ExaminedEvent>(OnScoreboardExamined);

        SubscribeLocalEvent<CMUBasketballResetComponent, ComponentInit>(OnResetInit);
        SubscribeLocalEvent<CMUBasketballResetComponent, InteractHandEvent>(OnResetInteractHand);
    }

    private void OnHoopInit(Entity<CMUBasketballHoopComponent> ent, ref ComponentInit args)
    {
        _deviceLink.EnsureSourcePorts(ent, ent.Comp.ScorePort);
    }

    private void OnScoreboardInit(Entity<CMUBasketballScoreboardComponent> ent, ref ComponentInit args)
    {
        _deviceLink.EnsureSinkPorts(ent, ent.Comp.ScorePort, ent.Comp.ResetPort);
    }

    private void OnScoreboardGetVerbs(Entity<CMUBasketballScoreboardComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        var locked = IsGameInProgress(ent);
        var lockedMessage = locked ? Loc.GetString("cmu-basketball-locked") : null;
        var icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/settings.svg.192dpi.png"));

        foreach (var side in new[] { CMUBasketballTeam.Left, CMUBasketballTeam.Right })
        {
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("cmu-basketball-verb-name", ("side", SideArg(side))),
                Icon = icon,
                Disabled = locked,
                Message = lockedMessage,
                Act = () => OpenTeamNameDialog(ent, user, side),
            });
        }

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("cmu-basketball-verb-target"),
            Icon = icon,
            Disabled = locked,
            Message = lockedMessage,
            Act = () => OpenTargetScoreDialog(ent, user),
        });
    }

    private void OpenTeamNameDialog(Entity<CMUBasketballScoreboardComponent> ent, EntityUid user, CMUBasketballTeam side)
    {
        // The dialog state is owned by the server; opening it during client prediction would desync it.
        if (_net.IsClient || !CheckUnlocked(ent, user))
            return;

        _dialog.OpenInput(
            ent,
            user,
            Loc.GetString("cmu-basketball-name-prompt", ("side", SideArg(side))),
            new CMUBasketballTeamNameEvent(GetNetEntity(user), side),
            characterLimit: ent.Comp.TeamNameLimit,
            title: Loc.GetString("cmu-basketball-name-title"));
    }

    private void OpenTargetScoreDialog(Entity<CMUBasketballScoreboardComponent> ent, EntityUid user)
    {
        if (_net.IsClient || !CheckUnlocked(ent, user))
            return;

        var netUser = GetNetEntity(user);
        var options = new List<DialogOption>();
        foreach (var score in ent.Comp.TargetScoreOptions)
        {
            options.Add(new DialogOption(GetTargetText(score), new CMUBasketballTargetScoreEvent(netUser, score)));
        }

        options.Add(new DialogOption(GetTargetText(0), new CMUBasketballTargetScoreEvent(netUser, 0)));
        _dialog.OpenOptions(ent, user, Loc.GetString("cmu-basketball-target-title"), options);
    }

    private void OnScoreboardTeamName(Entity<CMUBasketballScoreboardComponent> ent, ref CMUBasketballTeamNameEvent args)
    {
        // The score can change while the dialog is open, so the lock is checked again on submit.
        if (_net.IsClient || !TryGetEntity(args.User, out var user) || !CheckUnlocked(ent, user.Value))
            return;

        SetTeamName(ent, args.Side, args.Message);

        var name = GetTeamName(ent, args.Side);
        _popup.PopupEntity(
            Loc.GetString("cmu-basketball-name-set", ("side", SideArg(args.Side)), ("name", name)),
            ent,
            user.Value);
        _adminLog.Add(LogType.Action,
            LogImpact.Low,
            $"{ToPrettyString(user.Value):player} named the {args.Side} team of {ToPrettyString(ent):entity} {name}");
    }

    private void OnScoreboardTargetScore(Entity<CMUBasketballScoreboardComponent> ent, ref CMUBasketballTargetScoreEvent args)
    {
        if (_net.IsClient || !TryGetEntity(args.User, out var user) || !CheckUnlocked(ent, user.Value))
            return;

        // Only accept the scores the menu offered.
        if (args.Score != 0 && !ent.Comp.TargetScoreOptions.Contains(args.Score))
            return;

        ent.Comp.TargetScore = args.Score;
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("cmu-basketball-target-set", ("target", GetTargetText(args.Score))),
            ent,
            user.Value);
    }

    private void OnScoreboardExamined(Entity<CMUBasketballScoreboardComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(CMUBasketballScoreboardComponent)))
        {
            args.PushMarkup(Loc.GetString("cmu-basketball-scoreboard-examine",
                ("left", FormattedMessage.EscapeText(GetTeamName(ent, CMUBasketballTeam.Left))),
                ("leftScore", ent.Comp.LeftScore),
                ("right", FormattedMessage.EscapeText(GetTeamName(ent, CMUBasketballTeam.Right))),
                ("rightScore", ent.Comp.RightScore)));

            if (ent.Comp.Winner is { } winner)
            {
                args.PushMarkup(Loc.GetString("cmu-basketball-scoreboard-examine-winner",
                    ("team", FormattedMessage.EscapeText(GetTeamName(ent, winner)))));
            }
            else
            {
                args.PushMarkup(Loc.GetString("cmu-basketball-scoreboard-examine-target",
                    ("target", GetTargetText(ent.Comp.TargetScore))));
            }
        }
    }

    private void OnResetInit(Entity<CMUBasketballResetComponent> ent, ref ComponentInit args)
    {
        _deviceLink.EnsureSourcePorts(ent, ent.Comp.ResetPort);
    }

    private void OnHoopStartCollide(Entity<CMUBasketballHoopComponent> ent, ref StartCollideEvent args)
    {
        if (_net.IsClient || args.OurFixtureId != ent.Comp.SensorFixtureId)
            return;

        TryScoreThrownItem(ent, args.OtherEntity);
    }

    private void OnHoopInteractUsing(Entity<CMUBasketballHoopComponent> ent, ref InteractUsingEvent args)
    {
        // A multitool has to reach the device linking interaction instead of being dunked.
        if (_net.IsClient ||
            args.Handled ||
            !CanScoreWith(args.Used) ||
            HasComp<NetworkConfiguratorComponent>(args.Used))
        {
            return;
        }

        args.Handled = true;

        if (!_hands.TryDrop(args.User, args.Used, Transform(ent).Coordinates))
            return;

        TryDunkItem(ent, args.Used);
    }

    private void OnResetInteractHand(Entity<CMUBasketballResetComponent> ent, ref InteractHandEvent args)
    {
        if (_net.IsClient || args.Handled)
            return;

        args.Handled = true;

        if (_timing.CurTime < ent.Comp.NextResetAt)
        {
            _popup.PopupEntity(Loc.GetString("cmu-basketball-reset-cooldown"), ent, args.User, PopupType.SmallCaution);
            return;
        }

        if (!TryResetCourt(ent))
        {
            _popup.PopupEntity(Loc.GetString("cmu-basketball-reset-unlinked"), ent, args.User, PopupType.SmallCaution);
            return;
        }

        _popup.PopupEntity(Loc.GetString("cmu-basketball-reset"), ent, args.User);
    }

    public bool TryScoreThrownItem(Entity<CMUBasketballHoopComponent> hoop, EntityUid item)
    {
        if (_net.IsClient ||
            !CanScoreWith(item) ||
            !TryComp(item, out ThrownItemComponent? thrown))
        {
            return false;
        }

        var attempt = EnsureComp<CMUBasketballShotAttemptComponent>(item);
        if (attempt.Attempted && attempt.ThrownTime == thrown.ThrownTime)
            return false;

        attempt.Attempted = true;
        attempt.ThrownTime = thrown.ThrownTime;

        if (!_random.Prob(Math.Clamp(hoop.Comp.ShotChance, 0, 1)))
        {
            _popup.PopupEntity(Loc.GetString("cmu-basketball-miss"), hoop, PopupType.MediumCaution);
            return false;
        }

        Score(hoop, hoop.Comp.ShotPoints, false);
        return true;
    }

    public bool TryDunkItem(Entity<CMUBasketballHoopComponent> hoop, EntityUid item)
    {
        if (_net.IsClient || !CanScoreWith(item))
            return false;

        Score(hoop, hoop.Comp.DunkPoints, true);
        return true;
    }

    /// <summary>
    ///     Zeroes every scoreboard linked to the button. Returns false while on cooldown or when nothing is linked.
    /// </summary>
    public bool TryResetCourt(Entity<CMUBasketballResetComponent> reset)
    {
        if (_net.IsClient || _timing.CurTime < reset.Comp.NextResetAt)
            return false;

        GetLinkedScoreboards(reset, reset.Comp.ResetPort, true);
        if (_scoreboards.Count == 0)
            return false;

        foreach (var scoreboard in _scoreboards)
        {
            scoreboard.Comp.LeftScore = 0;
            scoreboard.Comp.RightScore = 0;
            scoreboard.Comp.Winner = null;
            Dirty(scoreboard);
        }

        var time = _timing.CurTime;
        reset.Comp.NextResetAt = time + reset.Comp.ResetCooldown;
        reset.Comp.PressedUntil = time + reset.Comp.PressedDuration;
        reset.Comp.Pressed = true;
        Dirty(reset);
        return true;
    }

    /// <summary>
    ///     Renames one side of a scoreboard. A blank name goes back to the default "Left" or "Right".
    /// </summary>
    public void SetTeamName(Entity<CMUBasketballScoreboardComponent> scoreboard, CMUBasketballTeam side, string? name)
    {
        name = SanitizeTeamName(name, scoreboard.Comp.TeamNameLimit);
        if (side == CMUBasketballTeam.Left)
            scoreboard.Comp.LeftTeamName = name;
        else
            scoreboard.Comp.RightTeamName = name;

        Dirty(scoreboard);
    }

    public string GetTeamName(Entity<CMUBasketballScoreboardComponent> scoreboard, CMUBasketballTeam side)
    {
        var name = side == CMUBasketballTeam.Left ? scoreboard.Comp.LeftTeamName : scoreboard.Comp.RightTeamName;
        return name ?? GetDefaultTeamName(side);
    }

    /// <summary>
    ///     Names and the winning score are locked from the first basket until the game is won or reset,
    ///     so nobody can rename a team or move the goal mid-game.
    /// </summary>
    public bool IsGameInProgress(Entity<CMUBasketballScoreboardComponent> scoreboard)
    {
        return scoreboard.Comp.Winner == null &&
               (scoreboard.Comp.LeftScore > 0 || scoreboard.Comp.RightScore > 0);
    }

    private bool CheckUnlocked(Entity<CMUBasketballScoreboardComponent> scoreboard, EntityUid user)
    {
        if (!IsGameInProgress(scoreboard))
            return true;

        _popup.PopupEntity(Loc.GetString("cmu-basketball-locked"), scoreboard, user, PopupType.SmallCaution);
        return false;
    }

    private string GetTargetText(int score)
    {
        return score > 0
            ? Loc.GetString("cmu-basketball-target-score", ("score", score))
            : Loc.GetString("cmu-basketball-target-none");
    }

    /// <summary>
    ///     Locale selector for the side, so each language can inflect "left"/"right" itself.
    /// </summary>
    private static string SideArg(CMUBasketballTeam side)
    {
        return side == CMUBasketballTeam.Left ? "left" : "right";
    }

    private string GetDefaultTeamName(CMUBasketballTeam side)
    {
        return Loc.GetString(side == CMUBasketballTeam.Left
            ? "cmu-basketball-side-left"
            : "cmu-basketball-side-right");
    }

    private static string? SanitizeTeamName(string? name, int limit)
    {
        if (name == null)
            return null;

        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (char.IsControl(chars[i]))
                chars[i] = ' ';
        }

        var clean = new string(chars).Trim();
        if (limit > 0 && clean.Length > limit)
            clean = clean[..limit].TrimEnd();

        return clean.Length == 0 ? null : clean;
    }

    private void Score(Entity<CMUBasketballHoopComponent> hoop, int points, bool dunk)
    {
        var side = hoop.Comp.Side;
        var key = dunk ? "cmu-basketball-dunk" : "cmu-basketball-shot";

        GetLinkedScoreboards(hoop, hoop.Comp.ScorePort, false);
        if (_scoreboards.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString(key, ("team", GetDefaultTeamName(side)), ("points", points)),
                hoop,
                PopupType.Medium);
            return;
        }

        foreach (var scoreboard in _scoreboards)
        {
            // A finished game ignores baskets until someone presses reset.
            if (!AddScore(scoreboard, side, points))
                continue;

            _audio.PlayPvs(scoreboard.Comp.BuzzerSound, scoreboard);

            var team = GetTeamName(scoreboard, side);
            string message;
            if (scoreboard.Comp.Winner == side)
            {
                var other = side == CMUBasketballTeam.Left ? CMUBasketballTeam.Right : CMUBasketballTeam.Left;
                message = Loc.GetString("cmu-basketball-win",
                    ("team", team),
                    ("score", GetScore(scoreboard, side)),
                    ("otherScore", GetScore(scoreboard, other)));
            }
            else
            {
                message = Loc.GetString(key, ("team", team), ("points", points));
            }

            // Spoken like a player, so it shows as a speech bubble and in the chat log.
            _chat.TrySendInGameICMessage(scoreboard,
                message,
                InGameICChatType.Speak,
                ChatTransmitRange.Normal,
                hideLog: true,
                checkRadioPrefix: false,
                ignoreActionBlocker: true);
        }
    }

    private static int GetScore(Entity<CMUBasketballScoreboardComponent> scoreboard, CMUBasketballTeam side)
    {
        return side == CMUBasketballTeam.Left ? scoreboard.Comp.LeftScore : scoreboard.Comp.RightScore;
    }

    /// <summary>
    ///     Adds points to one side and declares it the winner once it reaches the target score.
    ///     Returns false when the game is already over.
    /// </summary>
    private bool AddScore(Entity<CMUBasketballScoreboardComponent> scoreboard, CMUBasketballTeam side, int points)
    {
        if (points <= 0 || scoreboard.Comp.Winner != null)
            return false;

        var max = Math.Clamp(scoreboard.Comp.MaxScore, 1, 99);
        var score = Math.Min(GetScore(scoreboard, side) + points, max);
        if (side == CMUBasketballTeam.Left)
            scoreboard.Comp.LeftScore = score;
        else
            scoreboard.Comp.RightScore = score;

        if (scoreboard.Comp.TargetScore > 0 && score >= scoreboard.Comp.TargetScore)
            scoreboard.Comp.Winner = side;

        Dirty(scoreboard);
        return true;
    }

    /// <summary>
    ///     Fills <see cref="_scoreboards"/> with the scoreboards whose score or reset sink is linked to
    ///     <paramref name="sourcePort"/>, so a hoop linked to some other device port does nothing to it.
    /// </summary>
    private void GetLinkedScoreboards(EntityUid source, ProtoId<SourcePortPrototype> sourcePort, bool reset)
    {
        _scoreboards.Clear();
        foreach (var sink in _deviceLink.GetLinkedSinks(source, sourcePort))
        {
            if (!TryComp(sink, out CMUBasketballScoreboardComponent? scoreboard))
                continue;

            var sinkPort = reset ? scoreboard.ResetPort : scoreboard.ScorePort;
            if (!_deviceLink.GetLinks(source, sink).Contains((sourcePort, sinkPort)))
                continue;

            _scoreboards.Add((sink, scoreboard));
        }
    }

    private bool CanScoreWith(EntityUid item)
    {
        return HasComp<ItemComponent>(item) && !HasComp<ProjectileComponent>(item);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUBasketballResetComponent>();
        while (query.MoveNext(out var uid, out var reset))
        {
            if (!reset.Pressed || time < reset.PressedUntil)
                continue;

            reset.Pressed = false;
            Dirty(uid, reset);
        }
    }
}
