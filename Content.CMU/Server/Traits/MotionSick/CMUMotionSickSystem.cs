using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared.CMU14.Traits.MotionSick;
using Content.Shared.Medical;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Traits.MotionSick;

public sealed class CMUMotionSickSystem : EntitySystem
{
    /// <summary>Only the screen sway of being drunk, without the slurring or clumsiness.</summary>
    private static readonly EntProtoId Woozy = "StatusEffectWoozy";

    private const int QueasyMessages = 6;
    private const int WorseMessages = 4;

    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private VomitSystem _vomit = default!;

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUMotionSickComponent>();
        while (query.MoveNext(out var uid, out var sick))
        {
            if (now < sick.NextCheck)
                continue;

            sick.NextCheck = now + sick.CheckInterval;

            if (!_mobState.IsAlive(uid) || !InMotion(uid))
            {
                if (sick.Pukes > 0 && now > sick.LastInMotion + sick.RecoveryTime)
                    sick.Pukes = 0;

                continue;
            }

            sick.LastInMotion = now;

            if (_random.Prob(sick.PukeChance))
            {
                _vomit.Vomit(uid);
                sick.Pukes++;
                Dizzy(uid, sick);

                var worse = Loc.GetString($"cmu-motion-sick-worse-{_random.Next(1, WorseMessages + 1)}");
                _popup.PopupEntity(worse, uid, uid, PopupType.MediumCaution);
                continue;
            }

            Dizzy(uid, sick);

            if (_random.Prob(sick.MessageChance))
            {
                var queasy = Loc.GetString($"cmu-motion-sick-queasy-{_random.Next(1, QueasyMessages + 1)}");
                _popup.PopupEntity(queasy, uid, uid, PopupType.SmallCaution);
            }
        }
    }

    private void Dizzy(EntityUid uid, CMUMotionSickComponent sick)
    {
        var duration = sick.BaseDizziness + sick.DizzinessPerPuke * sick.Pukes;
        if (duration > sick.MaxDizziness)
            duration = sick.MaxDizziness;

        _status.TryUpdateStatusEffectDuration(uid, Woozy, duration);
    }

    /// <summary>
    ///     Aboard a flying dropship (any of its decks), or inside or riding on top of a moving vehicle.
    /// </summary>
    private bool InMotion(EntityUid uid)
    {
        if (TryComp(uid, out VehicleInteriorOccupantComponent? occupant) && VehicleMoving(occupant.Vehicle))
            return true;

        if (TryComp(uid, out VehicleRideSurfaceRiderComponent? rider) && VehicleMoving(rider.Vehicle))
            return true;

        if (Transform(uid).GridUid is not { } grid)
            return false;

        var ship = TryComp(grid, out DropshipDeckComponent? deck) && deck.Ship.IsValid() ? deck.Ship : grid;
        return TryComp(ship, out FTLComponent? ftl) && ftl.State is FTLState.Travelling or FTLState.Arriving;
    }

    private bool VehicleMoving(EntityUid vehicle)
    {
        return TryComp(vehicle, out GridVehicleMoverComponent? mover) &&
               (mover.IsMoving || MathF.Abs(mover.CurrentSpeed) > 0.01f);
    }
}
