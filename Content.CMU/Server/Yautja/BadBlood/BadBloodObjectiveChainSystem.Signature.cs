using System.Diagnostics.CodeAnalysis;
using Content.Shared._RMC14.Communications;
using Content.Shared._RMC14.Sensor;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Yautja.BadBlood;

// FLOW-511: the three signature avenues. The first one to finish latches the signature; later ones do nothing.
//   B1 bracer: a looted Bad Blood bracer put into a working uplink site, then cut, then wrenched
//   B2 Totem:  a marine surveys a desecrated Totem (verb + do-after)
//   B3 Lodge:  survey the revealed Lodge, or recover the clan star chart
public sealed partial class BadBloodObjectiveChainSystem
{
    // the bracer tinker stages kept on BadBloodUplinkSiteComponent.BracerStage
    private const int StageEmpty = 0;
    private const int StageInserted = 1;
    private const int StageCut = 2;
    private const int StageDone = 3;

    // no multitool / Pulsing: the multitool opens the comms tower's frequency dialog
    private static readonly ProtoId<ToolQualityPrototype> CuttingQuality = "Cutting";
    private static readonly ProtoId<ToolQualityPrototype> AnchoringQuality = "Anchoring";

    private void InitializeSignature()
    {
        // ahead of the RMC tower handlers, which react to tools on a tower
        SubscribeLocalEvent<BadBloodUplinkSiteComponent, InteractUsingEvent>(OnSiteInteractUsing,
            before: new[] { typeof(SensorTowerSystem), typeof(CommunicationsTowerSystem) });
        SubscribeLocalEvent<BadBloodUplinkSiteComponent, BadBloodBracerTinkerDoAfterEvent>(OnTinkerDoAfter);

        SubscribeLocalEvent<BadBloodCampComponent, GetVerbsEvent<Verb>>(OnLodgeVerbs);
        SubscribeLocalEvent<BadBloodTotemReadableComponent, GetVerbsEvent<Verb>>(OnTotemSurveyVerbs);
        SubscribeLocalEvent<BadBloodTotemReadableComponent, BadBloodTotemSurveyDoAfterEvent>(OnTotemSurveyDoAfter);
        SubscribeLocalEvent<BadBloodCampComponent, BadBloodLodgeSurveyDoAfterEvent>(OnSurveyDoAfter);

        SubscribeLocalEvent<BadBloodStarChartRecoveredEvent>(OnStarChartRecovered);
    }

    /// <summary>
    /// Latches the signature with <paramref name="avenue"/> if none is latched yet. False if one already was.
    /// </summary>
    private bool TryLatchSignature(BadBloodSignature avenue, EntityUid? site, EntityUid? user)
    {
        if (!TryGetChain(out var chain) || chain.Comp.Signature != BadBloodSignature.None)
            return false;

        chain.Comp.Signature = avenue;
        EjectStrandedBracers();

        var detail = avenue.ToString();
        var stage = new BadBloodObjectiveStageEvent(BadBloodObjectiveStage.Signature, detail, site);
        RaiseLocalEvent(ref stage);

        _marineAnnounce.AnnounceToMarines(
            Loc.GetString("cmu-bad-blood-obj-signature",
                ("avenue", Loc.GetString($"cmu-bad-blood-obj-avenue-{detail.ToLowerInvariant()}"))),
            faction: GovforAnnounceFaction);

        var by = user is { } who ? ToPrettyString(who).ToString() : "no one";
        _adminLog.Add(LogType.Action, LogImpact.Medium, $"BadBlood obj: clan signature acquired via {detail} by {by}");
        return true;
    }

    #region B1 bracer

    /// <summary>
    /// Any Bad Blood bracer: the scavenged bracer and its children (the Leader's bracer) set
    /// <see cref="YautjaBracerComponent.BadBlood"/>. Honourable bracers do not count.
    /// </summary>
    public bool IsBadBloodBracer(EntityUid uid)
    {
        return TryComp(uid, out YautjaBracerComponent? bracer) && bracer.BadBlood;
    }

