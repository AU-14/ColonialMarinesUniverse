using Content.Shared._RMC14.UniformAccessories;
using Content.Shared.CMU14.Marines.Roles.Ranks;
using Content.Shared.Item;
using Content.Shared.Verbs;
using Robust.Shared.Containers;

namespace Content.Shared.CMU14.UniformAccessories;

/// <summary>
///     Lets someone hide the chevron on their own clothing: it stays attached and still sets their rank,
///     but is no longer drawn on them.
/// </summary>
public sealed class CMUChevronVisibilitySystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedItemSystem _item = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<UniformAccessoryHolderComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<UniformAccessoryHolderComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess ||
            !args.CanInteract ||
            Transform(ent).ParentUid != args.User ||
            !_container.TryGetContainer(ent, ent.Comp.ContainerId, out var container))
        {
            return;
        }

        foreach (var accessory in container.ContainedEntities)
        {
            if (!HasComp<RankChangerComponent>(accessory) ||
                !TryComp(accessory, out UniformAccessoryComponent? accessoryComp))
            {
                continue;
            }

            var holder = ent.Owner;
            var chevron = accessory;
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString(accessoryComp.Hidden ? "cmu-chevron-show-verb" : "cmu-chevron-hide-verb"),
                IconEntity = GetNetEntity(accessory),
                Act = () => ToggleHidden(holder, chevron),
            });
        }
    }

    private void ToggleHidden(EntityUid holder, EntityUid chevron)
    {
        if (!TryComp(chevron, out UniformAccessoryComponent? accessory))
            return;

        accessory.Hidden = !accessory.Hidden;
        Dirty(chevron, accessory);
        _item.VisualsChanged(holder);
    }
}
