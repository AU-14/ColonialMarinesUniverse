using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.Materials;
using Content.Server.Stack;
using Content.Shared._AU14.Insurgency.Sapper;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared.Construction.Components;
using Content.Shared.Doors.Electronics;
using Content.Shared.PowerCell;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Materials;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._AU14.Insurgency.Sapper;

/// <summary>
///     Custom Sapper's Workbench UI and server actions. The bench has two faces:
///     gunsmithing, which force-fits attachments by broad slot category, and trap crafting, which consumes
///     stored materials from the bench instead of using the stock lathe window.
/// </summary>
public sealed partial class SapperWorkbenchSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private MaterialStorageSystem _materials = default!;
    [Dependency] private SharedStackSystem _stacks = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private AttachableHolderSystem _attachableHolder = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    // How far (in tiles) from the bench loose ingredient items still count for recipes.
    private const float IngredientRange = 1.2f;

    private static readonly ProtoId<TagPrototype> CableCoilTag = "CableCoil";

    private static readonly Dictionary<string, ProtoId<TagPrototype>> SlotCategoryTags = new()
    {
        { "rmc-aslot-rail", "RMCAttachmentRail" },
        { "rmc-aslot-barrel", "RMCAttachmentBarrel" },
        { "rmc-aslot-underbarrel", "RMCAttachmentUnderbarrel" },
        { "rmc-aslot-stock", "RMCAttachmentStock" },
    };

    // Raw MaterialStorage units in ONE sheet/plank, taken from each sheet entity's
    // PhysicalComposition (see _RMC14/Entities/Objects/Materials/Sheets/*.yml). This is what makes
    // "50 sheets in" display as 50, and it is also the refund granularity when ejecting.
    private static readonly Dictionary<string, int> MaterialDisplayUnits = new()
    {
        { "CMSteel", 3750 },
        { "CMPlasteel", 3750 },
        { "RMCWood", 3750 },
        { "RMCPlastic", 2000 },
    };

    private static readonly Dictionary<string, string> MaterialDisplayNames = new()
    {
        { "CMSteel", "Metal Sheets" },
        { "CMPlasteel", "Plasteel Sheets" },
        { "RMCWood", "Wooden Planks" },
        { "RMCPlastic", "Plastic Sheets" },
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SapperWorkbenchKitComponent, UseInHandEvent>(OnKitUseInHand);
        SubscribeLocalEvent<SapperWorkbenchKitComponent, SapperWorkbenchDeployDoAfterEvent>(OnKitDeployed);

        SubscribeLocalEvent<SapperWorkbenchComponent, InteractUsingEvent>(OnBenchInteractUsing);
        SubscribeLocalEvent<SapperWorkbenchComponent, SapperWorkbenchCraftDoAfterEvent>(OnCraftBuilt);
        SubscribeLocalEvent<SapperWorkbenchComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<SapperWorkbenchComponent, MaterialAmountChangedEvent>(OnMaterialChanged);
        SubscribeLocalEvent<SapperWorkbenchComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<SapperWorkbenchComponent, EntRemovedFromContainerMessage>(OnContainerChanged);

        Subs.BuiEvents<SapperWorkbenchComponent>(SapperWorkbenchUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<SapperWorkbenchAddAttachmentMessage>(OnAddAttachment);
            subs.Event<SapperWorkbenchRemoveAttachmentMessage>(OnRemoveAttachment);
            subs.Event<SapperWorkbenchTakeWeaponMessage>(OnTakeWeapon);
            subs.Event<SapperWorkbenchCraftMessage>(OnCraft);
            subs.Event<SapperWorkbenchEjectMaterialMessage>(OnEjectMaterial);
        });
    }

    private void OnOpenAttempt(Entity<SapperWorkbenchComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!HasComp<SapperComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("insfor-sapper-trap-unskilled"), ent, args.User, PopupType.SmallCaution);
            args.Cancel();
        }
    }

    private void OnOpened(Entity<SapperWorkbenchComponent> ent, ref BoundUIOpenedEvent args)
    {
        PushState(ent);
    }

    private void OnMaterialChanged(Entity<SapperWorkbenchComponent> ent, ref MaterialAmountChangedEvent args)
    {
        PushState(ent);
    }

    private void OnContainerChanged(Entity<SapperWorkbenchComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        PushState(ent);
    }

    private void OnContainerChanged(Entity<SapperWorkbenchComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        PushState(ent);
    }

    private void OnKitUseInHand(Entity<SapperWorkbenchKitComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!HasComp<SapperComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("insfor-sapper-trap-unskilled"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.DeployTime, new SapperWorkbenchDeployDoAfterEvent(), ent, ent)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnHandChange = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnKitDeployed(Entity<SapperWorkbenchKitComponent> ent, ref SapperWorkbenchDeployDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var coords = Transform(args.User).Coordinates;
        var bench = Spawn(ent.Comp.WorkbenchPrototype, coords);
        var benchXform = Transform(bench);
        if (!benchXform.Anchored && benchXform.GridUid != null)
            _transform.AnchorEntity(bench, benchXform);

        _popup.PopupEntity(Loc.GetString("insfor-sapper-workbench-deployed"), bench, args.User);
        QueueDel(ent);
    }

    private void OnBenchInteractUsing(Entity<SapperWorkbenchComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!HasComp<SapperComponent>(args.User))
        {
            if (HasComp<AttachableHolderComponent>(args.Used) ||
                HasComp<AttachableComponent>(args.Used) ||
                HasComp<MaterialComponent>(args.Used) ||
                _tag.HasTag(args.Used, CableCoilTag))
            {
                _popup.PopupEntity(Loc.GetString("insfor-sapper-trap-unskilled"), args.User, args.User, PopupType.SmallCaution);
                args.Handled = true;
            }

            return;
        }

        if (TryInsertWeapon(ent, args.Used, args.User))
        {
            args.Handled = true;
            return;
        }

        if (TryInsertMaterial(ent, args.Used, args.User))
            args.Handled = true;

        // The siphon rig is a normal recipe now (cable placed next to the bench), so applying a
        // coil directly no longer builds anything.
    }

    private bool TryInsertWeapon(Entity<SapperWorkbenchComponent> ent, EntityUid used, EntityUid user)
    {
        if (!HasComp<AttachableHolderComponent>(used))
            return false;

        var weaponSlot = _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.WeaponContainer);
        if (weaponSlot.Count > 0)
        {
            _popup.PopupEntity(Loc.GetString("insfor-sapper-workbench-weapon-occupied"), ent, user, PopupType.SmallCaution);
            return true;
        }

        if (_container.Insert(used, weaponSlot))
            _popup.PopupEntity(Loc.GetString("insfor-sapper-workbench-weapon-placed"), ent, user);

        return true;
    }

    private bool TryInsertMaterial(Entity<SapperWorkbenchComponent> ent, EntityUid used, EntityUid user)
    {
        if (!HasComp<MaterialComponent>(used))
            return false;

        return _materials.TryInsertMaterialEntity(user, used, ent);
    }

    private void OnAddAttachment(Entity<SapperWorkbenchComponent> ent, ref SapperWorkbenchAddAttachmentMessage args)
    {
        if (!HasComp<SapperComponent>(args.Actor) || GetBenchWeapon(ent) is not { } weapon)
            return;

        if (!_hands.TryGetActiveItem(args.Actor, out var attachment) || attachment == null)
        {
            _popup.PopupEntity(Loc.GetString("insfor-sapper-workbench-hold-attachment"), ent, args.Actor, PopupType.SmallCaution);
            return;
        }

        if (!CanSlotCategoryAccept(args.SlotId, attachment.Value))
        {
            _popup.PopupEntity(Loc.GetString("insfor-sapper-workbench-wrong-slot"), ent, args.Actor, PopupType.SmallCaution);
            return;
        }

        if (!_attachableHolder.HasSlot((weapon.Owner, weapon.Comp), args.SlotId))
            return;

        var container = _container.EnsureContainer<ContainerSlot>(weapon, args.SlotId);
        if (container.Count > 0)
        {
            _popup.PopupEntity(Loc.GetString("insfor-sapper-workbench-slots-full"), ent, args.Actor, PopupType.SmallCaution);
            return;
        }

        container.OccludesLight = false;
        if (_container.Insert(attachment.Value, container))
            _popup.PopupEntity(Loc.GetString("insfor-sapper-workbench-attached", ("slot", SlotDisplay(args.SlotId))), ent, args.Actor);

        PushState(ent);
    }

    private void OnRemoveAttachment(Entity<SapperWorkbenchComponent> ent, ref SapperWorkbenchRemoveAttachmentMessage args)
    {
        if (!HasComp<SapperComponent>(args.Actor) || GetBenchWeapon(ent) is not { } weapon)
            return;

        if (!_container.TryGetContainer(weapon, args.SlotId, out var container) || container.Count == 0)
            return;

        var attachment = container.ContainedEntities[0];
        _attachableHolder.Detach(weapon, attachment, args.Actor, args.SlotId);
        PushState(ent);
    }

    private void OnTakeWeapon(Entity<SapperWorkbenchComponent> ent, ref SapperWorkbenchTakeWeaponMessage args)
    {
        if (!HasComp<SapperComponent>(args.Actor) || GetBenchWeapon(ent) is not { } weapon)
            return;

        if (_container.TryGetContainer(ent, ent.Comp.WeaponContainer, out var container) &&
            _container.Remove(weapon.Owner, container))
        {
            _hands.TryPickupAnyHand(args.Actor, weapon);
        }

        PushState(ent);
    }

    private void OnEjectMaterial(Entity<SapperWorkbenchComponent> ent, ref SapperWorkbenchEjectMaterialMessage args)
    {
        if (!HasComp<SapperComponent>(args.Actor) || !MaterialDisplayUnits.TryGetValue(args.MaterialId, out var unit))
            return;

        // Eject whole sheets only; partial leftovers from crafting stay in the bench.
        var sheets = _materials.GetMaterialAmount(ent, args.MaterialId) / unit;
        if (sheets <= 0)
            return;

        var amount = sheets * unit;
        if (!_materials.TryChangeMaterialAmount(ent.Owner, args.MaterialId, -amount))
            return;

        _materials.SpawnMultipleFromMaterial(amount, args.MaterialId, Transform(ent).Coordinates);
        PushState(ent);
    }

    private void OnCraft(Entity<SapperWorkbenchComponent> ent, ref SapperWorkbenchCraftMessage args)
    {
        if (!HasComp<SapperComponent>(args.Actor) ||
            args.RecipeIndex < 0 ||
            args.RecipeIndex >= ent.Comp.Recipes.Count)
        {
            return;
        }

        var recipe = ent.Comp.Recipes[args.RecipeIndex];
        if (!CanBuildRecipe(ent, recipe, GetNearbyItems(ent)))
        {
            _popup.PopupEntity(Loc.GetString("insfor-sapper-workbench-need-materials"), ent, args.Actor, PopupType.SmallCaution);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.Actor, recipe.BuildTime, new SapperWorkbenchCraftDoAfterEvent(args.RecipeIndex), ent, ent)
        {
            NeedHand = false,
            BreakOnMove = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnCraftBuilt(Entity<SapperWorkbenchComponent> ent, ref SapperWorkbenchCraftDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (args.RecipeIndex < 0 || args.RecipeIndex >= ent.Comp.Recipes.Count)
            return;

        var recipe = ent.Comp.Recipes[args.RecipeIndex];
        var nearby = GetNearbyItems(ent);
        if (!CanBuildRecipe(ent, recipe, nearby))
            return;

        args.Handled = true;

        foreach (var (material, amount) in recipe.Materials)
            _materials.TryChangeMaterialAmount(ent.Owner, material, -ToRawMaterialAmount(material, amount));

        ConsumeItems(recipe, nearby);

        Spawn(recipe.Prototype, Transform(ent).Coordinates);
        _popup.PopupEntity(Loc.GetString("insfor-sapper-workbench-crafted", ("item", recipe.Name)), ent, args.User);
        PushState(ent);
    }

    private bool CanBuildRecipe(Entity<SapperWorkbenchComponent> ent, SapperWorkbenchRecipe recipe, List<EntityUid> nearby)
    {
        foreach (var (material, amount) in recipe.Materials)
        {
            if (GetDisplayMaterialAmount(ent, material) < amount)
                return false;
        }

        // One entity may satisfy only ONE requirement (a board that is somehow also a power cell
        // must not be counted for both), so requirements claim entities as they count them.
        var claimed = new HashSet<EntityUid>();
        foreach (var req in recipe.Items)
        {
            var have = 0;
            foreach (var uid in nearby)
            {
                if (claimed.Contains(uid))
                    continue;

                var give = Contribution(uid, req);
                if (give <= 0)
                    continue;

                claimed.Add(uid);
                have += give;
                if (have >= req.Count)
                    break;
            }

            if (have < req.Count)
                return false;
        }

        return true;
    }

    // ----- loose-item ingredients (placed on or next to the bench) -----------------------------

    private List<EntityUid> GetNearbyItems(Entity<SapperWorkbenchComponent> ent)
    {
        var items = new List<EntityUid>();
        foreach (var uid in _lookup.GetEntitiesInRange(Transform(ent).Coordinates, IngredientRange))
        {
            if (uid == ent.Owner || _container.IsEntityInContainer(uid))
                continue;

            items.Add(uid);
        }

        return items;
    }

    // How many units this entity contributes toward the requirement: stack count for stacks,
    // remaining shells for ammo handfuls, one for anything else. Zero when it doesn't match.
    private int Contribution(EntityUid uid, SapperWorkbenchItemRequirement req)
    {
        if (!MatchesRequirement(uid, req))
            return 0;

        if (TryComp(uid, out StackComponent? stack))
            return stack.Count;

        if (TryComp(uid, out BallisticAmmoProviderComponent? ammo))
            return ammo.Count;

        return 1;
    }

    private bool MatchesRequirement(EntityUid uid, SapperWorkbenchItemRequirement req)
    {
        // "Any electronics" means exactly that: every kind of circuit board counts.
        if (req.AnyElectronics)
        {
            return HasComp<MachineBoardComponent>(uid) ||
                   HasComp<ComputerBoardComponent>(uid) ||
                   HasComp<DoorElectronicsComponent>(uid);
        }

        if (req.AnyPowerCell)
            return HasComp<PowerCellComponent>(uid);

        // Any cable coil at all: the tag, or any stack whose type mentions Cable (RMCCable,
        // CableApc/MV/HV...). Deliberately broad to kill the "which coil?" confusion.
        if (req.AnyCable)
        {
            return _tag.HasTag(uid, CableCoilTag) ||
                   (TryComp(uid, out StackComponent? cableStack) &&
                    cableStack.StackTypeId.Contains("Cable", StringComparison.OrdinalIgnoreCase));
        }

        if (req.Prototype is { } proto)
            return MetaData(uid).EntityPrototype?.ID == proto;

        if (req.StackType is { } type)
            return TryComp(uid, out StackComponent? stack) && stack.StackTypeId == type;

        if (req.Tag is { } tag)
            return _tag.HasTag(uid, tag);

        return false;
    }

    private void ConsumeItems(SapperWorkbenchRecipe recipe, List<EntityUid> nearby)
    {
        // Mirrors the claim rule in CanBuildRecipe: an entity consumed (or partially used) for one
        // requirement is never counted again for a later one.
        var claimed = new HashSet<EntityUid>();
        foreach (var req in recipe.Items)
        {
            var need = req.Count;
            foreach (var uid in nearby)
            {
                if (need <= 0)
                    break;

                if (claimed.Contains(uid))
                    continue;

                var give = Contribution(uid, req);
                if (give <= 0)
                    continue;

                claimed.Add(uid);

                if (TryComp(uid, out StackComponent? stack))
                {
                    // Stacks shrink precisely; only what the recipe needs is taken.
                    var take = Math.Min(stack.Count, need);
                    _stacks.SetCount(uid, stack.Count - take, stack);
                    need -= take;
                }
                else
                {
                    // Non-stacks (handfuls, boards, IEDs) are consumed whole.
                    need -= give;
                    QueueDel(uid);
                }
            }
        }
    }

    private bool CanSlotCategoryAccept(string slotId, EntityUid attachment)
    {
        if (!HasComp<AttachableComponent>(attachment))
            return false;

        return SlotCategoryTags.TryGetValue(slotId, out var tag) && _tag.HasTag(attachment, tag);
    }

    private Entity<AttachableHolderComponent>? GetBenchWeapon(Entity<SapperWorkbenchComponent> bench)
    {
        if (!_container.TryGetContainer(bench, bench.Comp.WeaponContainer, out var container) || container.Count == 0)
            return null;

        var weapon = container.ContainedEntities[0];
        if (!TryComp(weapon, out AttachableHolderComponent? holder))
            return null;

        return (weapon, holder);
    }

    private void PushState(Entity<SapperWorkbenchComponent> ent)
    {
        var weapon = GetBenchWeapon(ent);
        var slots = new List<SapperWorkbenchSlotState>();
        // Same-named stats from different attachments sum into one line (two +0.5 accuracy
        // attachments show "Accuracy: +1"), so the aggregation happens before lines are built.
        var statTotals = new Dictionary<string, (float Value, bool GoodWhenPositive)>();

        if (weapon != null)
        {
            foreach (var slotId in weapon.Value.Comp.Slots.Keys)
            {
                string? attachmentName = null;
                var canRemove = false;
                if (_container.TryGetContainer(weapon.Value, slotId, out var container) && container.Count > 0)
                {
                    var attachment = container.ContainedEntities[0];
                    attachmentName = Name(attachment);
                    canRemove = true;
                    AddStats(statTotals, attachment);
                }

                // Deliberately NOT gated on what the viewer holds: doing that greyed out every
                // other slot after an add/remove (the detached attachment lands in the hand).
                // OnAddAttachment still validates category fit server-side with a popup.
                var canAdd = SlotCategoryTags.ContainsKey(slotId) && !canRemove;

                slots.Add(new SapperWorkbenchSlotState(slotId, SlotDisplay(slotId), attachmentName, canAdd, canRemove));
            }
        }

        var stats = new List<SapperWorkbenchStatLine>();
        foreach (var (name, (value, goodWhenPositive)) in statTotals.OrderBy(kvp => kvp.Key))
        {
            if (Math.Abs(value) < 0.001f)
                continue; // Attachments cancelled each other out; nothing to show.

            var buff = goodWhenPositive ? value > 0 : value < 0;
            stats.Add(new SapperWorkbenchStatLine($"{name}: {value:+0.##;-0.##}", buff));
        }

        var materials = TryComp(ent, out MaterialStorageComponent? materialStorage)
            ? _materials.GetStoredMaterials((ent.Owner, materialStorage))
                .Select(kvp => new SapperWorkbenchMaterialState(
                    kvp.Key.Id,
                    MaterialDisplay(kvp.Key.Id),
                    ToDisplayMaterialAmount(kvp.Key.Id, kvp.Value)))
                .Where(m => m.Count > 0)
                .OrderBy(m => m.Name)
                .ToList()
            : new List<SapperWorkbenchMaterialState>();
        var recipes = new List<SapperWorkbenchRecipeState>();
        var nearby = GetNearbyItems(ent);
        for (var i = 0; i < ent.Comp.Recipes.Count; i++)
        {
            var recipe = ent.Comp.Recipes[i];
            recipes.Add(new SapperWorkbenchRecipeState(
                i,
                recipe.Name,
                recipe.Prototype.Id,
                recipe.Materials.ToDictionary(kvp => MaterialDisplay(kvp.Key), kvp => kvp.Value),
                recipe.Items.Select(req => new SapperWorkbenchIngredientState(req.Name, req.Count, req.IconPrototype)).ToList(),
                CanBuildRecipe(ent, recipe, nearby)));
        }

        _ui.SetUiState(ent.Owner, SapperWorkbenchUiKey.Key, new SapperWorkbenchBuiState(
            weapon != null ? Name(weapon.Value.Owner) : null,
            weapon != null ? MetaData(weapon.Value.Owner).EntityPrototype?.ID : null,
            slots,
            stats,
            materials,
            recipes));
    }

    private void AddStats(Dictionary<string, (float Value, bool GoodWhenPositive)> stats, EntityUid attachment)
    {
        if (TryComp(attachment, out AttachableWeaponRangedModsComponent? ranged))
        {
            foreach (var mod in ranged.Modifiers)
            {
                AddStat(stats, "Accuracy", (float) mod.AccuracyAddMult.Double());
                AddStat(stats, "Damage falloff", (float) mod.DamageFalloffAddMult.Double(), goodWhenPositive: false);
                AddStat(stats, "Burst scatter", (float) mod.BurstScatterAddMult, goodWhenPositive: false);
                AddStat(stats, "Shots per burst", mod.ShotsPerBurstFlat);
                AddStat(stats, "Damage", (float) mod.DamageAddMult.Double());
                AddStat(stats, "Recoil", mod.RecoilFlat, goodWhenPositive: false);
                AddStat(stats, "Scatter", (float) mod.ScatterFlat, goodWhenPositive: false);
                AddStat(stats, "Fire delay", mod.FireDelayFlat, goodWhenPositive: false);
                AddStat(stats, "Projectile speed", mod.ProjectileSpeedFlat);
                AddStat(stats, "Range", mod.RangeFlat);
            }
        }

        if (TryComp(attachment, out AttachableSpeedModsComponent? speed))
        {
            foreach (var mod in speed.Modifiers)
            {
                AddStat(stats, "Walk speed", mod.Walk);
                AddStat(stats, "Sprint speed", mod.Sprint);
            }
        }

        if (TryComp(attachment, out AttachableSizeModsComponent? size))
        {
            foreach (var mod in size.Modifiers)
                AddStat(stats, "Item size", mod.Size, goodWhenPositive: false);
        }

        if (TryComp(attachment, out AttachableWieldDelayModsComponent? wield))
        {
            foreach (var mod in wield.Modifiers)
                AddStat(stats, "Wield delay", (float) mod.Delay.TotalSeconds, goodWhenPositive: false);
        }
    }

    private static void AddStat(Dictionary<string, (float Value, bool GoodWhenPositive)> stats, string name, float value, bool goodWhenPositive = true)
    {
        if (Math.Abs(value) < 0.001f)
            return;

        var total = stats.TryGetValue(name, out var existing) ? existing.Value + value : value;
        stats[name] = (total, goodWhenPositive);
    }

    private static string SlotDisplay(string slotId) => slotId switch
    {
        "rmc-aslot-rail" => "Rail",
        "rmc-aslot-barrel" => "Barrel",
        "rmc-aslot-underbarrel" => "Underbarrel",
        "rmc-aslot-stock" => "Stock",
        _ => slotId,
    };

    private int GetDisplayMaterialAmount(EntityUid uid, string material)
    {
        return ToDisplayMaterialAmount(material, _materials.GetMaterialAmount(uid, material));
    }

    private static int ToRawMaterialAmount(string material, int displayAmount)
    {
        return displayAmount * MaterialDisplayUnits.GetValueOrDefault(material, 1);
    }

    private static int ToDisplayMaterialAmount(string material, int rawAmount)
    {
        var unit = MaterialDisplayUnits.GetValueOrDefault(material, 1);
        return rawAmount / unit;
    }

    private static string MaterialDisplay(string material)
    {
        return MaterialDisplayNames.GetValueOrDefault(material, material);
    }
}
