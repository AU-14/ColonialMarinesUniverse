// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Server.Stack;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.CMU14.Gunsmith;
using Content.Shared.CMU14.Insurgency.Sapper;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Gunsmith;

/// <summary>
/// The ammunition bench. Based on the mod "CE Custom Ammo": a cartridge is made of parts (the core, the jacket and the tip of the bullet, the
/// casing, the powder load), each of a material of its own that does something to the cartridge; on top of that the core can be hollow and filled
/// with a liquid poured into the bench from a flamethrower's tank or any other vessel: fuel makes a bullet that burns, acid a bullet that eats,
/// a poison a bullet that poisons. The numbers are worked out by the formulas of the handbook; the window never says what they come to.
/// </summary>
public sealed class AmmoBenchSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private StackSystem _stacks = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private GunsmithCatalogSystem _catalog = default!;
    [Dependency] private RMCReagentSystem _reagent = default!;

    private PrototypeAmmoData _data = default!;

    public override void Initialize()
    {
        base.Initialize();

        _data = new PrototypeAmmoData(_prototypes);

        SubscribeLocalEvent<AmmoBenchComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<AmmoBenchComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<AmmoBenchComponent, AmmoBuildDoAfterEvent>(OnBuilt);
        SubscribeLocalEvent<AmmoBenchComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<AmmoBenchComponent, EntRemovedFromContainerMessage>(OnContainerChanged);

        Subs.BuiEvents<AmmoBenchComponent>(AmmoBenchUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<AmmoBuildMessage>(OnBuild);
            subs.Event<AmmoEjectStockMessage>(OnEjectStock);
            subs.Event<AmmoDrainTankMessage>(OnDrainTank);
            subs.Event<AmmoTakeItemMessage>(OnTakeItem);
        });
    }

    private void OnOpenAttempt(Entity<AmmoBenchComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!HasComp<SapperComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("gunsmith-unskilled"), ent, args.User, PopupType.SmallCaution);
            args.Cancel();
        }
    }

    private void OnOpened(Entity<AmmoBenchComponent> ent, ref BoundUIOpenedEvent args) => PushState(ent);

    private void OnContainerChanged(Entity<AmmoBenchComponent> ent, ref EntInsertedIntoContainerMessage args) => PushState(ent);

    private void OnContainerChanged(Entity<AmmoBenchComponent> ent, ref EntRemovedFromContainerMessage args) => PushState(ent);

    // ------------------------------------------------------------------ taking things in

    private HashSet<string> AcceptedStacks(Entity<AmmoBenchComponent> ent)
    {
        var set = new HashSet<string> { ent.Comp.PowderStack };
        foreach (var material in _prototypes.EnumeratePrototypes<GunsmithMaterialPrototype>())
            set.Add(material.StackType);

        return set;
    }

    private void OnInteractUsing(Entity<AmmoBenchComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var used = args.Used;
        var isStack = TryComp<StackComponent>(used, out var stackProbe) && AcceptedStacks(ent).Contains(stackProbe.StackTypeId);
        var isSource = _catalog.IsSource(used);
        var holdsLiquid = !isStack && !isSource && HasPourable(used);
        if (!isStack && !isSource && !holdsLiquid)
            return;

        args.Handled = true;

        if (!HasComp<SapperComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("gunsmith-unskilled"), ent, args.User, PopupType.SmallCaution);
            return;
        }

        if (isSource)
        {
            if (!_catalog.TryGetCaliber(used, out _))
            {
                _popup.PopupEntity(Loc.GetString("ammo-item-unknown"), ent, args.User, PopupType.SmallCaution);
                return;
            }

            var slot = _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.ItemSlot);
            if (slot.Count > 0)
            {
                _popup.PopupEntity(Loc.GetString("ammo-item-occupied"), ent, args.User, PopupType.SmallCaution);
                return;
            }

            if (_container.Insert(used, slot))
                _popup.PopupEntity(Loc.GetString("ammo-item-placed"), ent, args.User);

            return;
        }

        if (holdsLiquid)
        {
            Pour(ent, used, args.User);
            return;
        }

        var stack = Comp<StackComponent>(used);
        var type = stack.StackTypeId.Id;
        var held = ent.Comp.Stock.GetValueOrDefault(type);
        var take = Math.Min(stack.Count, ent.Comp.StockLimit - held);
        if (take <= 0)
        {
            _popup.PopupEntity(Loc.GetString("gunsmith-stock-full"), ent, args.User, PopupType.SmallCaution);
            return;
        }

        ent.Comp.Stock[type] = held + take;
        if (take >= stack.Count)
            QueueDel(used);
        else
            _stacks.SetCount(used, stack.Count - take);

        _popup.PopupEntity(Loc.GetString("gunsmith-stock-taken", ("count", take)), ent, args.User);
        PushState(ent);
    }

    // ------------------------------------------------------------------ the liquids

    /// <summary>Whether the thing holds a liquid that can be poured out of it: a tank of a flamethrower, a canister, a beaker, a can.</summary>
    private bool HasPourable(EntityUid used)
    {
        foreach (var (_, solution) in _solutions.EnumerateSolutions(used))
        {
            if (solution.Comp.Solution.Volume > FixedPoint2.Zero)
                return true;
        }

        return false;
    }

    private bool TryGetTank(Entity<AmmoBenchComponent> ent, out Entity<SolutionComponent> tank)
    {
        if (_solutions.TryGetSolution(ent.Owner, ent.Comp.TankSolution, out var found))
        {
            tank = found.Value;
            return true;
        }

        tank = default;
        return false;
    }

    /// <summary>Pours the liquid of a vessel into the tank of the bench: the first solution that holds anything, as much as the tank has room for.</summary>
    private void Pour(Entity<AmmoBenchComponent> ent, EntityUid vessel, EntityUid user)
    {
        if (!TryGetTank(ent, out var tank))
            return;

        foreach (var (_, source) in _solutions.EnumerateSolutions(vessel))
        {
            var volume = source.Comp.Solution.Volume;
            if (volume <= FixedPoint2.Zero)
                continue;

            var room = tank.Comp.Solution.MaxVolume - tank.Comp.Solution.Volume;
            var take = FixedPoint2.Min(FixedPoint2.Min(volume, room), FixedPoint2.New((float) ent.Comp.PourUnits));
            if (take <= FixedPoint2.Zero)
            {
                _popup.PopupEntity(Loc.GetString("ammo-tank-full"), ent, user, PopupType.SmallCaution);
                return;
            }

            var poured = _solutions.SplitSolution(source, take);
            _solutions.TryAddSolution(tank, poured);
            _popup.PopupEntity(Loc.GetString("ammo-poured", ("units", take.Float())), ent, user);
            PushState(ent);
            return;
        }
    }

    private void OnDrainTank(Entity<AmmoBenchComponent> ent, ref AmmoDrainTankMessage args)
    {
        if (!HasComp<SapperComponent>(args.Actor) || !TryGetTank(ent, out var tank))
            return;

        _solutions.RemoveAllSolution(tank);
        PushState(ent);
    }

    // ------------------------------------------------------------------ the rest of the things on the bench

    private void OnEjectStock(Entity<AmmoBenchComponent> ent, ref AmmoEjectStockMessage args)
    {
        if (!HasComp<SapperComponent>(args.Actor) || !ent.Comp.Stock.TryGetValue(args.StackType, out var count) || count <= 0)
            return;

        if (!_prototypes.HasIndex<StackPrototype>(args.StackType))
            return;

        ent.Comp.Stock[args.StackType] = 0;
        _stacks.SpawnMultipleNextToOrDrop(new ProtoId<StackPrototype>(args.StackType), count, ent);
        PushState(ent);
    }

    private void OnTakeItem(Entity<AmmoBenchComponent> ent, ref AmmoTakeItemMessage args)
    {
        if (!HasComp<SapperComponent>(args.Actor) || GetItem(ent) is not { } item)
            return;

        if (_container.TryGetContainer(ent, ent.Comp.ItemSlot, out var container) && _container.Remove(item, container))
            _hands.TryPickupAnyHand(args.Actor, item);

        PushState(ent);
    }

    private EntityUid? GetItem(Entity<AmmoBenchComponent> ent)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.ItemSlot, out var container) || container.ContainedEntities.Count == 0)
            return null;

        return container.ContainedEntities[0];
    }

    private CaliberSpec? ItemCaliber(Entity<AmmoBenchComponent> ent)
    {
        if (GetItem(ent) is not { } item)
            return null;

        return _catalog.TryGetCaliber(item, out var caliber) ? caliber : null;
    }

    private void PushState(Entity<AmmoBenchComponent> ent)
    {
        var stock = new Dictionary<string, int>();
        foreach (var (type, count) in ent.Comp.Stock)
        {
            if (count > 0)
                stock[type] = count;
        }

        var tank = new Dictionary<string, double>();
        var capacity = 0.0;
        if (TryGetTank(ent, out var soln))
        {
            capacity = soln.Comp.Solution.MaxVolume.Float();
            foreach (var reagent in soln.Comp.Solution.Contents)
                tank[reagent.Reagent.Prototype] = tank.GetValueOrDefault(reagent.Reagent.Prototype) + reagent.Quantity.Float();
        }

        _ui.SetUiState(ent.Owner, AmmoBenchUiKey.Key, new AmmoBenchBuiState(stock, tank, capacity, ItemCaliber(ent)));
    }

    // ------------------------------------------------------------------ asking for a build

    private (bool Ok, string Lacking) HasEverything(Entity<AmmoBenchComponent> ent, AmmoDesign design, AmmoEvaluation e, int count)
    {
        var (materials, powder, liquid) = AmmoEvaluator.BatchCost(e, count);

        foreach (var (id, amount) in materials)
        {
            var type = _data.Material(id)!.StackType;
            var need = (int) Math.Ceiling(amount);
            if (ent.Comp.Stock.GetValueOrDefault(type) < need)
                return (false, $"{StackName(type)} ×{need}");
        }

        var powderNeed = (int) Math.Ceiling(powder);
        if (ent.Comp.Stock.GetValueOrDefault(ent.Comp.PowderStack) < powderNeed)
            return (false, $"{StackName(ent.Comp.PowderStack)} ×{powderNeed}");

        if (liquid > 0 && LiquidHeld(ent, design.Payload) + 0.001 < liquid)
            return (false, $"{design.Payload} ×{liquid:0.#}");

        return (true, string.Empty);
    }

    private string StackName(string type) =>
        _prototypes.TryIndex<StackPrototype>(type, out var stack) ? Loc.GetString(stack.Name) : type;

    private double LiquidHeld(Entity<AmmoBenchComponent> ent, string reagent)
    {
        if (!TryGetTank(ent, out var tank))
            return 0;

        var held = 0.0;
        foreach (var quantity in tank.Comp.Solution.Contents)
        {
            if (quantity.Reagent.Prototype == reagent)
                held += quantity.Quantity.Float();
        }

        return held;
    }

    private bool Accept(Entity<AmmoBenchComponent> ent, EntityUid user, DesignCheck<AmmoEvaluation> check, AmmoDesign design, int count)
    {
        if (!check.Valid)
        {
            _popup.PopupEntity(Loc.GetString("gunsmith-invalid", ("what", check.Errors[0])), ent, user, PopupType.SmallCaution);
            return false;
        }

        var (ok, lacking) = HasEverything(ent, design, check.Result, count);
        if (!ok)
        {
            _popup.PopupEntity(Loc.GetString("gunsmith-lacking", ("what", lacking)), ent, user, PopupType.SmallCaution);
            return false;
        }

        return true;
    }

    private void OnBuild(Entity<AmmoBenchComponent> ent, ref AmmoBuildMessage args)
    {
        if (!HasComp<SapperComponent>(args.Actor))
            return;

        var design = args.Design.Clone();
        var count = Math.Clamp(args.Count, 1, 20);
        var check = AmmoEvaluator.Evaluate(design, _data);
        if (!Accept(ent, args.Actor, check, design, count))
            return;

        var seconds = Math.Max(0.2, (4 + count * check.Result.BuildSeconds) * ent.Comp.TimeScale);
        var doAfter = new DoAfterArgs(EntityManager, args.Actor, TimeSpan.FromSeconds(seconds), new AmmoBuildDoAfterEvent(design, count), ent, ent)
        {
            BreakOnMove = true,
            BreakOnHandChange = false,
            NeedHand = false,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("gunsmith-working"), ent, args.Actor);
    }

    // ------------------------------------------------------------------ making the cartridges

    private void OnBuilt(Entity<AmmoBenchComponent> ent, ref AmmoBuildDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var design = args.Design.Clone();
        var count = Math.Clamp(args.Count, 1, 20);
        var check = AmmoEvaluator.Evaluate(design, _data);
        if (!Accept(ent, args.User, check, design, count))
            return;

        var e = check.Result;
        var (materials, powder, liquid) = AmmoEvaluator.BatchCost(e, count);
        foreach (var (id, amount) in materials)
        {
            var type = _data.Material(id)!.StackType;
            ent.Comp.Stock[type] = Math.Max(0, ent.Comp.Stock.GetValueOrDefault(type) - (int) Math.Ceiling(amount));
        }

        ent.Comp.Stock[ent.Comp.PowderStack] = Math.Max(0, ent.Comp.Stock.GetValueOrDefault(ent.Comp.PowderStack) - (int) Math.Ceiling(powder));

        if (liquid > 0 && TryGetTank(ent, out var tank))
            _solutions.RemoveReagent(tank, design.Payload, FixedPoint2.New((float) liquid));

        // The cartridge is a shell if the caliber is a shotgun's (a heavy one for a heavy caliber), and a pellet cartridge if it is shot in some other caliber.
        // What it is loaded with (one bullet or shot, and how much) is the bullet's own.
        var shot = e.Kind is AmmoBulletKind.Buckshot or AmmoBulletKind.HeavyBuckshot;
        var proto = e.ArmorLevel == 0 ? ent.Comp.CartridgePrototype : $"RUMCCraftedCartridgeAp{e.ArmorLevel}";
        if (e.Shell)
            proto = $"RUMCCrafted{(e.Heavy ? "Heavy" : string.Empty)}Shell{(shot ? e.Pellets : 1)}";
        else if (shot)
            proto = $"RUMCCraftedCartridgeP{e.Pellets}";

        if (!_prototypes.HasIndex<EntityPrototype>(proto))
            proto = ent.Comp.CartridgePrototype;

        var key = design.Caliber.Key();
        var coords = Transform(ent).Coordinates;
        var summary = Summary(design);
        var signature = Signature(design, e);

        // Cartridges made the same are one stack.
        var cartridge = Spawn(proto, coords);
        var crafted = EnsureComp<CraftedCartridgeComponent>(cartridge);
        crafted.CaliberKey = key;
        crafted.Load = e.Load.Clone();
        crafted.DamageMultiplier = e.DamageMultiplier;
        crafted.ArmorPierce = e.ArmorPierce;
        crafted.Seal = e.Seal;
        crafted.Jam = e.Jam;
        crafted.Extra = new Dictionary<string, double>(e.Extra);
        crafted.PayloadReagent = e.PayloadReagent;
        crafted.PayloadUnits = e.PayloadUnits;
        crafted.InjectUnits = e.InjectUnits;
        crafted.Ignite = e.Ignite;
        crafted.PayloadDamageType = e.PayloadDamageType;
        crafted.PayloadDamagePerUnit = e.PayloadDamagePerUnit;
        crafted.Pellets = e.Pellets;
        crafted.RatedDamage = e.RatedDamage;
        crafted.GameDamage = e.GameDamage;
        crafted.GameMaxDamage = e.GameMaxDamage;
        crafted.BaselineEnergyJ = e.BaselineEnergyJ;
        crafted.NominalPellets = e.NominalPellets;
        crafted.Signature = signature;
        crafted.Kind = e.Kind;
        Dirty(cartridge, crafted);
        _catalog.TagCartridge(cartridge, key);

        string nameKey;
        if (e.Shell)
            nameKey = (e.Heavy ? "ammo-shell-name-heavy-" : "ammo-shell-name-") + (shot ? "shot" : "slug");
        else if (e.Kind == AmmoBulletKind.Syringe)
            nameKey = "ammo-cartridge-name-syringe";
        else
            nameKey = shot ? "ammo-cartridge-name-shot" : "ammo-cartridge-name";

        _meta.SetEntityName(cartridge, Loc.GetString(nameKey, ("caliber", GunsmithBenchSystem.CaliberName(design.Caliber)), ("pellets", e.Pellets)));
        _meta.SetEntityDescription(cartridge, Loc.GetString("ammo-cartridge-description", ("key", key), ("parts", summary)));
        if (HasComp<StackComponent>(cartridge) && count > 1)
            _stacks.SetCount(cartridge, count);

        _popup.PopupEntity(Loc.GetString("gunsmith-built"), ent, args.User);
        PushState(ent);
    }

    /// <summary>What the cartridge is made of, as one string: cartridges stack with the ones that have the same.</summary>
    private static string Signature(AmmoDesign d, AmmoEvaluation e)
    {
        var plain = d.Kind == AmmoBulletKind.Bullet;
        var syringe = d.Kind == AmmoBulletKind.Syringe;
        return string.Join("|", d.Caliber.Key(), d.Kind, e.Pellets, syringe ? string.Empty : d.Core, plain || syringe ? d.Jacket : string.Empty, plain ? d.Tip : string.Empty,
            d.Casing, d.PowderLevel, d.Payload, syringe ? e.PayloadUnits : 0.0);
    }

    /// <summary>What a cartridge is made of, for its description: the materials of its parts and what is in it. Not what it comes to.</summary>
    private string Summary(AmmoDesign d)
    {
        string Material(string ammoMaterial) =>
            _data.AmmoMaterial(ammoMaterial) is { } m && _data.Material(m.Material) is { } g ? Loc.GetString(g.Name) : "?";

        string Liquid() => _reagent.TryIndex(d.Payload, out var proto) ? proto.LocalizedName : d.Payload;
        string Level() => d.PowderLevel > 0 ? $"+{d.PowderLevel}" : d.PowderLevel.ToString();

        if (d.Kind == AmmoBulletKind.Syringe)
        {
            // A syringe bullet: the metal of the body, the casing, the powder, the liquid and how much of it.
            return string.Join(", ", new[]
            {
                Loc.GetString("ammo-part-body", ("material", Material(d.Jacket))),
                Loc.GetString("ammo-part-casing", ("material", Material(d.Casing))),
                Loc.GetString("ammo-part-powder", ("level", Level())),
                Loc.GetString("ammo-part-syringe", ("reagent", Liquid()), ("units", d.PayloadUnits)),
            });
        }

        if (d.Kind != AmmoBulletKind.Bullet)
        {
            // Buckshot: the metal of the shot, the casing, how many pellets, the powder, a fuel.
            var shotParts = new List<string>
            {
                Loc.GetString("ammo-part-shot", ("material", Material(d.Core))),
                Loc.GetString("ammo-part-casing", ("material", Material(d.Casing))),
                Loc.GetString("ammo-part-pellets", ("count", d.Pellets)),
                Loc.GetString("ammo-part-powder", ("level", Level())),
            };

            if (!string.IsNullOrEmpty(d.Payload))
                shotParts.Add(Loc.GetString("ammo-part-payload", ("reagent", Liquid())));

            return string.Join(", ", shotParts);
        }

        var parts = new List<string>
        {
            Loc.GetString("ammo-part-core", ("material", Material(d.Core))),
            Loc.GetString("ammo-part-jacket", ("material", Material(d.Jacket))),
        };

        if (!string.IsNullOrEmpty(d.Tip))
            parts.Add(Loc.GetString("ammo-part-tip", ("material", Material(d.Tip))));

        parts.Add(Loc.GetString("ammo-part-casing", ("material", Material(d.Casing))));
        parts.Add(Loc.GetString("ammo-part-powder", ("level", d.PowderLevel > 0 ? $"+{d.PowderLevel}" : d.PowderLevel.ToString())));

        if (!string.IsNullOrEmpty(d.Payload))
            parts.Add(Loc.GetString("ammo-part-payload", ("reagent", Liquid())));

        return string.Join(", ", parts);
    }
}
