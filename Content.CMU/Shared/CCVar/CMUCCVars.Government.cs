// SPDX-License-Identifier: AGPL-3.0-only
// ReSharper disable CheckNamespace

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    // off: nothing gates building on the city zones or a mayor's permit, whatever the console of the administration says
    public static readonly CVarDef<bool> CMULandEnforcement =
        CVarDef.Create("cmu.land_enforcement", false, CVar.SERVERONLY | CVar.ARCHIVE);
}
