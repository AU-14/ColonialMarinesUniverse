using System;
using System.Collections.Generic;

namespace Content.Shared._AU14.Insurgency;

/// <summary>
///     The Heavy Cell Kit. Using it opens a small UI listing the active faction's deployables with
///     their sprites; picking one runs a short do-after and then spawns it where the leader stands, so
///     the leader controls what goes down, in what order, and where.
///
///     Lives in Shared only so the client also registers it and can load the item prototype without
///     an unknown-component error. The behavior is server-side (InsurgencyCellKitSystem).
/// </summary>
[RegisterComponent]
public sealed partial class InsurgencyCellKitComponent : Component
{
    /// <summary>
    ///     Whether the kit's deployable list has been populated from the active faction yet. Filled
    ///     lazily the first time the UI is opened, since the faction is chosen after spawn.
    /// </summary>
    [DataField]
    public bool Initialized;

    /// <summary>
    ///     Deployables still available in this kit. Consumed one at a time as they are placed.
    /// </summary>
    [DataField]
    public List<CellKitDeployable> Remaining = new();

    /// <summary>
    ///     Seconds of do-after per deployed entity. One place to tune the deploy speed.
    /// </summary>
    [DataField]
    public float DeployTime = 5f;
}

/// <summary>
///     One thing the cell kit can put down: a plain placeable entity, or a vendor that also needs its
///     faction sections injected after it spawns.
/// </summary>
[DataDefinition]
public sealed partial class CellKitDeployable
{
    [DataField]
    public string Proto = string.Empty;

    /// <summary>
    ///     Label shown in the cell kit UI. For vendors this is the faction-authored vendor name; empty
    ///     falls back to the prototype's own name.
    /// </summary>
    [DataField]
    public string DisplayName = string.Empty;

    [DataField]
    public bool IsVendor;

    /// <summary>Index into the faction's vendor definitions, used to configure a spawned vendor.</summary>
    [DataField]
    public int VendorIndex;
}
