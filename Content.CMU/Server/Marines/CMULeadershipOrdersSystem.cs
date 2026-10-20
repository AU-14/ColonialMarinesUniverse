using Content.Shared._RMC14.Marines.Orders;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.CMU14.Skills;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Marines;

/// <summary>
/// Grants order capability to anyone with leadership training.
/// </summary>
public sealed partial class CMULeadershipOrdersSystem : EntitySystem
{
    private static readonly EntProtoId<SkillDefinitionComponent> Leadership = "RMCSkillLeadership";

    [Dependency] private SkillsSystem _skills = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SkillsComponent, CMUSkillsInitializedEvent>(OnSkillsStartup);
        SubscribeLocalEvent<SkillsComponent, SkillChangedEvent>(OnSkillChanged);
    }

    private void OnSkillsStartup(Entity<SkillsComponent> ent, ref CMUSkillsInitializedEvent args)
    {
        GrantOrders(ent);
    }

    private void OnSkillChanged(Entity<SkillsComponent> ent, ref SkillChangedEvent args)
    {
        if (args.Skill == Leadership && args.NewLevel >= 1)
            GrantOrders(ent);
    }

    private void GrantOrders(Entity<SkillsComponent> ent)
    {
        if (_skills.GetSkill((ent, ent.Comp), Leadership) >= 1)
            EnsureComp<MarineOrdersComponent>(ent);
    }
}
