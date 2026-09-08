using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._CMU14.Chemistry.Effects.Special;
using Content.Shared._CMU14.Xenonids.CorruptedHive;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Xenonids.Egg;
using Content.Shared._RMC14.Xenonids.Egg.EggRetriever;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Chemistry;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Mobs;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Popups;
using Content.Shared.StepTrigger.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Xenonids.CorruptedHive;

/// <summary>
/// Owns the WY ciphering path from an intact egg to a corrupted-hive facehugger.
/// </summary>
public sealed partial class CMUCorruptedHiveSystem : EntitySystem
{
    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private GhostRoleSystem _ghostRole = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private ISharedPlaytimeManager _playtime = default!;
    [Dependency] private ISharedPlayerManager _players = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const string PrimeHivePrototype = "CMXenoHive";
    private const string CorruptedHivePrototype = "CMUCorruptedHive";
    private const string CorruptedParasitePrototype = "CMUCorruptedXenoParasite";
    private static readonly FixedPoint2 MinimumCipheringAmount = FixedPoint2.New(1);
    private static readonly TimeSpan ClaimReservationDuration = TimeSpan.FromSeconds(30);

    private (NetUserId UserId, EntityUid Queen)? _lastDeadPrimeQueen;
    private uint _nextOfferId;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUCipherableXenoEggComponent, ReactionEntityEvent>(OnEggReaction);
        SubscribeLocalEvent<CMUCorruptedParasiteComponent, XenoParasiteClaimAttemptEvent>(OnParasiteClaimAttempt);
        SubscribeLocalEvent<MobStateChangedEvent>(OnQueenMobStateChanged);
        SubscribeLocalEvent<CMUCorruptedParasiteClaimChoiceEvent>(OnClaimChoice);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnQueenMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead ||
            !HasComp<XenoEvolutionGranterComponent>(args.Target) ||
            !TryComp(args.Target, out ActorComponent? actor) ||
            _hive.GetHive(args.Target) is not { } hive ||
            MetaData(hive.Owner).EntityPrototype?.ID != PrimeHivePrototype)
        {
            return;
        }

        _lastDeadPrimeQueen = (actor.PlayerSession.UserId, args.Target);
    }

    private void OnEggReaction(Entity<CMUCipherableXenoEggComponent> ent, ref ReactionEntityEvent args)
    {
        if (ent.Comp.Converted ||
            args.Method != ReactionMethod.Injection ||
            args.ReagentQuantity.Quantity < MinimumCipheringAmount ||
            !TryComp(ent, out XenoEggComponent? egg) ||
            egg.State is XenoEggState.Opening or XenoEggState.Opened ||
            !HasCorruptedCiphering(args))
        {
            return;
        }

        ent.Comp.Converted = true;
        RemComp<StepTriggerComponent>(ent);
        RemoveFromSustainer(ent.Owner);

        var corruptedHive = GetOrCreateCorruptedHive();
        var parasite = SpawnNextToOrDrop(CorruptedParasitePrototype, ent.Owner);
        _hive.SetHive(parasite, corruptedHive);
        _audio.PlayPvs(egg.BurstSound, ent.Owner);

        var corrupted = EnsureComp<CMUCorruptedParasiteComponent>(parasite);
        HidePublicGhostRole((parasite, corrupted));
        OfferToKilledQueen((parasite, corrupted));

        QueueDel(ent);
    }

    private static bool HasCorruptedCiphering(ReactionEntityEvent args)
    {
        if (args.Reagent.Metabolisms == null)
            return false;

        foreach (var metabolism in args.Reagent.Metabolisms.Values)
        {
            foreach (var effect in metabolism.Effects)
            {
                if (effect is Ciphering ciphering && (int) MathF.Round(ciphering.Potency) == 2)
                    return true;
            }
        }

        return false;
    }

    private EntityUid GetOrCreateCorruptedHive()
    {
        var query = EntityQueryEnumerator<HiveComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (MetaData(uid).EntityPrototype?.ID == CorruptedHivePrototype)
                return uid;
        }

        return Spawn(CorruptedHivePrototype);
    }

    private void RemoveFromSustainer(EntityUid egg)
    {
        if (!TryComp(egg, out XenoFragileEggComponent? fragile) ||
            fragile.SustainedBy is not { } sustainer ||
            !TryComp(sustainer, out XenoEggSustainerComponent? sustainerComp))
        {
            return;
        }

        if (sustainerComp.SustainedEggs.Remove(egg))
            Dirty(sustainer, sustainerComp);
    }

    private void OfferToKilledQueen(Entity<CMUCorruptedParasiteComponent> parasite)
    {
        if (_lastDeadPrimeQueen is not { } killedQueen)
        {
            MakePubliclyTakeable(parasite);
            return;
        }

        _lastDeadPrimeQueen = null;

        if (!_players.TryGetSessionById(killedQueen.UserId, out var session) ||
            session.AttachedEntity is not { } claimant ||
            claimant != killedQueen.Queen && !HasComp<GhostComponent>(claimant))
        {
            MakePubliclyTakeable(parasite);
            return;
        }

        parasite.Comp.ReservedFor = killedQueen.UserId;
        parasite.Comp.ReservationExpiresAt = _timing.CurTime + ClaimReservationDuration;
        parasite.Comp.OfferId = ++_nextOfferId;
        var offerId = parasite.Comp.OfferId;

        Timer.Spawn(ClaimReservationDuration, () =>
        {
            if (TerminatingOrDeleted(parasite.Owner) ||
                !TryComp(parasite.Owner, out CMUCorruptedParasiteComponent? corrupted) ||
                corrupted.OfferId != offerId ||
                corrupted.ReservedFor is null)
            {
                return;
            }

            MakePubliclyTakeable((parasite.Owner, corrupted));
        });

        var options = new List<DialogOption>
        {
            new(
                Loc.GetString("cmu-corrupted-hive-claim-yes"),
                new CMUCorruptedParasiteClaimChoiceEvent(
                    GetNetEntity(claimant),
                    GetNetEntity(parasite.Owner),
                    parasite.Comp.OfferId,
                    true)),
            new(
                Loc.GetString("cmu-corrupted-hive-claim-no"),
                new CMUCorruptedParasiteClaimChoiceEvent(
                    GetNetEntity(claimant),
                    GetNetEntity(parasite.Owner),
                    parasite.Comp.OfferId,
                    false)),
        };

        _dialog.OpenOptions(
            claimant,
            claimant,
            Loc.GetString("cmu-corrupted-hive-claim-title"),
            options,
            Loc.GetString("cmu-corrupted-hive-claim-message"));
    }

    private void OnClaimChoice(CMUCorruptedParasiteClaimChoiceEvent args)
    {
        if (!TryGetEntity(args.Claimant, out var claimant) ||
            !TryGetEntity(args.Parasite, out var parasite) ||
            !TryComp(claimant.Value, out ActorComponent? actor) ||
            actor.PlayerSession.AttachedEntity != claimant.Value ||
            !TryComp(parasite.Value, out CMUCorruptedParasiteComponent? corrupted) ||
            corrupted.OfferId != args.OfferId ||
            corrupted.ReservedFor != actor.PlayerSession.UserId ||
            HasComp<ActorComponent>(parasite.Value))
        {
            return;
        }

        var expired = corrupted.ReservationExpiresAt is not { } expiresAt || _timing.CurTime > expiresAt;
        ClearReservation(corrupted);

        if (!args.Claim || expired)
        {
            MakePubliclyTakeable((parasite.Value, corrupted));
            return;
        }

        if (!CMUCorruptedHiveRequirements.IsEligible(
                EntityManager,
                _prototypes,
                _playtime,
                actor.PlayerSession))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-corrupted-hive-claim-insufficient-hours"),
                claimant.Value,
                claimant.Value,
                PopupType.MediumCaution);
            MakePubliclyTakeable((parasite.Value, corrupted));
            return;
        }

        _ghostRole.GhostRoleInternalCreateMindAndTransfer(
            actor.PlayerSession,
            parasite.Value,
            parasite.Value);
    }

    private void OnParasiteClaimAttempt(
        Entity<CMUCorruptedParasiteComponent> parasite,
        ref XenoParasiteClaimAttemptEvent args)
    {
        if (!TryComp(args.User, out ActorComponent? actor))
        {
            args.Cancel();
            return;
        }

        if (parasite.Comp.ReservationExpiresAt is { } expiresAt && _timing.CurTime > expiresAt)
            MakePubliclyTakeable(parasite);

        if (parasite.Comp.ReservedFor is { } reservedFor && reservedFor != actor.PlayerSession.UserId)
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-corrupted-hive-claim-reserved"),
                args.User,
                args.User,
                PopupType.MediumCaution);
            args.Cancel();
            return;
        }

        if (!CMUCorruptedHiveRequirements.IsEligible(
                EntityManager,
                _prototypes,
                _playtime,
                actor.PlayerSession))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-corrupted-hive-claim-insufficient-hours"),
                args.User,
                args.User,
                PopupType.MediumCaution);
            args.Cancel();
            return;
        }

        args.BypassDeathTime = parasite.Comp.ReservedFor == actor.PlayerSession.UserId;
        ClearReservation(parasite.Comp);
    }

    /// <summary>
    /// Corrupted parasites keep a real ghost-role component so a delayed Queen response can still transfer a mind.
    /// Keep it out of the public ghost-role list until the Queen declines or cannot be offered the parasite.
    /// </summary>
    private void HidePublicGhostRole(Entity<CMUCorruptedParasiteComponent> parasite)
    {
        RemComp<GhostTakeoverAvailableComponent>(parasite);
        if (TryComp(parasite, out GhostRoleComponent? role))
            _ghostRole.UnregisterGhostRole((parasite.Owner, role));
    }

    /// <summary>
    /// Publishes the parasite through both the existing facehugger takeover verb and the standard ghost-role list.
    /// </summary>
    private void MakePubliclyTakeable(Entity<CMUCorruptedParasiteComponent> parasite)
    {
        if (TerminatingOrDeleted(parasite.Owner) || HasComp<ActorComponent>(parasite))
            return;

        ClearReservation(parasite.Comp);
        EnsureComp<ParasiteAIComponent>(parasite);
        EnsureComp<GhostTakeoverAvailableComponent>(parasite);

        if (TryComp(parasite, out GhostRoleComponent? role))
            _ghostRole.RegisterGhostRole((parasite.Owner, role));
    }

    private static void ClearReservation(CMUCorruptedParasiteComponent parasite)
    {
        parasite.ReservedFor = null;
        parasite.ReservationExpiresAt = null;
        parasite.OfferId = 0;
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        _lastDeadPrimeQueen = null;
        _nextOfferId = 0;
    }
}
