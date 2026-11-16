using System.Diagnostics.CodeAnalysis;
using Content.Shared._RMC14.Communications;
using Content.Shared._RMC14.Sensor;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Yautja.BadBlood;

// ARCH-504: uplink sites and the uplink. Every planet sensor and comms tower is a site; the field relay prototype
// carries the component itself. The uplink needs the gate and a signature, and it latches
public sealed partial class BadBloodObjectiveChainSystem
{
    /// <summary>
    /// The AU-14 GOVFOR command channel (Content.CMU/Resources/Prototypes/CMU14/Roles/radio_channels.yml).
    /// </summary>
    public static readonly ProtoId<RadioChannelPrototype> GovforCommandChannel = "radioGovforCommand";

    private void InitializeUplink()
    {
        // C2 hosts the "Re-route lock" AlternativeVerb on the same component, so the uplink uses InteractionVerb
        SubscribeLocalEvent<BadBloodUplinkSiteComponent, GetVerbsEvent<InteractionVerb>>(OnSiteVerbs);
        SubscribeLocalEvent<BadBloodUplinkSiteComponent, BadBloodUplinkDoAfterEvent>(OnUplinkDoAfter);
    }

    // makes every planet sensor and comms tower an uplink site. Cheap: there are a handful of towers on a map
    private void SweepSites()
    {
        _siteScratch.Clear();

        var sensors = EntityQueryEnumerator<SensorTowerComponent, TransformComponent>();
        while (sensors.MoveNext(out var uid, out _, out var xform))
        {
            if (!HasComp<BadBloodUplinkSiteComponent>(uid) && _planet.IsOnPlanetLevel(xform))
                _siteScratch.Add((uid, BadBloodUplinkSiteKind.Sensor));
        }

        var comms = EntityQueryEnumerator<CommunicationsTowerComponent, TransformComponent>();
        while (comms.MoveNext(out var uid, out _, out var xform))
        {
            if (!HasComp<BadBloodUplinkSiteComponent>(uid) && _planet.IsOnPlanetLevel(xform))
                _siteScratch.Add((uid, BadBloodUplinkSiteKind.Comms));
        }

        foreach (var (uid, kind) in _siteScratch)
        {
            if (TerminatingOrDeleted(uid))
                continue;

            // the component's default UplinkTime (90 s) is the tower time; the relay prototype sets its own
            var site = EnsureComp<BadBloodUplinkSiteComponent>(uid);
            site.Kind = kind;
        }

        _siteScratch.Clear();
    }

    private void OnSiteVerbs(Entity<BadBloodUplinkSiteComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !IsMarine(args.User) ||
            !TryGetChain(out var chain) || chain.Comp.Uplinked)
        {
            return;
        }

        var user = args.User;
        var site = ent.Owner;
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("cmu-bad-blood-obj-uplink-verb"),
            Message = Loc.GetString("cmu-bad-blood-obj-uplink-verb-message"),
            Act = () => TryStartUplink(user, site),
        });
    }

    /// <summary>
    /// Whether <paramref name="user"/> may run the uplink at <paramref name="site"/> now. <paramref name="reason"/> is
    /// the locale id of the refusal.
    /// </summary>
    public bool CanUplink(EntityUid user, EntityUid site, [NotNullWhen(false)] out string? reason)
    {
        reason = null;
        if (!TryGetChain(out var chain))
        {
            reason = "cmu-bad-blood-obj-no-rule";
            return false;
        }

        if (!IsMarine(user))
        {
            reason = "cmu-bad-blood-obj-marines-only";
            return false;
        }

        if (chain.Comp.Uplinked)
        {
            reason = "cmu-bad-blood-obj-uplink-done";
            return false;
        }

        if (!chain.Comp.GateMet)
        {
            reason = "cmu-bad-blood-obj-uplink-no-gate";
            return false;
        }

        if (chain.Comp.Signature == BadBloodSignature.None)
        {
            reason = "cmu-bad-blood-obj-uplink-no-signature";
            return false;
        }

        if (!IsSiteWorking(site))
        {
            reason = "cmu-bad-blood-obj-site-not-working";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Starts the uplink do-after at a working site. Tells <paramref name="user"/> why when it is refused.
    /// </summary>
    public bool TryStartUplink(EntityUid user, EntityUid site)
    {
        if (!CanUplink(user, site, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason), user, user, PopupType.SmallCaution);
            return false;
        }

        var comp = Comp<BadBloodUplinkSiteComponent>(site);
        var doAfter = new DoAfterArgs(EntityManager, user, comp.UplinkTime, new BadBloodUplinkDoAfterEvent(), site,
            target: site)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-obj-uplink-start",
            ("seconds", (int) Math.Ceiling(comp.UplinkTime.TotalSeconds))), user, user);
        return true;
    }

    private void OnUplinkDoAfter(Entity<BadBloodUplinkSiteComponent> ent, ref BadBloodUplinkDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        var user = args.User;

        // the site can have been sabotaged, or another team finished first, while the do-after ran
        if (!CanUplink(user, ent.Owner, out var reason) || !TryGetChain(out var chain))
        {
            _popup.PopupEntity(Loc.GetString(reason ?? "cmu-bad-blood-obj-no-rule"), user, user, PopupType.SmallCaution);
            return;
        }

        chain.Comp.Uplinked = true;
        chain.Comp.LockSite = ent.Owner;

        var kind = ent.Comp.Kind.ToString();
        var stage = new BadBloodObjectiveStageEvent(BadBloodObjectiveStage.Uplink, kind, ent.Owner);
        RaiseLocalEvent(ref stage);

        var message = Loc.GetString("cmu-bad-blood-obj-uplink-done-announce",
            ("site", SiteKindName(ent.Comp.Kind)),
            ("area", _area.GetAreaName(ent.Owner)));
        _marineAnnounce.AnnounceARES(null, message, faction: GovforAnnounceFaction);
        _marineAnnounce.AnnounceRadio(ent.Owner, message, GovforCommandChannel);

        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-obj-uplink-finished"), user, user, PopupType.Medium);
        _adminLog.Add(LogType.Action, LogImpact.High,
            $"BadBlood obj: {ToPrettyString(user):player} ran the uplink at {ToPrettyString(ent.Owner):site} ({kind}); it is the lock site");
    }
}
