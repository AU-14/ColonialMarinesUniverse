using Content.Shared.CMU14.Yautja;
using Robust.Shared.GameObjects;

namespace Content.Shared.CMU14.Vehicle;

/// <summary>
/// CMU14: who a vehicle knocks down when it hits them but does not pin under its hull. A Yautja, honourable or Bad
/// Blood, takes the hit's knockdown like anyone, then gets up and walks out instead of being knocked down again every
/// tick the hull sits on them.
/// </summary>
public static class CMUVehicleRunover
{
    public static bool IgnoresPin(IEntityManager entMan, EntityUid mob)
    {
        return entMan.HasComponent<YautjaComponent>(mob) || entMan.HasComponent<YautjaBadBloodComponent>(mob);
    }
}
