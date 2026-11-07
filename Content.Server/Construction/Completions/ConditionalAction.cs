using Content.Shared.Construction;
using JetBrains.Annotations;

namespace Content.Server.Construction.Completions
{
    [UsedImplicitly]
    [DataDefinition]
    public sealed partial class ConditionalAction : IGraphAction
    {
        [DataField("passUser")] public bool PassUser { get; private set; }

        [DataField("condition", required:true)] public IGraphCondition? Condition { get; private set; }

        [DataField("action", required:true)] public IGraphAction? Action { get; private set; }

        [DataField("else")] public IGraphAction? Else { get; private set; }

        public void PerformAction(EntityUid uid, EntityUid? userUid, IEntityManager entityManager)
        {
            if (Condition == null || Action == null)
                return;

            // CMU14 Begin: selected nested refunds must pass through transaction adjustments.
            var selected = Condition.Condition(PassUser && userUid != null ? userUid.Value : uid, entityManager)
                ? Action
                : Else;
            if (selected != null)
                entityManager.EntitySysManager.GetEntitySystem<ConstructionSystem>().PerformActions(uid, userUid, [selected]);
            // CMU14 End
        }
    }
}
