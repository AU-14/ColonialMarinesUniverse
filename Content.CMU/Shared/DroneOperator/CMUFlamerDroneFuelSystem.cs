using Content.Shared._RMC14.Water;
using Content.Shared.Examine;
using Content.Shared.Movement.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.DroneOperator;

public sealed partial class CMUFlamerDroneFuelSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private RMCWaterSystem _water = default!;

    // deeper than wading water, the nozzle is under the surface
    private const float SubmergedDepth = 8;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUFlamerDroneFuelComponent, RefreshMovementSpeedModifiersEvent>(OnSpeed);
        SubscribeLocalEvent<CMUFlamerDroneFuelComponent, AttemptShootEvent>(OnShootAttempt);
        SubscribeLocalEvent<CMUFlamerDroneFuelComponent, ExaminedEvent>(OnExamine);
    }

    private void OnSpeed(Entity<CMUFlamerDroneFuelComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.Ruined || ent.Comp.ControlLockedUntil > _timing.CurTime
            ? 0f : 1f - 0.3f * ent.Comp.Sticky);
    }

    private void OnShootAttempt(Entity<CMUFlamerDroneFuelComponent> ent, ref AttemptShootEvent args)
    {
        if (ent.Comp.Ruined || ent.Comp.CoolingUntil > _timing.CurTime || ent.Comp.ControlLockedUntil > _timing.CurTime)
        {
            args.Cancelled = true;
            args.ResetCooldown = true;
            args.Message = Loc.GetString(ent.Comp.Ruined ? "cmu-flamer-fuel-ruined" : "cmu-flamer-fuel-lockout");
            return;
        }

        if (_water.TryGetWaterSurface(ent, out _, out var depth, out var covered) &&
            !covered && depth > SubmergedDepth && _water.CanSubmerge(ent))
        {
            args.Cancelled = true;
            args.ResetCooldown = true;
            args.Message = Loc.GetString("cmu-flamer-fuel-submerged");
        }
    }

    private void OnExamine(Entity<CMUFlamerDroneFuelComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Ruined)
            args.PushMarkup(Loc.GetString("cmu-flamer-fuel-ruined"));
        else if (ent.Comp.SpecialFraction > 0)
            args.PushMarkup(Loc.GetString("cmu-flamer-fuel-warning"));
        if (ent.Comp.RepairEfficiency < 1f)
            args.PushMarkup(Loc.GetString("cmu-flamer-fuel-corrosion"));
    }
}
