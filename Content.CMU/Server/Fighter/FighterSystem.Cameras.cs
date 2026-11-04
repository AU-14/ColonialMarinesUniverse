using Content.Shared.CMU14.Fighter;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Fighter;

public sealed partial class FighterSystem
{
    private void OnCameraTerminating(Entity<CMUFighterCameraOwnerComponent> camera, ref EntityTerminatingEvent args)
    {
        if (!TryComp(camera.Comp.Seat, out FighterSeatComponent? seat) || TerminatingOrDeleted(camera.Comp.Seat))
            return;

        if (seat.Camera == camera.Owner)
            seat.Camera = null;
        else if (seat.ExteriorCamera == camera.Owner)
            seat.ExteriorCamera = null;
        else
            return;

        // Do not spawn from a deletion callback: the containing map may also be shutting down.
        Dirty(camera.Comp.Seat, seat);
    }

    private void EnsureSeatCameras(Entity<FighterSeatComponent> seat)
    {
        if (seat.Comp.Occupant is not { } occupant || TerminatingOrDeleted(occupant) ||
            TerminatingOrDeleted(seat) || seat.Comp.Aircraft is not { } aircraft || TerminatingOrDeleted(aircraft) ||
            Transform(seat).MapUid is not { } map || TerminatingOrDeleted(map))
            return;

        EnsureCamera(ref seat.Comp.Camera);
        EnsureCamera(ref seat.Comp.ExteriorCamera);
        return;

        void EnsureCamera(ref EntityUid? camera)
        {
            if (camera is { } existing && !TerminatingOrDeleted(existing))
                return;

            camera = Spawn("CMUFighterCamera", Transform(seat).Coordinates);
            AddComp<CMUFighterCameraOwnerComponent>(camera.Value).Seat = seat;
            _zLevels.EnsureZLevelViewer(camera.Value);
            if (TryComp(occupant, out ActorComponent? actor))
                _views.AddViewSubscriber(camera.Value, actor.PlayerSession);
            Dirty(seat);
        }
    }
}
