using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Server.CMU14.ZLevelBuilding;

public sealed partial class ZLevelBuildingSystem
{
    [Dependency] private IConfigurationManager _diggingConfig = default!;

    /// <summary>Controls new downward excavations, without blocking upward digging or existing shafts.</summary>
    public bool CanDigDown => _diggingConfig.GetCVar(CCVars.AllowDiggingDown);
}
