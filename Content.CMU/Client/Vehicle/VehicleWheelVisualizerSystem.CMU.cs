using Content.Shared.CMU14.Vehicle;
using Robust.Client.GameObjects;

namespace Content.Client._RMC14.Vehicle;

public sealed partial class VehicleWheelVisualizerSystem
{
    [Dependency] private CMUVehicleTyreSystem _cmuTyres = default!;

    /// <summary>
    /// The intact wheels' state for the tyres fitted, if the vehicle's art has them.
    /// </summary>
    private string CMUIntactState(EntityUid uid, SpriteComponent sprite, int layer)
    {
        const string state = "wheels_0";
        var prefix = _cmuTyres.StatePrefix(uid);
        if (prefix.Length == 0 || _sprite.LayerGetEffectiveRsi((uid, sprite), layer) is not { } rsi)
            return state;

        return rsi.TryGetState(prefix + state, out _) ? prefix + state : state;
    }
}
