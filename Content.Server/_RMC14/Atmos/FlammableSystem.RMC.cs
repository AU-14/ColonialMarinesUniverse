using Content.Shared._RMC14.Atmos;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class FlammableSystem
{
    private const float RMCFireStackFade = -0.25f;
    private const float RMCResistingFireStackFade = -10f;

    private bool UsesRMCFireBehavior(EntityUid uid)
    {
        return HasComp<RMCFireColorComponent>(uid);
    }

    private static float GetRMCFireStackFade(bool resisting)
    {
        return resisting ? RMCResistingFireStackFade : RMCFireStackFade;
    }
}
