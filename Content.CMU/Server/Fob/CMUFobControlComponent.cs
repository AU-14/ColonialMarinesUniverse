using Content.Shared.FixedPoint;

namespace Content.Server.CMU14.Fob;

[RegisterComponent, Access(typeof(CMUFobInteractionSystem))]
public sealed partial class CMUFobControlComponent : Component
{
    [DataField] public string Faction = "govfor";
    [DataField] public FixedPoint2 BreakDamage = 100;
    [ViewVariables] public EntityUid? Network;
    [ViewVariables] public bool Broken;
}
