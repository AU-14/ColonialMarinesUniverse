using Content.Server._RMC14.Power;
using Content.Shared._RMC14.Power;
using Content.Shared.CMU14.Ship;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Gravity;
using Content.Shared.Power;
using Content.Shared.Popups;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Ship;

/// <summary>
///     A ship's gravity follows its <see cref="CMUShipGravityGeneratorComponent"/>: every deck in the z-network has
///     gravity while the generator is powered, loses it when the power goes, and gets it back when power returns.
///     The decks start with gravity on, so a generator that hasn't been powered yet doesn't strand anyone in zero-g at
///     round start.
/// </summary>
public sealed class CMUShipGravityGeneratorSystem : EntitySystem
{
    /// <summary>
    ///     Power changes are picked up from events right away; this re-check catches anything they miss, such as an APC
    ///     being repaired, so gravity can never get stuck off.
    /// </summary>
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RMCPowerSystem _power = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUShipGravityGeneratorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUShipGravityGeneratorComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<CMUShipGravityGeneratorComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + CheckInterval;

        var query = EntityQueryEnumerator<CMUShipGravityGeneratorComponent>();
        while (query.MoveNext(out var uid, out var generator))
        {
            UpdateGenerator((uid, generator), IsPowered(uid));
        }
    }

    private void OnMapInit(Entity<CMUShipGravityGeneratorComponent> ent, ref MapInitEvent args)
    {
        // Hand the decks' gravity over to the generator. They keep the gravity they loaded with until the generator
        // reports it has lost power.
        foreach (var deck in GetDecks(ent))
        {
            if (TryComp(deck, out GravityComponent? gravity))
            {
                gravity.Inherent = false;
                Dirty(deck, gravity);
            }
        }
    }

    private void OnPowerChanged(Entity<CMUShipGravityGeneratorComponent> ent, ref PowerChangedEvent args)
    {
        // Same source of truth as the periodic check, so the two can never disagree and flicker the gravity.
        UpdateGenerator(ent, IsPowered(ent));
    }

    private void OnShutdown(Entity<CMUShipGravityGeneratorComponent> ent, ref ComponentShutdown args)
    {
        // A destroyed generator takes gravity with it, but not when the whole ship is being unloaded.
        if (TerminatingOrDeleted(Transform(ent).MapUid))
            return;

        SetShipGravity(ent, false);
    }

    private void UpdateGenerator(Entity<CMUShipGravityGeneratorComponent> ent, bool powered)
    {
        // Until it has had power once, an unpowered generator leaves the decks' starting gravity alone.
        if (ent.Comp.LastPowered == null && !powered)
            return;

        if (ent.Comp.LastPowered == powered)
            return;

        ent.Comp.LastPowered = powered;
        SetShipGravity(ent, powered);
    }

    private bool IsPowered(EntityUid generator)
    {
        // Go by the area's equipment channel itself rather than the receiver's cached flag.
        if (TryComp(generator, out RMCPowerReceiverComponent? receiver) && receiver.Area is { } area)
            return _power.IsAreaPowered(area, receiver.Channel);

        return _power.IsPowered(generator);
    }

    private void SetShipGravity(EntityUid generator, bool enabled)
    {
        var changed = false;
        foreach (var deck in GetDecks(generator))
        {
            if (TerminatingOrDeleted(deck) ||
                !TryComp(deck, out GravityComponent? gravity) ||
                gravity.Enabled == enabled && !gravity.Inherent)
            {
                continue;
            }

            gravity.Inherent = false;
            gravity.Enabled = enabled;
            Dirty(deck, gravity);

            var ev = new GravityChangedEvent(deck, enabled);
            RaiseLocalEvent(deck, ref ev, true);
            changed = true;
        }

        if (!changed)
            return;

        var decks = new HashSet<EntityUid>(GetDecks(generator));
        var message = Loc.GetString(enabled ? "cmu-ship-gravity-restored" : "cmu-ship-gravity-lost");
        var popup = enabled ? PopupType.Large : PopupType.LargeCaution;
        var players = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (players.MoveNext(out var player, out _, out var xform))
        {
            if (xform.MapUid is { } map && decks.Contains(map))
                _popup.PopupEntity(message, player, player, popup);
        }
    }

    private List<EntityUid> GetDecks(EntityUid generator)
    {
        return Transform(generator).MapUid is { } map ? _zLevels.GetAllNetworkMaps(map) : new List<EntityUid>();
    }
}
