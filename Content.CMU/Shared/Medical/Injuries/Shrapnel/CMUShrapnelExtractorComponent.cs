using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._CMU14.Medical.Injuries.Shrapnel;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedCMUShrapnelSystem))]
public sealed partial class CMUShrapnelExtractorComponent : Component
{
    [DataField]
    public int RemoveCount = 2;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(2);

    [DataField]
    public float PainPenalty = 5f;

    [DataField]
    public FixedPoint2 DamageOnExtract = FixedPoint2.New(2);

    [DataField]
    public ProtoId<DamageTypePrototype> DamageType = "Piercing";

    [DataField]
    public SpriteSpecifier VerbIcon = new SpriteSpecifier.Rsi(
        new ResPath("/Textures/_RMC14/Objects/Weapons/shrapnel.rsi"),
        "shrapnel_glass");

    [DataField]
    public SoundSpecifier ExtractSound = new SoundPathSpecifier(
        "/Audio/Effects/glass_step.ogg",
        AudioParams.Default.WithVariation(0.2f).WithVolume(-2f));
}
