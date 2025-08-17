using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.Roles.Jobs;
using Content.Shared.Au14.Util;
using Content.Shared.CharacterInfo;
using Content.Shared.Inventory;
using Content.Shared.Objectives;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Systems;

namespace Content.Server.CharacterInfo;

public sealed class CharacterInfoSystem : EntitySystem
{
    [Dependency] private readonly JobSystem _jobs = default!;
    [Dependency] private readonly MindSystem _minds = default!;
    [Dependency] private readonly RoleSystem _roles = default!;
    [Dependency] private readonly SharedObjectivesSystem _objectives = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<RequestCharacterInfoEvent>(OnRequestCharacterInfoEvent);
    }

    private void OnRequestCharacterInfoEvent(RequestCharacterInfoEvent msg, EntitySessionEventArgs args)
    {
        if (!args.SenderSession.AttachedEntity.HasValue
            || args.SenderSession.AttachedEntity != GetEntity(msg.NetEntity))
            return;

        var entity = args.SenderSession.AttachedEntity.Value;

        var objectives = new Dictionary<string, List<ObjectiveInfo>>();
        var jobTitle = Loc.GetString("character-info-no-profession");
        string? briefing = null;
        if (_minds.TryGetMind(entity, out var mindId, out var mind))
        {
            // Get objectives
            foreach (var objective in mind.Objectives)
            {
                var info = _objectives.GetInfo(objective, mindId, mind);
                if (info == null)
                    continue;

                // group objectives by their issuer
                var issuer = Comp<ObjectiveComponent>(objective).LocIssuer;
                if (!objectives.ContainsKey(issuer))
                    objectives[issuer] = new List<ObjectiveInfo>();
                objectives[issuer].Add(info.Value);
            }

            if (_jobs.MindTryGetJobName(mindId, out var jobName))
                jobTitle = jobName;

            // Get briefing
            briefing = _roles.MindGetBriefing(mindId);
        }

        // Check inventory and hands for JobTitleChangerComponent
        if (EntityManager.TryGetComponent(entity, out InventoryComponent? inventory))
        {
            var invSys = EntityManager.System<InventorySystem>();
            foreach (var item in invSys.GetHandOrInventoryEntities(entity))
            {
                if (EntityManager.TryGetComponent<JobTitleChangerComponent>(item, out var changer) && !string.IsNullOrWhiteSpace(changer.JobTitle))
                {
                    jobTitle = changer.JobTitle;
                    break;
                }
            }
        }

        // Check uniform accessories (e.g., armbands) for JobTitleChangerComponent
        if (EntityManager.TryGetComponent(entity, out Content.Shared._RMC14.UniformAccessories.UniformAccessoryHolderComponent? accessoryHolder))
        {
            var containerSys = EntityManager.EntitySysManager.GetEntitySystem<Robust.Shared.Containers.SharedContainerSystem>();
            if (containerSys.TryGetContainer(entity, accessoryHolder.ContainerId, out var container))
            {
                foreach (var accessory in container.ContainedEntities)
                {
                    if (EntityManager.TryGetComponent<JobTitleChangerComponent>(accessory, out var changer) && !string.IsNullOrWhiteSpace(changer.JobTitle))
                    {
                        jobTitle = changer.JobTitle;
                        break;
                    }
                }
            }
        }

        RaiseNetworkEvent(new CharacterInfoEvent(GetNetEntity(entity), jobTitle, objectives, briefing), args.SenderSession);
    }
}
