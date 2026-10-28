using Content.Shared.Coordinates;
using Robust.Shared.Map;

namespace Content.Shared._RMC14.Areas;

public sealed partial class AreaSystem
{
    /// <summary>
    /// Evaluates the local roof permissions needed together by area-info alerts in one live pass.
    /// Supply drops and top-down ordnance retain their separate coordinate/column checks.
    /// </summary>
    internal CMUAreaInfoRoofing CMUGetAreaInfoRoofing(EntityCoordinates coordinates)
    {
        var result = new CMUAreaInfoRoofing();
        var roofs = EntityQueryEnumerator<RoofingEntityComponent>();
        while (roofs.MoveNext(out var uid, out var roof))
        {
            result.EntitiesScanned++;
            var core = !result.HiveCore && !roof.CanOrbitalBombard && roof.Range > 10;
            var pylon = !result.Pylon && roof.CanOrbitalBombard && !roof.CanCAS && roof.Range < 10;
            if (core || pylon)
                result.ProtectionCandidates++;

            if (!core && !pylon &&
                (result.BlocksCAS || roof.CanCAS) &&
                (result.BlocksMortarPlacement || roof.CanMortarPlace) &&
                (result.BlocksLase || roof.CanLase) &&
                (result.BlocksMedevac || roof.CanMedevac) &&
                (result.BlocksParadrop || roof.CanParadrop))
            {
                continue;
            }

            // Match the action checks' coordinate conversion, map exclusion and inclusive range.
            if (!coordinates.TryDistance(EntityManager, uid.ToCoordinates(), out var distance) ||
                !(distance <= roof.Range))
            {
                continue;
            }

            result.HiveCore |= core;
            result.Pylon |= pylon;
            result.BlocksCAS |= !roof.CanCAS;
            result.BlocksMortarPlacement |= !roof.CanMortarPlace;
            result.BlocksLase |= !roof.CanLase;
            result.BlocksMedevac |= !roof.CanMedevac;
            result.BlocksParadrop |= !roof.CanParadrop;

            if (result.HiveCore && result.Pylon && result.BlocksCAS && result.BlocksMortarPlacement &&
                result.BlocksLase && result.BlocksMedevac && result.BlocksParadrop)
            {
                break;
            }
        }

        return result;
    }
}

// A per-call summary, never retained across movement or changes to a roof's permissions/lifetime.
internal struct CMUAreaInfoRoofing
{
    public bool BlocksCAS;
    public bool BlocksMortarPlacement;
    public bool BlocksLase;
    public bool BlocksMedevac;
    public bool BlocksParadrop;
    public bool HiveCore;
    public bool Pylon;
    public int EntitiesScanned;
    public int ProtectionCandidates;
}
