using Robust.Shared.GameObjects;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    // Brief presentation effects must not evict persistent lights when the shadow-light budget is full.
    internal const bool MuzzleFlashCastsShadows = false;

    internal static void ConfigureMuzzleFlashLight(
        EntityUid uid,
        SharedPointLightComponent light,
        SharedPointLightSystem lights)
    {
        lights.SetCastShadows(uid, MuzzleFlashCastsShadows, light);
        lights.SetEnabled(uid, true, light);
        lights.SetRadius(uid, 2f, light);
        lights.SetColor(uid, Color.FromHex("#cc8e2b"), light);
        lights.SetEnergy(uid, 5f, light);
    }
}
