using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Vendors;

/// <summary>
///     Put on a vendor to teach the user skills when the vendor hands out particular items, e.g. the skill needed to
///     fire the smart gun it just gave them. Skills are only ever raised, never lowered.
/// </summary>
[RegisterComponent]
public sealed partial class CMUGrantSkillOnVendComponent : Component
{
    /// <summary>
    ///     Vended item prototype to the skills (and minimum levels) the user gets for receiving it.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<EntProtoId, Dictionary<EntProtoId<SkillDefinitionComponent>, int>> Skills = new();
}

public sealed class CMUGrantSkillOnVendSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SkillsSystem _skills = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUGrantSkillOnVendComponent, CMUVendorVendedItemEvent>(OnVended);
    }

    private void OnVended(Entity<CMUGrantSkillOnVendComponent> ent, ref CMUVendorVendedItemEvent args)
    {
        if (_net.IsClient)
            return;

        if (MetaData(args.Item).EntityPrototype?.ID is not { } id ||
            !ent.Comp.Skills.TryGetValue(id, out var grants))
        {
            return;
        }

        foreach (var (skill, level) in grants)
        {
            if (_skills.GetSkill(args.User, skill) < level)
                _skills.SetSkill(args.User, skill, level);
        }
    }
}
