using System.Collections.Generic;
using Content.Shared.CMU14.Medical.Injuries.Wounds;

namespace Content.Server.CMU14.Medical.Injuries.Wounds;

/// <summary>
/// Active server operation handles. Each DoAfter owns its complete context;
/// cancellation of one handle cannot overwrite or retire another operation.
/// </summary>
[RegisterComponent]
[Access(typeof(CMUBandageInterceptionSystem))]
public sealed partial class CMUBandagePendingComponent : Component
{
    public readonly HashSet<CMUBandageDoAfterEvent> Operations = new();
}
