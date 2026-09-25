using Content.Shared._RMC14.Vehicle;
using Content.Shared.Vehicle.Components;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.Server._RMC14.Vehicle;

/// <summary>Exterior views subscribe to the hull's eye, not the gunner's interior eye.</summary>
// CMU14
public sealed class VehicleGunnerPvsSystem : EntitySystem
{
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private ISharedPlayerManager _players = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VehicleComponent, ViewSubscriberAddedEvent>(OnAdded);
        SubscribeLocalEvent<VehicleComponent, ViewSubscriberRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<VehicleGunnerViewChangedEvent>(OnViewChanged);
    }

    private void OnAdded(Entity<VehicleComponent> ent, ref ViewSubscriberAddedEvent args) => Refresh(ent.Owner);
    private void OnRemoved(Entity<VehicleComponent> ent, ref ViewSubscriberRemovedEvent args) => Refresh(ent.Owner);

    private void OnViewChanged(ref VehicleGunnerViewChangedEvent args)
    {
        // Only visit hulls whose subscription range this system manages, on view changes.
        var query = EntityQueryEnumerator<VehicleGunnerPvsComponent>();
        while (query.MoveNext(out var vehicle, out _))
            Refresh(vehicle, args.Removing ? args.User : null);
    }

    public void Refresh(EntityUid vehicle, EntityUid? excludedUser = null)
    {
        var eye = EnsureComp<EyeComponent>(vehicle);
        var state = EnsureComp<VehicleGunnerPvsComponent>(vehicle);
        state.OriginalScale ??= eye.PvsScale;
        var scale = state.OriginalScale.Value;
        foreach (var session in _players.Sessions)
        {
            if (!session.ViewSubscriptions.Contains(vehicle) ||
                session.AttachedEntity is not { } user || user == excludedUser ||
                !HasComp<VehicleGunnerViewUserComponent>(user) ||
                !TryComp(user, out EyeComponent? userEye))
                continue;

            scale = MathF.Max(scale, userEye.PvsScale);
        }
        _eye.SetPvsScale((vehicle, eye), scale);
    }
}

[RegisterComponent]
// CMU14
public sealed partial class VehicleGunnerPvsComponent : Component
{
    public float? OriginalScale;
}
