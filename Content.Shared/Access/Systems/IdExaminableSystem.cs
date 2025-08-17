using Content.Shared.Access.Components;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Au14.Util;
using Content.Shared.PDA;
using Content.Shared.Verbs;
using Content.Shared._RMC14.UniformAccessories;
using Robust.Shared.Utility;
using Robust.Shared.Containers;

namespace Content.Shared.Access.Systems;

public sealed class IdExaminableSystem : EntitySystem
{
    [Dependency] private readonly ExamineSystemShared _examineSystem = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IdExaminableComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
    }

    private void OnGetExamineVerbs(EntityUid uid, IdExaminableComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        var detailsRange = _examineSystem.IsInDetailsRange(args.User, uid);
        var info = GetMessage(uid);

        var verb = new ExamineVerb()
        {
            Act = () =>
            {
                var markup = FormattedMessage.FromMarkupOrThrow(info);

                _examineSystem.SendExamineTooltip(args.User, uid, markup, false, false);
            },
            Text = Loc.GetString("id-examinable-component-verb-text"),
            Category = VerbCategory.Examine,
            Disabled = !detailsRange,
            Message = detailsRange ? null : Loc.GetString("id-examinable-component-verb-disabled"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/character.svg.192dpi.png"))
        };

        args.Verbs.Add(verb);
    }

    public string GetMessage(EntityUid uid)
    {
        return GetInfo(uid) ?? Loc.GetString("id-examinable-component-verb-no-id");
    }

    public string? GetInfo(EntityUid uid)
    {
        // Try to get ID card from inventory slot
        if (_inventorySystem.TryGetSlotEntity(uid, "id", out var idUid))
        {
            // PDA
            if (TryComp(idUid, out PdaComponent? pda) &&
                TryComp<IdCardComponent>(pda.ContainedId, out var idCardFromPda))
            {
                var jobTitle = GetOverridingJobTitle(uid, idCardFromPda.LocalizedJobTitle);
                return GetNameAndJob(idCardFromPda, jobTitle);
            }
            // ID Card
            if (TryComp(idUid, out IdCardComponent? idCard))
            {
                var jobTitle = GetOverridingJobTitle(uid, idCard.LocalizedJobTitle);
                return GetNameAndJob(idCard, jobTitle);
            }
        }
        // If no ID card, check for a JobTitleChangerComponent directly (equipped or uniform accessories)
        var overrideJobTitle = GetOverridingJobTitle(uid, null);
        if (!string.IsNullOrWhiteSpace(overrideJobTitle))
        {
            // Fallback display if no ID card exists, but a job title override is present
            return Loc.GetString("access-id-card-component-owner-name-job-title-text", ("jobSuffix", $" ({overrideJobTitle})"));
        }
        return null;
    }

    private string GetNameAndJob(IdCardComponent id, string? overrideJobTitle = null)
    {
        var jobTitle = overrideJobTitle ?? id.LocalizedJobTitle;
        var jobSuffix = string.IsNullOrWhiteSpace(jobTitle) ? string.Empty : $" ({jobTitle})";

        var val = string.IsNullOrWhiteSpace(id.FullName)
            ? Loc.GetString(id.NameLocId,
                ("jobSuffix", jobSuffix))
            : Loc.GetString(id.FullNameLocId,
                ("fullName", id.FullName),
                ("jobSuffix", jobSuffix));

        return val;
    }

    private string? GetOverridingJobTitle(EntityUid uid, string? fallback)
    {
        // Check all equipped items for a JobTitleChangerComponent with Override=true
        if (TryComp(uid, out InventoryComponent? inventory))
        {
            foreach (var item in _inventorySystem.GetHandOrInventoryEntities(uid))
            {
                // Directly on equipped item
                if (TryComp<JobTitleChangerComponent>(item, out var changer) && changer.Override && !string.IsNullOrWhiteSpace(changer.JobTitle))
                {
                    return changer.JobTitle;
                }
                // Check for accessories on equipped item
                if (TryComp<UniformAccessoryHolderComponent>(item, out var accessoryHolder))
                {
                    if (_containerSystem.TryGetContainer(item, accessoryHolder.ContainerId, out var container))
                    {
                        foreach (var accessory in container.ContainedEntities)
                        {
                            if (TryComp<JobTitleChangerComponent>(accessory, out var changer2) && changer2.Override && !string.IsNullOrWhiteSpace(changer2.JobTitle))
                            {
                                return changer2.JobTitle;
                            }
                        }
                    }
                }
            }
        }
        // Check uniform accessories directly on the entity (legacy/fallback)
        if (TryComp(uid, out UniformAccessoryHolderComponent? directAccessoryHolder))
        {
            if (_containerSystem.TryGetContainer(uid, directAccessoryHolder.ContainerId, out var container))
            {
                foreach (var accessory in container.ContainedEntities)
                {
                    if (TryComp<JobTitleChangerComponent>(accessory, out var changer) && changer.Override && !string.IsNullOrWhiteSpace(changer.JobTitle))
                    {
                        return changer.JobTitle;
                    }
                }
            }
        }
        return fallback;
    }
}
