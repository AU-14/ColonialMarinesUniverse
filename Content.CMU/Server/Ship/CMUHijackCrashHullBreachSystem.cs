using Content.Server.Destructible;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Power;
using Content.Shared.CMU14.Atmos;
using Content.Shared.CMU14.Ship;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Ship;

/// <summary>
///     When a hijacked dropship crashes into a ship with <see cref="CMUHijackCrashHullBreachComponent"/>, the hull
///     stops being invulnerable: hull windows become as tough as a reinforced window but breakable, breathing and
///     pressure matter on every deck (if the map didn't already turn them on), and the impact knocks out every APC
///     aboard. After that the ship keeps
///     coming apart on a timer: first the hijack's pipe explosions can break hull windows, then explosions can blow out
///     any floor tile aboard.
/// </summary>
public sealed partial class CMUHijackCrashHullBreachSystem : EntitySystem
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);

    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private DestructibleSystem _destructible = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        SubscribeLocalEvent<DropshipHijackLandedEvent>(OnHijackLanded);
    }

    private void OnHijackLanded(ref DropshipHijackLandedEvent ev)
    {
        // Human hijacks land normally; only the xeno crash breaches the hull.
        if (ev.IsHumanHijack)
            return;

        var decks = new HashSet<EntityUid>(_zLevels.GetAllNetworkMaps(ev.Map));
        Entity<CMUHijackCrashHullBreachComponent>? breach = null;
        foreach (var deck in decks)
        {
            if (TryComp(deck, out CMUHijackCrashHullBreachComponent? comp))
            {
                breach = (deck, comp);
                break;
            }
        }

        if (breach is not { } ship)
            return;

        ship.Comp.CrashedAt ??= _timing.CurTime;

        // A ship that already needs air (the Bush, so its batteries can be serviced by spacewalk) has call-in kits off
        // while it's whole. Once it's breached, anyone who turns up aboard needs a mask and a tank.
        var kitDefaults = new CMUBreathingRequiredMapComponent();
        foreach (var deck in decks)
        {
            var breathing = EnsureComp<CMUBreathingRequiredMapComponent>(deck);
            breathing.CallInMask ??= kitDefaults.CallInMask;
            breathing.CallInTank ??= kitDefaults.CallInTank;
            EnsureComp<CMUPressureRequiredMapComponent>(deck);
            EnsureComp<CMUShipCrashedMapComponent>(deck);
        }

        var hulls = EntityQueryEnumerator<CMUCrashBreakableHullComponent, TransformComponent>();
        var toBreach = new List<(EntityUid Uid, EntProtoId Template, bool Replace)>();
        while (hulls.MoveNext(out var uid, out var hull, out var xform))
        {
            if (xform.MapUid is { } map && decks.Contains(map))
                toBreach.Add((uid, hull.Template ?? ship.Comp.HullTemplate, hull.ReplaceExisting));
        }

        foreach (var (uid, templateId, replace) in toBreach)
        {
            if (_prototypes.TryIndex(templateId, out var template))
                EntityManager.AddComponents(uid, template, removeExisting: replace);

            RemComp<CMUCrashBreakableHullComponent>(uid);
        }

        var apcs = EntityQueryEnumerator<RMCApcComponent, TransformComponent>();
        var toBreak = new List<EntityUid>();
        while (apcs.MoveNext(out var uid, out var apc, out var xform))
        {
            if (!apc.Broken && xform.MapUid is { } map && decks.Contains(map))
                toBreak.Add(uid);
        }

        foreach (var uid in toBreak)
        {
            _destructible.BreakEntity(uid);
        }
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + CheckInterval;

        var query = EntityQueryEnumerator<CMUHijackCrashHullBreachComponent>();
        while (query.MoveNext(out var uid, out var breach))
        {
            if (breach.CrashedAt is not { } crashedAt)
                continue;

            var elapsed = now - crashedAt;

            if (!breach.PipeBreaksWindows && elapsed >= breach.PipeBreaksWindowsDelay)
            {
                breach.PipeBreaksWindows = true;
                LetPipesBreakWindows(uid);
            }

            if (!breach.TilesBreakable && elapsed >= breach.TilesBreakableDelay)
            {
                breach.TilesBreakable = true;
                foreach (var deck in _zLevels.GetAllNetworkMaps(uid))
                {
                    EnsureComp<CMUHullCollapsingMapComponent>(deck);
                }
            }
        }
    }

    private void LetPipesBreakWindows(EntityUid ship)
    {
        foreach (var deck in _zLevels.GetAllNetworkMaps(ship))
        {
            EnsureComp<CMUHijackPipesBreakHullComponent>(deck);
        }
    }
}
