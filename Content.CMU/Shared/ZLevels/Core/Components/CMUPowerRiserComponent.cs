namespace Content.Shared.CMU14.ZLevels.Core.Components;

[RegisterComponent]
public sealed partial class CMUPowerRiserComponent : Component
{
    /// <summary>
    /// True: this unit is a Source (monitors mains/twin). False: this unit is a Sink (supplies grid).
    /// Independent of Offset direction. Use screwdriver to toggle.
    /// </summary>
    [DataField]
    public bool Source;

    [ViewVariables]
    public bool Powered;
}
