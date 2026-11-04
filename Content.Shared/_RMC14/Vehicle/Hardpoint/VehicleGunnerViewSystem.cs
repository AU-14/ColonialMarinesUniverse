using System;
using Content.Shared.Camera;
using Robust.Shared.Configuration;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Vehicle;

public sealed partial class VehicleGunnerViewSystem : EntitySystem
{
    [Dependency] private SharedContentEyeSystem _eye = default!;
    [Dependency] private IConfigurationManager _config = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VehicleGunnerViewUserComponent, GetEyePvsScaleEvent>(OnGetEyePvsScale);
        SubscribeLocalEvent<VehicleGunnerViewUserComponent, AfterAutoHandleStateEvent>(OnHandleState);
        SubscribeLocalEvent<VehicleGunnerViewUserComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<VehicleGunnerViewUserComponent, ComponentShutdown>(OnShutdown);
    }

    // CMU14 method: vehicle damage and usability.
    private void OnGetEyePvsScale(Entity<VehicleGunnerViewUserComponent> ent, ref GetEyePvsScaleEvent args)
    {
        var offsetScale = 2f * MathF.Max(0f, ent.Comp.CursorMaxOffset) /
            MathF.Max(1f, _config.GetCVar(Robust.Shared.CVars.NetMaxUpdateRange));
        args.Scale += ent.Comp.PvsScale + MathF.Max(ent.Comp.CursorPvsIncrease, offsetScale);
    }

    // CMU14 method: vehicle damage and usability.
    private void OnHandleState(Entity<VehicleGunnerViewUserComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        RefreshView(ent.Owner);
    }

    // CMU14 method: vehicle damage and usability.
    private void OnStartup(Entity<VehicleGunnerViewUserComponent> ent, ref ComponentStartup args)
    {
        RefreshView(ent.Owner);
    }

    // CMU14 method: vehicle damage and usability.
    private void OnShutdown(Entity<VehicleGunnerViewUserComponent> ent, ref ComponentShutdown args)
    {
        RefreshView(ent.Owner, removing: true);
    }

    // CMU14 method: vehicle damage and usability.
    public void RefreshView(EntityUid uid, bool removing = false)
    {
        if (!HasComp<EyeComponent>(uid) || !HasComp<ContentEyeComponent>(uid))
            return;

        _eye.UpdatePvsScale(uid);
        var ev = new VehicleGunnerViewChangedEvent(uid, removing);
        RaiseLocalEvent(ref ev);
    }
}

[ByRefEvent]
public record struct VehicleGunnerViewChangedEvent(EntityUid User, bool Removing);
