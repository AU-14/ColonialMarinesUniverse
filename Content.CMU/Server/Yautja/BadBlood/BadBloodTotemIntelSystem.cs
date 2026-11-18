using Content.Shared._RMC14.Chat;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Rules;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// FLOW-506, reading a Totem: a marine at a desecrated Clan Totem, with no living Bad Blood near it, can read its
/// markings once per desecration for the grid square the Lodge stands in, how many ripe Totems the clan holds, and
/// how long is left of the ripe-Totem hold if one is running. Reconsecration makes it readable again.
/// </summary>
public sealed partial class BadBloodTotemIntelSystem : EntitySystem
{
    [Dependency] private BadBloodSystem _badBlood = default!;
    [Dependency] private SharedCMChatSystem _cmChat = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private RMCPlanetSystem _planet = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private BadBloodCampRuleSystem _rule = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private BadBloodTotemSystem _totems = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodTotemReadableComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<BadBloodTotemReadableComponent, BadBloodTotemReadDoAfterEvent>(OnReadDoAfter);
        SubscribeLocalEvent<BadBloodTotemReconsecratedEvent>(OnReconsecrated);
    }

    private void OnGetVerbs(Entity<BadBloodTotemReadableComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || GetReadDenial(args.User, ent.Owner) != null)
            return;

        var user = args.User;
        var totem = ent.Owner;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-bad-blood-totem-read-verb"),
            Act = () => TryStartRead(user, totem),
        });
    }

    /// <summary>
    /// Why <paramref name="user"/> cannot read <paramref name="totem"/> now, as a loc key, or null when they can.
    /// Only marines who are not Bad Bloods may read, and only a counted, desecrated Totem with no living Bad Blood
    /// near that has not been read since it was desecrated.
    /// </summary>
    public string? GetReadDenial(EntityUid user, EntityUid totem)
    {
        if (!HasComp<MarineComponent>(user) || _badBlood.IsBadBlood(user))
            return "cmu-bad-blood-totem-read-consecrated";

        if (!TryComp(totem, out BadBloodTotemReadableComponent? readable) ||
            !TryComp(totem, out BadBloodTotemComponent? comp) ||
            !comp.Registered ||
            comp.Destroyed ||
            !comp.Desecrated)
        {
            return "cmu-bad-blood-totem-read-consecrated";
        }

        if (readable.ReadThisDesecration)
            return "cmu-bad-blood-totem-read-already";

        if (_totems.IsBadBloodNear(new Entity<BadBloodTotemComponent>(totem, comp)))
            return "cmu-bad-blood-totem-read-hunter-near";

        return null;
    }

    /// <summary>
    /// Starts the read do-after. The user is told why when it is refused.
    /// </summary>
    public bool TryStartRead(EntityUid user, EntityUid totem)
    {
        if (!TryComp(totem, out BadBloodTotemReadableComponent? readable))
            return false;

        if (GetReadDenial(user, totem) is { } denial)
        {
            _popup.PopupEntity(Loc.GetString(denial), user, user, PopupType.SmallCaution);
            return false;
        }

        var doAfter = new DoAfterArgs(EntityManager, user, readable.ReadTime, new BadBloodTotemReadDoAfterEvent(), totem, totem)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-totem-read-start"), user, user);
        return true;
    }

    private void OnReadDoAfter(Entity<BadBloodTotemReadableComponent> ent, ref BadBloodTotemReadDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        TryCompleteRead(args.User, ent.Owner);
    }

    /// <summary>
    /// Finishes a read at once: the conditions are checked again, the reader is told the Lodge's square, the ripe
    /// count and the hold, the desecration is marked read and <see cref="BadBloodTotemReadEvent"/> is raised.
    /// </summary>
    public bool TryCompleteRead(EntityUid user, EntityUid totem)
    {
        if (!TryComp(totem, out BadBloodTotemReadableComponent? readable))
            return false;

        if (GetReadDenial(user, totem) is { } denial)
        {
            _popup.PopupEntity(Loc.GetString(denial), user, user, PopupType.SmallCaution);
            return false;
        }

        readable.ReadThisDesecration = true;

        foreach (var line in GetReadReport(readable.GridSize))
        {
            _cmChat.ChatMessageToOne(line, user);
            _popup.PopupEntity(line, user, user, PopupType.Medium);
        }

        var ev = new BadBloodTotemReadEvent(totem, user);
        RaiseLocalEvent(ref ev);
        return true;
    }

    /// <summary>
    /// The lowest corner of the <paramref name="gridSize"/>-tile square the standing Lodge is in, in the
    /// coordinates the rangefinder and mortars use. False without a Lodge.
    /// </summary>
    public bool TryGetLodgeSquare(int gridSize, out Vector2i corner)
    {
        return TryGetLodgeSquare(gridSize, out corner, out _);
    }

    /// <summary>
    /// <see cref="TryGetLodgeSquare(int, out Vector2i)"/>, also giving the tile the Lodge stands on in the same
    /// coordinates, so a caller can place the square relative to the Lodge (FLOW-606, the marine map region).
    /// </summary>
    public bool TryGetLodgeSquare(int gridSize, out Vector2i corner, out Vector2i lodgeTile)
    {
        corner = default;
        lodgeTile = default;
        if (_rule.GetLodge() is not { } lodge || TerminatingOrDeleted(lodge))
            return false;

        var coords = _transform.GetMapCoordinates(lodge);
        var position = coords.Position.Floored();
        if (_planet.TryGetOffset(coords, out var offset))
            position += offset;

        var size = Math.Max(1, gridSize);
        lodgeTile = position;
        corner = new Vector2i(
            (int) Math.Floor(position.X / (double) size) * size,
            (int) Math.Floor(position.Y / (double) size) * size);
        return true;
    }

    /// <summary>
    /// What a read tells the reader, one line per fact.
    /// </summary>
    public List<string> GetReadReport(int gridSize)
    {
        var lines = new List<string>();
        var size = Math.Max(1, gridSize);

        if (TryGetLodgeSquare(size, out var corner))
        {
            lines.Add(Loc.GetString("cmu-bad-blood-totem-read-lodge",
                ("x1", corner.X),
                ("x2", corner.X + size - 1),
                ("y1", corner.Y),
                ("y2", corner.Y + size - 1)));
        }
        else
        {
            lines.Add(Loc.GetString("cmu-bad-blood-totem-read-no-lodge"));
        }

        if (!_rule.TryGetRule(out var rule))
            return lines;

        lines.Add(Loc.GetString("cmu-bad-blood-totem-read-ripe",
            ("ripe", _totems.CountRipeTotems()),
            ("needed", rule.Comp.RipeTotemsForCall)));

        if (rule.Comp.ShipState == BadBloodShipState.None && rule.Comp.RipeHoldStartedAt is { } since)
        {
            var callAt = since + rule.Comp.RipeHoldForCall;
            var earliest = rule.Comp.StartedAt + rule.Comp.ShipEarliestCall;
            if (earliest > callAt)
                callAt = earliest;

            var left = callAt - _timing.CurTime;
            if (left < TimeSpan.Zero)
                left = TimeSpan.Zero;

            lines.Add(Loc.GetString("cmu-bad-blood-totem-read-hold", ("minutes", (int) Math.Ceiling(left.TotalMinutes))));
        }

        return lines;
    }

    private void OnReconsecrated(ref BadBloodTotemReconsecratedEvent args)
    {
        if (TryComp(args.Totem, out BadBloodTotemReadableComponent? readable))
            readable.ReadThisDesecration = false;
    }
}