    private void OnSiteInteractUsing(Entity<BadBloodUplinkSiteComponent> ent, ref InteractUsingEvent args)
    {
        // NEW-5: once a signature is latched nothing here is ours any more; leave every use to the RMC handlers
        if (args.Handled || !TryGetChain(out var chain) || chain.Comp.Signature != BadBloodSignature.None)
            return;

        if (IsBadBloodBracer(args.Used))
        {
            args.Handled = true;
            TryInsertBracer(args.User, ent.Owner, args.Used);
            return;
        }

        // tools only mean something here while a bracer is being worked on at a working site. A broken or
        // sabotaged tower needs those same cutters and wrench for its RMC repair, so leave them unhandled then
        if (!IsSiteWorking(ent.Owner))
            return;

        var stage = ent.Comp.BracerStage;
        if ((stage == StageInserted && _tool.HasQuality(args.Used, CuttingQuality)) ||
            (stage == StageCut && _tool.HasQuality(args.Used, AnchoringQuality)))
        {
            args.Handled = true;
            TryTinkerStep(args.User, ent.Owner, args.Used);
        }
    }

    private bool CanTinker(EntityUid user, EntityUid site, [NotNullWhen(false)] out string? reason)
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

        if (chain.Comp.Signature != BadBloodSignature.None)
        {
            reason = "cmu-bad-blood-obj-signature-done";
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
    /// B1, step 1: puts a Bad Blood bracer into the site's bracer slot. The bracer can no longer be scuttled and an
    /// armed self-destruct on it is disarmed. Tells <paramref name="user"/> why when it is refused.
    /// </summary>
    public bool TryInsertBracer(EntityUid user, EntityUid site, EntityUid bracer)
    {
        if (!TryComp(site, out BadBloodUplinkSiteComponent? comp) || !IsBadBloodBracer(bracer))
            return false;

        if (!CanTinker(user, site, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason), site, user, PopupType.SmallCaution);
            return false;
        }

        var slot = _container.EnsureContainer<ContainerSlot>(site, comp.BracerSlot);
        if (slot.ContainedEntity != null || comp.BracerStage != StageEmpty)
        {
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-obj-bracer-slot-full"), site, user, PopupType.SmallCaution);
            return false;
        }

        if (!_container.Insert(bracer, slot))
        {
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-obj-bracer-cannot-insert"), site, user, PopupType.SmallCaution);
            return false;
        }

        DefuseBracer(bracer);
        comp.BracerStage = StageInserted;

        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-obj-bracer-inserted"), site, user, PopupType.Medium);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"BadBlood obj: {ToPrettyString(user):player} put the Bad Blood bracer {ToPrettyString(bracer):bracer} into {ToPrettyString(site):site}");
        return true;
    }

    // a bracer in a site is the marines' now: no scuttle, no honourable blast
    private void DefuseBracer(EntityUid bracer)
    {
        if (RemComp<BadBloodScuttleComponent>(bracer))
            _pointLight.SetEnabled(bracer, false);

        if (!TryComp(bracer, out YautjaBracerComponent? comp) || !comp.SelfDestructArmed)
            return;

        comp.SelfDestructArmed = false;
        comp.SelfDestructAt = TimeSpan.Zero;
        comp.NextSelfDestructWarning = TimeSpan.Zero;
        _selfDestruct.StopSelfDestructAudio((bracer, comp));
        Dirty(bracer, comp);
    }

    /// <summary>
    /// B1, steps 2 and 3: wirecutters on an inserted bracer, then a wrench on a cut one. Starts the tool do-after.
    /// </summary>
    public bool TryTinkerStep(EntityUid user, EntityUid site, EntityUid tool)
    {
        if (!TryGetChain(out var chain) || !TryComp(site, out BadBloodUplinkSiteComponent? comp))
            return false;

        if (!CanTinker(user, site, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason), site, user, PopupType.SmallCaution);
            return false;
        }

        if (GetInsertedBracer((site, comp)) == null)
        {
            comp.BracerStage = StageEmpty;
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-obj-bracer-missing"), site, user, PopupType.SmallCaution);
            return false;
        }

        ProtoId<ToolQualityPrototype> quality;
        switch (comp.BracerStage)
        {
            case StageInserted:
                quality = CuttingQuality;
                break;
            case StageCut:
                quality = AnchoringQuality;
                break;
            default:
                return false;
        }

