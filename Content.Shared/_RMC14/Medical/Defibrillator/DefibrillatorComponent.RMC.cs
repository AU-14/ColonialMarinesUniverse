using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Metabolism;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical;

public sealed partial class DefibrillatorComponent
{
    /// <summary>
    /// RMC damage-group healing applied in addition to the upstream defibrillator healing.
    /// </summary>
    [DataField("rmcZapHeal")]
    public List<(ProtoId<DamageGroupPrototype> Group, int Amount)>? RMCZapDamage;

    /// <summary>
    /// Metabolism stage searched for RMC electrogenetic effects.
    /// </summary>
    [DataField]
    public ProtoId<MetabolismStagePrototype> MetabolismId = "Bloodstream";

    /// <summary>
    /// Skill used to modify the RMC defibrillation delay.
    /// </summary>
    [DataField]
    public EntProtoId<SkillDefinitionComponent> Skill = "RMCSkillMedical";

    [DataField]
    public TimeSpan SkillMultiplierDuration = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Active RMC charge sound, if one was spawned by the authoritative system.
    /// </summary>
    [ViewVariables]
    public EntityUid? ChargeSoundEntity;
}
