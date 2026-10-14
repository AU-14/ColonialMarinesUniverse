using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Medical.Treatment.Effects;

/// <summary>Repairs fractures and replaces blood during Necrosol metabolism.</summary>
[UsedImplicitly]
public sealed partial class CMUCryogenicBoneAndBloodRepair : EntityEffectBase<CMUCryogenicBoneAndBloodRepair>
{
    [DataField]
    public FixedPoint2 BoneIntegrityPerUnit = 0.25;

    [DataField]
    public float BloodPercentPerSecond = 1f;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("cmu-medical-cryo-bone-blood-guidebook",
            ("bone", BoneIntegrityPerUnit), ("blood", BloodPercentPerSecond));
}

/// <summary>Closes the matching CMU wound entries as the medicine metabolizes.</summary>
[UsedImplicitly]
public sealed partial class CMUCryogenicWoundRepair : EntityEffectBase<CMUCryogenicWoundRepair>
{
    [DataField]
    public WoundType Type = WoundType.Brute;

    [DataField]
    public FixedPoint2 AmountPerUnit = 0.25;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString(Type == WoundType.Burn
                ? "cmu-medical-cryo-burn-wounds-guidebook"
                : "cmu-medical-cryo-brute-wounds-guidebook",
            ("amount", AmountPerUnit));
}

/// <summary>Gradually repairs every attached organ.</summary>
[UsedImplicitly]
public sealed partial class CMUCryogenicOrganRepair : EntityEffectBase<CMUCryogenicOrganRepair>
{
    [DataField]
    public FixedPoint2 AmountPerUnit = 0.25;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("cmu-medical-cryo-organs-guidebook", ("amount", AmountPerUnit));
}
