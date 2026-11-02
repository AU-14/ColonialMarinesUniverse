using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Emplacements;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Visor;
using Content.Shared.Actions;
using Content.Shared.Buckle.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.Dropship.AttachmentPoint;

public sealed partial class CMUGunshipCrewIFFSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private VisorSystem _visors = default!;
    [Dependency] private AttachableIFFSystem _iff = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUAdvancedGunshipCrewVisorComponent, ComponentStartup>(OnVisorStartup);
        SubscribeLocalEvent<CMUAdvancedGunshipCrewVisorComponent, ComponentShutdown>(OnVisorShutdown);
        SubscribeLocalEvent<CMUAdvancedGunshipCrewVisorComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<CMUAdvancedGunshipCrewVisorComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<CMUGunshipCrewIFFComponent, ComponentShutdown>(OnWearerShutdown);
        SubscribeLocalEvent<CMUGunshipCrewIFFComponent, SkillChangedEvent>(OnSkillChanged);
        SubscribeLocalEvent<CMUGunshipCrewIFFComponent, CMUToggleGunshipCrewIFFActionEvent>(OnToggle);
    }

    private bool TryGetWearer(EntityUid helmet, out EntityUid wearer)
    {
        wearer = Transform(helmet).ParentUid;
        return _inventory.TryGetSlotEntity(wearer, "head", out var worn) && worn == helmet;
    }

    private void OnVisorStartup(Entity<CMUAdvancedGunshipCrewVisorComponent> ent, ref ComponentStartup args)
    {
        if (!_net.IsClient && TryGetWearer(ent, out var wearer))
            Apply(wearer, ent);
    }

    private void OnEquipped(Entity<CMUAdvancedGunshipCrewVisorComponent> ent, ref GotEquippedEvent args)
    {
        if (!_net.IsClient && args.Slot == "head")
            Apply(args.EquipTarget, ent);
    }

    private void Apply(EntityUid wearer, EntityUid helmet)
    {
        if (!_skills.HasSkill(wearer, "CMUSkillAircrew", 1))
        {
            SwitchUp(wearer, helmet);
            return;
        }

        var iff = EnsureComp<CMUGunshipCrewIFFComponent>(wearer);
        iff.Helmet = helmet;
        _actions.AddAction(wearer, ref iff.ToggleAction, "CMUActionGunshipCrewIFF");
        _actions.SetToggled(iff.ToggleAction, iff.Enabled);
        Dirty(wearer, iff);
    }

    private void OnVisorShutdown(Entity<CMUAdvancedGunshipCrewVisorComponent> ent, ref ComponentShutdown args)
    {
        if (!_net.IsClient && TryGetWearer(ent, out var wearer))
            Remove(wearer, ent);
    }

    private void OnUnequipped(Entity<CMUAdvancedGunshipCrewVisorComponent> ent, ref GotUnequippedEvent args)
    {
        if (!_net.IsClient && args.Slot == "head")
            Remove(args.EquipTarget, ent);
    }

    private void Remove(EntityUid wearer, EntityUid helmet)
    {
        if (TryComp(wearer, out CMUGunshipCrewIFFComponent? iff) && iff.Helmet == helmet)
            RemComp<CMUGunshipCrewIFFComponent>(wearer);
    }

    private void OnWearerShutdown(Entity<CMUGunshipCrewIFFComponent> ent, ref ComponentShutdown args)
    {
        if (_net.IsClient || ent.Comp.ToggleAction is not { } action)
            return;

        _actions.RemoveAction(ent.Owner, action);
        QueueDel(action);
        ent.Comp.ToggleAction = null;
    }

    private void OnSkillChanged(Entity<CMUGunshipCrewIFFComponent> ent, ref SkillChangedEvent args)
    {
        if (!_net.IsClient && !_skills.HasSkill(ent.Owner, "CMUSkillAircrew", 1))
            SwitchUp(ent.Owner, ent.Comp.Helmet);
    }

    private void SwitchUp(EntityUid wearer, EntityUid helmet)
    {
        if (TryComp(helmet, out CycleableVisorComponent? cycleable))
            _visors.CMUSwitchVisorUp((helmet, cycleable), wearer);
    }

    private void OnToggle(Entity<CMUGunshipCrewIFFComponent> ent, ref CMUToggleGunshipCrewIFFActionEvent args)
    {
        if (args.Handled || !_skills.HasSkill(ent.Owner, "CMUSkillAircrew", 1) ||
            !TryGetWearer(ent.Comp.Helmet, out var wearer) || wearer != ent.Owner ||
            !HasComp<CMUAdvancedGunshipCrewVisorComponent>(ent.Comp.Helmet))
            return;

        args.Handled = true;
        ent.Comp.Enabled = !ent.Comp.Enabled;
        Dirty(ent);
        _actions.SetToggled(ent.Comp.ToggleAction, ent.Comp.Enabled);
        _popup.PopupClient(Loc.GetString(ent.Comp.Enabled ? "cmu-gunship-crew-iff-on" : "cmu-gunship-crew-iff-off"), ent, ent);
    }

    public void CheckGunshipShot(Entity<MountableWeaponComponent> weapon, ref AttemptShootEvent args)
    {
        if (args.Cancelled || !TryComp(args.User, out CMUGunshipCrewIFFComponent? iff) || !iff.Enabled ||
            !_skills.HasSkill(args.User, "CMUSkillAircrew", 1) ||
            !TryGetWearer(iff.Helmet, out var wearer) || wearer != args.User ||
            !HasComp<CMUAdvancedGunshipCrewVisorComponent>(iff.Helmet) ||
            !TryComp(args.User, out BuckleComponent? buckle) || buckle.BuckledTo is not { } mount ||
            !HasComp<CMUDoorGunVisualComponent>(mount) ||
            !TryComp(mount, out WeaponMountComponent? mounted) || mounted.User != args.User || mounted.MountedEntity != weapon.Owner ||
            Transform(mount).GridUid is not { } ship || !HasComp<DropshipComponent>(ship))
            return;

        _iff.CMUCheckPreventGunshipHit(weapon.Owner, mount, ship, ref args);
    }
}
