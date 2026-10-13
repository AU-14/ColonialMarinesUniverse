// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Server.Stack;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Welding;
using Content.Shared.Clothing;
using Content.Shared.CMU14.Armorsmith;
using Content.Shared.CMU14.Clothing;
using Content.Shared.CMU14.Gunsmith;
using Content.Shared.CMU14.Insurgency.Sapper;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Server.CMU14.Armorsmith;

/// <summary>
/// The armorer's bench. It takes in ingots, sheets and cloth, shows the player the numbers they need (never the answers), and makes armor and helmets from what it
/// holds: the protection follows from the plate and the backing by the formulas of the handbook. An armor that lies on the bench can be painted anew.
/// </summary>
public sealed class ArmorsmithBenchSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private StackSystem _stacks = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private ISerializationManager _serialization = default!;

    private PrototypeArmorsmithData _data = default!;

    public override void Initialize()
    {
        base.Initialize();

        _data = new PrototypeArmorsmithData(_prototypes);

        SubscribeLocalEvent<ArmorsmithBenchComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ArmorsmithBenchComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<ArmorsmithBenchComponent, ArmorsmithBuildDoAfterEvent>(OnBuilt);
        SubscribeLocalEvent<ArmorsmithBenchComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<ArmorsmithBenchComponent, EntRemovedFromContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<CraftedArmorComponent, ExaminedEvent>(OnExamined);

        Subs.BuiEvents<ArmorsmithBenchComponent>(ArmorsmithUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<ArmorsmithBuildMessage>(OnBuild);
            subs.Event<ArmorsmithEjectStockMessage>(OnEjectStock);
            subs.Event<ArmorsmithTakeItemMessage>(OnTakeItem);
            subs.Event<ArmorsmithRecolorMessage>(OnRecolor);
        });
    }

    // ------------------------------------------------------------------ taking things in

    private void OnOpenAttempt(Entity<ArmorsmithBenchComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!HasComp<SapperComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("armorsmith-unskilled"), ent, args.User, PopupType.SmallCaution);
            args.Cancel();
        }
    }

    private void OnOpened(Entity<ArmorsmithBenchComponent> ent, ref BoundUIOpenedEvent args)
    {
        PushState(ent);
    }

    private void OnContainerChanged(Entity<ArmorsmithBenchComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        PushState(ent);
    }

    private void OnContainerChanged(Entity<ArmorsmithBenchComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        PushState(ent);
    }

    /// <summary>The stacks the bench takes in: the metals of the plates and the soft layers.</summary>
    private HashSet<string> AcceptedStacks()
    {
        var set = new HashSet<string>();
        foreach (var plate in _prototypes.EnumeratePrototypes<ArmorsmithPlatePrototype>())
        {
            if (_data.Material(plate.Material) is { } material)
                set.Add(material.StackType);
        }

        foreach (var backing in _prototypes.EnumeratePrototypes<ArmorsmithBackingPrototype>())
            set.Add(backing.StackType);

        // The plant fiber a ghillie suit is woven of.
        set.Add(ArmorsmithMechanics.GhillieFiberStack);

        return set;
    }

    private void OnInteractUsing(Entity<ArmorsmithBenchComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var used = args.Used;
        var isArmor = HasComp<CraftedArmorComponent>(used);
        var isStack = TryComp<StackComponent>(used, out var stack) && AcceptedStacks().Contains(stack.StackTypeId);
        if (!isArmor && !isStack)
            return;

        args.Handled = true;

        if (!HasComp<SapperComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("armorsmith-unskilled"), ent, args.User, PopupType.SmallCaution);
            return;
        }

        if (isArmor)
        {
            var slot = _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.ItemSlot);
            if (slot.Count > 0)
            {
                _popup.PopupEntity(Loc.GetString("armorsmith-item-occupied"), ent, args.User, PopupType.SmallCaution);
                return;
            }

            if (_container.Insert(used, slot))
                _popup.PopupEntity(Loc.GetString("armorsmith-item-placed"), ent, args.User);

            return;
        }

        var type = stack!.StackTypeId.Id;
        var held = ent.Comp.Stock.GetValueOrDefault(type);
        var take = Math.Min(stack.Count, ent.Comp.StockLimit - held);
        if (take <= 0)
        {
            _popup.PopupEntity(Loc.GetString("armorsmith-stock-full"), ent, args.User, PopupType.SmallCaution);
            return;
        }

        ent.Comp.Stock[type] = held + take;
        if (take >= stack.Count)
            QueueDel(used);
        else
            _stacks.SetCount(used, stack.Count - take);

        _popup.PopupEntity(Loc.GetString("armorsmith-stock-taken", ("count", take)), ent, args.User);
        PushState(ent);
    }

    private void OnEjectStock(Entity<ArmorsmithBenchComponent> ent, ref ArmorsmithEjectStockMessage args)
    {
        if (!HasComp<SapperComponent>(args.Actor) || !ent.Comp.Stock.TryGetValue(args.StackType, out var count) || count <= 0)
            return;

        if (!_prototypes.HasIndex<StackPrototype>(args.StackType))
            return;

        ent.Comp.Stock[args.StackType] = 0;
        _stacks.SpawnMultipleNextToOrDrop(new ProtoId<StackPrototype>(args.StackType), count, ent);
        PushState(ent);
    }

    private void OnTakeItem(Entity<ArmorsmithBenchComponent> ent, ref ArmorsmithTakeItemMessage args)
    {
        if (!HasComp<SapperComponent>(args.Actor) || GetBenchItem(ent) is not { } item)
            return;

        if (_container.TryGetContainer(ent, ent.Comp.ItemSlot, out var container) && _container.Remove(item, container))
            _hands.TryPickupAnyHand(args.Actor, item);

        PushState(ent);
    }

    private EntityUid? GetBenchItem(Entity<ArmorsmithBenchComponent> ent)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.ItemSlot, out var container) || container.ContainedEntities.Count == 0)
            return null;

        return container.ContainedEntities[0];
    }

    private void PushState(Entity<ArmorsmithBenchComponent> ent)
    {
        ArmorDesign? design = null;
        if (GetBenchItem(ent) is { } item && TryComp<CraftedArmorComponent>(item, out var crafted))
            design = crafted.Design.Clone();

        var stock = new Dictionary<string, int>();
        foreach (var (type, count) in ent.Comp.Stock)
        {
            if (count > 0)
                stock[type] = count;
        }

        _ui.SetUiState(ent.Owner, ArmorsmithUiKey.Key, new ArmorsmithBuiState(stock, design));
    }

    // ------------------------------------------------------------------ painting anew

    private void OnRecolor(Entity<ArmorsmithBenchComponent> ent, ref ArmorsmithRecolorMessage args)
    {
        if (!HasComp<SapperComponent>(args.Actor) || GetBenchItem(ent) is not { } item || !TryComp<CraftedArmorComponent>(item, out var crafted))
            return;

        Paint(item, crafted, args.Color);
        _popup.PopupEntity(Loc.GetString("armorsmith-recolored"), ent, args.Actor);
        PushState(ent);
    }

    /// <summary>Paints the armor: the grey sprite is tinted. A color is kept to what the screen can show.</summary>
    private void Paint(EntityUid item, CraftedArmorComponent crafted, Color color)
    {
        color = new Color(Math.Clamp(color.R, 0f, 1f), Math.Clamp(color.G, 0f, 1f), Math.Clamp(color.B, 0f, 1f), 1f);
        crafted.Design.Color = color;

        var tint = EnsureComp<AU14CustomClothingColorComponent>(item);
        tint.Color = color;
        Dirty(item, tint);
    }

    // ------------------------------------------------------------------ asking for a build

    private void OnBuild(Entity<ArmorsmithBenchComponent> ent, ref ArmorsmithBuildMessage args)
    {
        if (!HasComp<SapperComponent>(args.Actor))
            return;

        var design = args.Design.Clone();
        var check = ArmorsmithEvaluator.Evaluate(design, _data);
        if (!Accept(ent, args.Actor, check.Errors, check.Result.MaterialUnits))
            return;

        var seconds = Math.Max(0.2, check.Result.BuildSeconds * ent.Comp.TimeScale);
        var doAfter = new DoAfterArgs(EntityManager, args.Actor, TimeSpan.FromSeconds(seconds), new ArmorsmithBuildDoAfterEvent(design), ent, ent)
        {
            BreakOnMove = true,
            BreakOnHandChange = false,
            NeedHand = false,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("armorsmith-working"), ent, args.Actor);
    }

    /// <summary>Tells the player what is wrong with the request, or lets it through (when the bench holds the materials).</summary>
    private bool Accept(Entity<ArmorsmithBenchComponent> ent, EntityUid user, List<string> errors, Dictionary<string, double> units)
    {
        if (errors.Count > 0)
        {
            _popup.PopupEntity(Loc.GetString("armorsmith-invalid", ("what", errors[0])), ent, user, PopupType.SmallCaution);
            return false;
        }

        foreach (var (key, amount) in units)
        {
            var type = StackTypeOf(key);
            var need = (int) Math.Ceiling(amount);
            if (ent.Comp.Stock.GetValueOrDefault(type) < need)
            {
                _popup.PopupEntity(Loc.GetString("armorsmith-lacking", ("what", $"{StackName(type)} ×{need}")), ent, user, PopupType.SmallCaution);
                return false;
            }
        }

        return true;
    }

    /// <summary>The keys of the cost are the ids of the materials of the plates and the types of the stacks of the backing; this finds the stack.</summary>
    private string StackTypeOf(string key) => _data.Material(key)?.StackType ?? key;

    private string StackName(string type) =>
        _prototypes.TryIndex<StackPrototype>(type, out var stack) ? Loc.GetString(stack.Name) : type;

    // ------------------------------------------------------------------ making things

    private void OnBuilt(Entity<ArmorsmithBenchComponent> ent, ref ArmorsmithBuildDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var user = args.User;
        var design = args.Design.Clone();
        var check = ArmorsmithEvaluator.Evaluate(design, _data);
        if (!Accept(ent, user, check.Errors, check.Result.MaterialUnits))
            return;

        foreach (var (key, amount) in check.Result.MaterialUnits)
        {
            var type = StackTypeOf(key);
            ent.Comp.Stock[type] = Math.Max(0, ent.Comp.Stock.GetValueOrDefault(type) - (int) Math.Ceiling(amount));
        }

        var look = _data.Look(design.Look)!;
        var item = Spawn(look.Entity, Transform(ent).Coordinates);
        Apply(item, design, check.Result);
        _hands.TryPickupAnyHand(user, item);
        _popup.PopupEntity(Loc.GetString("armorsmith-built"), ent, user);
        PushState(ent);
    }

    /// <summary>Gives the piece the protection the design comes to, its color, its name and its weight.</summary>
    private void Apply(EntityUid item, ArmorDesign design, ArmorEvaluation e)
    {
        var crafted = EnsureComp<CraftedArmorComponent>(item);
        crafted.Design = design.Clone();
        crafted.MassKg = e.MassKg;
        Paint(item, crafted, design.Color);

        // The protection of the game is a component with a closed set of fields, so it is read from data, the way the prototypes are.
        var node = new MappingDataNode
        {
            ["melee"] = new ValueDataNode(e.Melee.ToString()),
            ["bullet"] = new ValueDataNode(e.Bullet.ToString()),
            ["bio"] = new ValueDataNode(e.Bio.ToString()),
            ["explosionArmor"] = new ValueDataNode(e.Explosion.ToString()),
        };

        var armor = _serialization.Read<CMArmorComponent>(node, notNullableOverride: true);
        EntityManager.AddComponent(item, armor, overwrite: true);

        // The slowdown: the thicker the plate and the more layers, the slower the wearer (a helmet does not slow). The tier follows from how much is left of the speed.
        if (e.SpeedTier.Length > 0)
        {
            var tier = _serialization.Read<RMCArmorSpeedTierComponent>(new MappingDataNode { ["speedTier"] = new ValueDataNode(e.SpeedTier) }, notNullableOverride: true);
            EntityManager.AddComponent(item, tier, overwrite: true);

            var share = e.SpeedShare.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            var speed = _serialization.Read<ClothingSpeedModifierComponent>(new MappingDataNode
            {
                ["walkModifier"] = new ValueDataNode(share),
                ["sprintModifier"] = new ValueDataNode(share),
            }, notNullableOverride: true);
            EntityManager.AddComponent(item, speed, overwrite: true);
        }

        // A closed helmet darkens the sight the way a welding mask does.
        if (e.Closed)
            EnsureComp<WeldingVisionComponent>(item);

        var name = FirearmDesign.CleanText(design.ItemName, ArmorDesign.MaxNameLength);
        var description = FirearmDesign.CleanText(design.ItemDescription, ArmorDesign.MaxDescriptionLength);
        var look = _data.Look(design.Look)!;
        var nameKey = look.Slot switch
        {
            ArmorSlotKind.Head => "armorsmith-item-name-head",
            ArmorSlotKind.Ghillie => "armorsmith-item-name-ghillie",
            _ => "armorsmith-item-name-body",
        };
        _meta.SetEntityName(item, name.Length > 0 ? name : Loc.GetString(nameKey, ("look", Loc.GetString(look.Name))));
        _meta.SetEntityDescription(item, description.Length > 0 ? description : Loc.GetString("armorsmith-item-description"));
    }

    private void OnExamined(Entity<CraftedArmorComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var plate = _data.Material(ent.Comp.Design.PlateMaterial);
        args.PushMarkup(Loc.GetString("armorsmith-examine",
            ("plate", plate == null ? ent.Comp.Design.PlateMaterial : Loc.GetString(plate.Name)),
            ("thickness", ent.Comp.Design.PlateThicknessMm.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)),
            ("mass", ent.Comp.MassKg.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture))));
    }
}