        return _tool.UseTool(tool, user, site, (float) chain.Comp.BracerStepTime.TotalSeconds, quality,
            new BadBloodBracerTinkerDoAfterEvent { Stage = comp.BracerStage });
    }

    private void OnTinkerDoAfter(Entity<BadBloodUplinkSiteComponent> ent, ref BadBloodBracerTinkerDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        var user = args.User;
        if (ent.Comp.BracerStage != args.Stage)
            return;

        if (!CanTinker(user, ent.Owner, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason), ent.Owner, user, PopupType.SmallCaution);
            return;
        }

        if (GetInsertedBracer(ent) is not { } bracer)
        {
            ent.Comp.BracerStage = StageEmpty;
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-obj-bracer-missing"), ent.Owner, user, PopupType.SmallCaution);
            return;
        }

        ent.Comp.BracerStage++;
        if (ent.Comp.BracerStage < StageDone)
        {
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-obj-bracer-cut"), ent.Owner, user, PopupType.Medium);
            return;
        }

        // the slot is free again for nothing in particular: the signature is latched for the round
        ent.Comp.BracerStage = StageEmpty;
        QueueDel(bracer);
        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-obj-bracer-done"), ent.Owner, user, PopupType.Medium);
        TryLatchSignature(BadBloodSignature.Bracer, ent.Owner, user);
    }

    private EntityUid? GetInsertedBracer(Entity<BadBloodUplinkSiteComponent> site)
    {
        if (!_container.TryGetContainer(site.Owner, site.Comp.BracerSlot, out var container) ||
            container is not ContainerSlot slot ||
            slot.ContainedEntity is not { } bracer ||
            TerminatingOrDeleted(bracer) ||
            EntityManager.IsQueuedForDeletion(bracer))
        {
            return null;
        }

        return bracer;
    }

    // NEW-6: once the signature latched, a half-tinkered bracer is no use to anyone: drop it at its site and free
    // the slot. The bracer the B1 completion just used up is queued for deletion and left alone
    private void EjectStrandedBracers()
    {
        var sites = new List<Entity<BadBloodUplinkSiteComponent>>();
        var query = EntityQueryEnumerator<BadBloodUplinkSiteComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            sites.Add((uid, comp));
        }

        foreach (var site in sites)
        {
            site.Comp.BracerStage = StageEmpty;
            if (GetInsertedBracer(site) is not { } bracer ||
                !_container.TryGetContainer(site.Owner, site.Comp.BracerSlot, out var slot))
            {
                continue;
            }

            _container.Remove(bracer, slot);
        }
    }

    /// <summary>
    /// The Bad Blood bracer sitting in an uplink site's bracer slot, if any.
    /// </summary>
    public bool IsInUplinkSite(EntityUid bracer)
    {
        return _container.TryGetContainingContainer((bracer, null, null), out var container) &&
               HasComp<BadBloodUplinkSiteComponent>(container.Owner);
    }

    #endregion

    #region B2 Totem

    // the Totem verb sits on the readable component: BadBloodTotemSystem already owns the Totem's GetVerbsEvent<Verb>
    private void OnTotemSurveyVerbs(Entity<BadBloodTotemReadableComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !CanSurveyTotem(args.User, ent.Owner, out _))
            return;

        var user = args.User;
        var totem = ent.Owner;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("cmu-bad-blood-obj-totem-survey-verb"),
            Message = Loc.GetString("cmu-bad-blood-obj-totem-survey-verb-message"),
            Act = () => TryStartTotemSurvey(user, totem),
        });
    }

    /// <summary>
    /// Whether <paramref name="user"/> may survey <paramref name="totem"/> for the Totem signature now: a marine, no
    /// signature yet, and a counted Totem that is desecrated. <paramref name="reason"/> is the locale id of the
    /// refusal.
    /// </summary>
    public bool CanSurveyTotem(EntityUid user, EntityUid totem, [NotNullWhen(false)] out string? reason)
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

        if (chain.Comp.Signature != BadBloodSignature.None)
        {
            reason = "cmu-bad-blood-obj-signature-done";
            return false;
        }

        if (!TryComp(totem, out BadBloodTotemComponent? comp) ||
            !comp.Registered ||
            comp.Destroyed ||
            TerminatingOrDeleted(totem))
        {
            reason = "cmu-bad-blood-totem-consecration-gone";
            return false;
        }

        if (!comp.Desecrated)
        {
            reason = "cmu-bad-blood-obj-totem-survey-consecrated";
            return false;
        }

        return true;
    }

    /// <summary>
    /// B2: starts the Totem survey do-after. Tells <paramref name="user"/> why when it is refused.
    /// </summary>
    public bool TryStartTotemSurvey(EntityUid user, EntityUid totem)
    {
        if (!CanSurveyTotem(user, totem, out var reason) || !TryGetChain(out var chain))
        {
            _popup.PopupEntity(Loc.GetString(reason ?? "cmu-bad-blood-obj-no-rule"), user, user, PopupType.SmallCaution);
            return false;
        }

        var doAfter = new DoAfterArgs(EntityManager, user, chain.Comp.TotemSurveyTime,
            new BadBloodTotemSurveyDoAfterEvent(), totem, target: totem)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _popup.PopupEntity(
            Loc.GetString("cmu-bad-blood-obj-totem-survey-start", ("seconds", (int) chain.Comp.TotemSurveyTime.TotalSeconds)),
            user,
            user);
        return true;
    }

    private void OnTotemSurveyDoAfter(Entity<BadBloodTotemReadableComponent> ent, ref BadBloodTotemSurveyDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        TryCompleteTotemSurvey(args.User, ent.Owner);
    }

    /// <summary>
    /// Finishes a Totem survey at once: the conditions are checked again (the clan can have reconsecrated it) and
    /// the Totem signature is latched.
    /// </summary>
    public bool TryCompleteTotemSurvey(EntityUid user, EntityUid totem)
    {
        if (!CanSurveyTotem(user, totem, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason), user, user, PopupType.SmallCaution);
            return false;
        }

        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-obj-totem-survey-done"), user, user, PopupType.Medium);
        return TryLatchSignature(BadBloodSignature.Totem, totem, user);
    }

    #endregion

    #region B3 Lodge

    private void OnLodgeVerbs(Entity<BadBloodCampComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !IsMarine(args.User) ||
            !TryGetChain(out var chain) || chain.Comp.Signature != BadBloodSignature.None)
        {
            return;
        }

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("cmu-bad-blood-obj-survey-verb"),
            Message = Loc.GetString("cmu-bad-blood-obj-survey-verb-message"),
            Act = () => TryStartSurvey(user),
        });
    }

    /// <summary>
    /// Whether <paramref name="user"/> may survey the Lodge now: a marine, no signature yet, the Lodge stands and is
    /// revealed. <paramref name="reason"/> is the locale id of the refusal.
    /// </summary>
    public bool CanSurvey(EntityUid user, [NotNullWhen(false)] out string? reason)
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

        if (chain.Comp.Signature != BadBloodSignature.None)
        {
            reason = "cmu-bad-blood-obj-signature-done";
            return false;
        }

        if (!_camp.TryGetLodge(out _) || !_camp.IsLodgeRevealed())
        {
            reason = "cmu-bad-blood-obj-survey-hidden";
            return false;
        }

        return true;
    }

    /// <summary>
    /// B3: starts the Lodge survey do-after. Tells <paramref name="user"/> why when it is refused.
    /// </summary>
    public bool TryStartSurvey(EntityUid user)
    {
        if (!CanSurvey(user, out var reason) || !TryGetChain(out var chain) || !_camp.TryGetLodge(out var lodge))
        {
            _popup.PopupEntity(Loc.GetString(reason ?? "cmu-bad-blood-obj-survey-hidden"), user, user, PopupType.SmallCaution);
            return false;
        }

        var doAfter = new DoAfterArgs(EntityManager, user, chain.Comp.LodgeSurveyTime,
            new BadBloodLodgeSurveyDoAfterEvent(), lodge.Owner, target: lodge.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-obj-survey-start"), user, user);
        return true;
    }

    private void OnSurveyDoAfter(Entity<BadBloodCampComponent> ent, ref BadBloodLodgeSurveyDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        var user = args.User;

        // the reveal can have run out while the survey ran
        if (!CanSurvey(user, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason), user, user, PopupType.SmallCaution);
            return;
        }

        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-obj-survey-done"), user, user, PopupType.Medium);
        TryLatchSignature(BadBloodSignature.Lodge, ent.Owner, user);
    }

    private void OnStarChartRecovered(ref BadBloodStarChartRecoveredEvent ev)
    {
        TryLatchSignature(BadBloodSignature.Lodge, ev.Chart, ev.User);
    }

    #endregion
}
