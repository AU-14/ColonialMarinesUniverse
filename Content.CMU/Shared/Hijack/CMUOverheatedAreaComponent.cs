namespace Content.Shared.CMU14.Hijack;

/// <summary>Constant area temperature used by CM-style atmospherically immutable maps.</summary>
[RegisterComponent]
public sealed partial class CMUOverheatedAreaComponent : Component
{
    [DataField] public float Temperature = 293.15f;
}
