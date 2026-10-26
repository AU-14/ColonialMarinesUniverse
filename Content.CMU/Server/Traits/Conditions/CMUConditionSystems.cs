using Content.Shared.GameTicking;
using Content.Server.Traits;
using Content.Shared._RMC14.Body;
using Content.Shared.Buckle;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared.CMU14.Medical.Diagnostics;
using Content.Shared.CMU14.Traits.Conditions;
using Content.Shared.CMU14.Traits.PanicProne;
using Content.Shared.Fluids.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Interaction;
using Content.Shared.Jittering;
using Content.Shared.Maps;
using Content.Shared.Medical;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.Storage.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Traits.Conditions;

/// <summary>
///     Someone afraid of blood gets dizzy near a pool of blood or a dead body, and now and then throws up.
/// </summary>
public sealed class CMUFearOfBloodSystem : EntitySystem
{
    private static readonly EntProtoId Woozy = "StatusEffectWoozy";
    private const int ScaredMessages = 4;

    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private VomitSystem _vomit = default!;

    private readonly HashSet<Entity<PuddleComponent>> _puddles = new();
    private readonly HashSet<Entity<MobStateComponent>> _bodies = new();

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUFearOfBloodComponent>();
        while (query.MoveNext(out var uid, out var fear))
        {
            if (now < fear.NextCheck)
                continue;

            fear.NextCheck = now + fear.CheckInterval;

            var scared = !_mobState.IsIncapacitated(uid) &&
                         !_container.IsEntityInContainer(uid) &&
                         SeesBlood(uid, fear.Range);

            if (scared)
            {
                _status.TryUpdateStatusEffectDuration(uid, Woozy, fear.Dizziness);
                if (!fear.WasScared || now >= fear.NextMessage)
                {
                    fear.NextMessage = now + fear.MessageCooldown;
                    var message = Loc.GetString($"cmu-fear-of-blood-scared-{_random.Next(1, ScaredMessages + 1)}");
                    _popup.PopupEntity(message, uid, uid, PopupType.MediumCaution);
                }

                if (_random.Prob(fear.VomitChance))
                    _vomit.Vomit(uid);
            }
            else if (fear.WasScared)
            {
                _popup.PopupEntity(Loc.GetString("cmu-fear-of-blood-calm"), uid, uid);
            }

            fear.WasScared = scared;
        }
    }

    private bool SeesBlood(EntityUid uid, float range)
    {
        var coords = Transform(uid).Coordinates;

        _bodies.Clear();
        _lookup.GetEntitiesInRange(coords, range, _bodies, LookupFlags.Dynamic);
        foreach (var body in _bodies)
        {
            if (body.Owner != uid && _mobState.IsDead(body) && _interaction.InRangeUnobstructed(uid, body.Owner, range))
                return true;
        }

        _puddles.Clear();
        _lookup.GetEntitiesInRange(coords, range, _puddles);
        foreach (var puddle in _puddles)
        {
            if (IsBloody(puddle) && _interaction.InRangeUnobstructed(uid, puddle.Owner, range))
                return true;
        }

        return false;
    }

    private bool IsBloody(Entity<PuddleComponent> puddle)
    {
        if (!_solution.TryGetSolution(puddle.Owner, puddle.Comp.SolutionName, out _, out var solution))
            return false;

        foreach (var reagent in solution.Contents)
        {
            if (reagent.Reagent.Prototype.Id.Contains("Blood", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

/// <summary>
///     A heart condition builds up strain while sprinting. Too much brings on an episode, and an untreated episode can
///     stop the heart.
/// </summary>
public sealed class CMUHeartConditionSystem : EntitySystem
{
    private static readonly EntProtoId Woozy = "StatusEffectWoozy";
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);
    private static readonly ProtoId<ReagentPrototype>[] Treatments = ["CMUCardiocaine", "CMEpinephrine"];

    [Dependency] private SharedHeartSystem _heart = default!;
    [Dependency] private SharedRMCBloodstreamSystem _rmcBloodstream = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUHeartConditionComponent, CMUHealthScannerReadingEvent>(OnScannerReading);
    }

    private void OnScannerReading(Entity<CMUHeartConditionComponent> ent, ref CMUHealthScannerReadingEvent args)
    {
        if (ent.Comp.InEpisode)
            args.State.CMUHeartEpisode = true;
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUHeartConditionComponent>();
        while (query.MoveNext(out var uid, out var heart))
        {
            if (now < heart.NextTick)
                continue;

            heart.NextTick = now + TickInterval;
            var seconds = (float) TickInterval.TotalSeconds;

            if (_mobState.IsDead(uid))
            {
                EndEpisode((uid, heart));
                heart.Strain = 0;
                continue;
            }

            if (heart.InEpisode)
            {
                _status.TryUpdateStatusEffectDuration(uid, Woozy, heart.Dizziness);
                _jitter.DoJitter(uid, TimeSpan.FromSeconds(2), true, 4f, 6f);

                if (now >= heart.EpisodeEndsAt)
                {
                    EndEpisode((uid, heart));
                    heart.Strain = 0;

                    if (!IsTreated(uid) && _random.Prob(heart.ArrestChance) && _heart.TryInduceCardiacArrest(uid))
                        _popup.PopupEntity(Loc.GetString("cmu-heart-condition-arrest"), uid, uid, PopupType.LargeCaution);
                    else
                        _popup.PopupEntity(Loc.GetString("cmu-heart-condition-recovered"), uid, uid);
                }

                continue;
            }

            if (IsSprinting(uid) && now >= heart.StrainBlockedUntil)
                heart.Strain += heart.SprintGainPerSecond * seconds;
            else
                heart.Strain = Math.Max(0, heart.Strain - heart.RecoveryPerSecond * seconds);

            if (heart.Strain >= heart.EpisodeThreshold)
            {
                StartEpisode((uid, heart), now);
            }
            else if (heart.Strain >= heart.WarnThreshold && now >= heart.NextWarn)
            {
                heart.NextWarn = now + heart.WarnCooldown;
                _popup.PopupEntity(Loc.GetString("cmu-heart-condition-warn"), uid, uid, PopupType.SmallCaution);
            }
        }
    }

    private void StartEpisode(Entity<CMUHeartConditionComponent> ent, TimeSpan now)
    {
        ent.Comp.InEpisode = true;
        ent.Comp.EpisodeEndsAt = now + ent.Comp.EpisodeDuration;
        Dirty(ent);
        _speed.RefreshMovementSpeedModifiers(ent.Owner);
        _popup.PopupEntity(Loc.GetString("cmu-heart-condition-episode"), ent, ent, PopupType.MediumCaution);
    }

    private void EndEpisode(Entity<CMUHeartConditionComponent> ent)
    {
        if (!ent.Comp.InEpisode)
            return;

        ent.Comp.InEpisode = false;
        Dirty(ent);
        _speed.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private bool IsTreated(EntityUid uid)
    {
        if (!_rmcBloodstream.TryGetChemicalSolution(uid, out _, out var blood))
            return false;

        foreach (var treatment in Treatments)
        {
            if (blood.GetTotalPrototypeQuantity(treatment.Id) > 0)
                return true;
        }

        return false;
    }

    private bool IsSprinting(EntityUid uid)
    {
        if (!TryComp<InputMoverComponent>(uid, out var mover) || !mover.Sprinting)
            return false;

        return TryComp<PhysicsComponent>(uid, out var physics) &&
               physics.LinearVelocity.LengthSquared() > 0.01f;
    }
}

/// <summary>
///     Someone claustrophobic gets dizzy shut in a locker or crate, or with walls pressing in on most sides. If they're
///     also panic-prone it builds their panic.
/// </summary>
public sealed class CMUClaustrophobiaSystem : EntitySystem
{
    private static readonly EntProtoId Woozy = "StatusEffectWoozy";
    private const int ScaredMessages = 3;

    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PanicSystem _panic = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private TurfSystem _turf = default!;

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUClaustrophobiaComponent>();
        while (query.MoveNext(out var uid, out var claustrophobia))
        {
            if (now < claustrophobia.NextCheck)
                continue;

            claustrophobia.NextCheck = now + claustrophobia.CheckInterval;

            var scared = !_mobState.IsIncapacitated(uid) && IsCramped(uid, claustrophobia.BlockedNeighbours);
            if (scared)
            {
                _status.TryUpdateStatusEffectDuration(uid, Woozy, claustrophobia.Dizziness);
                if (HasComp<PanicComponent>(uid))
                    _panic.AddPanic(uid, claustrophobia.PanicPerCheck);

                if (!claustrophobia.WasScared || now >= claustrophobia.NextMessage)
                {
                    claustrophobia.NextMessage = now + claustrophobia.MessageCooldown;
                    var message = Loc.GetString($"cmu-claustrophobia-scared-{_random.Next(1, ScaredMessages + 1)}");
                    _popup.PopupEntity(message, uid, uid, PopupType.MediumCaution);
                }
            }
            else if (claustrophobia.WasScared)
            {
                _popup.PopupEntity(Loc.GetString("cmu-claustrophobia-calm"), uid, uid);
            }

            claustrophobia.WasScared = scared;
        }
    }

    private bool IsCramped(EntityUid uid, int blockedNeeded)
    {
        // Shut inside something like a locker, crate or body bag.
        if (_container.TryGetContainingContainer((uid, null, null), out var container) &&
            HasComp<EntityStorageComponent>(container.Owner))
        {
            return true;
        }

        var xform = Transform(uid);
        if (xform.GridUid is not { } gridUid || !TryComp(gridUid, out MapGridComponent? grid))
            return false;

        var center = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var blocked = 0;
        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0)
                    continue;

                var indices = center + new Vector2i(x, y);
                if (!_map.TryGetTileRef(gridUid, grid, indices, out var tile) ||
                    _turf.IsSpace(tile) ||
                    _turf.IsTileBlocked(gridUid, indices, CollisionGroup.Impassable, grid))
                {
                    blocked++;
                }
            }
        }

        return blocked >= blockedNeeded;
    }
}

/// <summary>Someone wheelchair-bound spawns sitting in a wheelchair.</summary>
public sealed class CMUWheelchairSpawnSystem : EntitySystem
{
    [Dependency] private SharedBuckleSystem _buckle = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete, after: [typeof(TraitSystem)]);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (!TryComp(args.Mob, out CMUWheelchairBoundComponent? bound))
            return;

        var chair = Spawn(bound.Wheelchair, Transform(args.Mob).Coordinates);
        if (!_buckle.TryBuckle(args.Mob, null, chair, popup: false))
            Log.Warning($"Couldn't buckle {ToPrettyString(args.Mob)} into their wheelchair {ToPrettyString(chair)}");
    }
}
