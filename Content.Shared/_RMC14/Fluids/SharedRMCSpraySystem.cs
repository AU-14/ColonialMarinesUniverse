using Content.Shared.Fluids.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;

namespace Content.Shared._RMC14.Fluids;

public abstract partial class SharedRMCSpraySystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private INetManager _net = default!;

    public virtual void Spray(EntityUid entity, EntityUid user, MapCoordinates mapcoord, bool hitUser = false)
    {
        // RMC gun firing runs on both prediction timelines. Play locally on the
        // predicting client; the server sends the same sound to everyone else
        // only after the spray is accepted.
        if (_net.IsClient && TryComp(entity, out SprayComponent? spray))
        {
            _audio.PlayPredicted(
                spray.SpraySound,
                entity,
                user,
                spray.SpraySound.Params.WithVariation(0.125f));
        }
    }
}
