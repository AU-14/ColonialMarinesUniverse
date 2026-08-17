using Content.Shared._RMC14.Barricade.Components;
using Content.Shared._RMC14.Construction;
using Content.Shared._RMC14.Emplacements;
using Content.Shared.Construction.Components;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Timing;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using static Content.Shared.Physics.CollisionGroup;

namespace Content.Shared._RMC14.Entrenching;

public sealed partial class BarricadeSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private RMCConstructionSystem _rmcConstruction = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;
    [Dependency] private SharedWeaponMountSystem _weaponMount = default!;

    private EntityQuery<BarricadeComponent> _barricadeQuery;

    public override void Initialize()
    {
        _barricadeQuery = GetEntityQuery<BarricadeComponent>();

        SubscribeLocalEvent<EntrenchingToolComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<EntrenchingToolComponent, EntrenchingToolDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<EntrenchingToolComponent, ItemToggledEvent>(OnItemToggled);
        SubscribeLocalEvent<EntrenchingToolComponent, SandbagFillDoAfterEvent>(OnSandbagFillDoAfter);
        SubscribeLocalEvent<EntrenchingToolComponent, SandbagDismantleDoAfterEvent>(OnSandbagDismantleDoAfter);
        SubscribeLocalEvent<EntrenchingToolComponent, DirtMoundBuildDoAfterEvent>(OnDirtMoundBuildDoAfter);
        SubscribeLocalEvent<EntrenchingToolComponent, DirtMoundDismantleDoAfterEvent>(OnDirtMoundDismantleDoAfter);
        SubscribeLocalEvent<EntrenchingToolComponent, HescoFillDoAfterEvent>(OnHescoFillDoAfter);
        SubscribeLocalEvent<EntrenchingToolComponent, HescoRaiseDoAfterEvent>(OnHescoRaiseDoAfter);
        SubscribeLocalEvent<EntrenchingToolComponent, HescoDisassembleDoAfterEvent>(OnHescoDisassembleDoAfter);

        SubscribeLocalEvent<HescoDisassemblableComponent, GetVerbsEvent<AlternativeVerb>>(OnHescoGetDisassembleVerbs);

        SubscribeLocalEvent<EmptySandbagComponent, InteractUsingEvent>(OnEmptyInteractUsing);

        SubscribeLocalEvent<FullSandbagComponent, ActivateInWorldEvent>(OnFullActivateInWorld);
        SubscribeLocalEvent<FullSandbagComponent, AfterInteractEvent>(OnFullAfterInteract);
        SubscribeLocalEvent<FullSandbagComponent, SandbagBuildDoAfterEvent>(OnFullBuildDoAfter);

        SubscribeLocalEvent<HescoKitComponent, ActivateInWorldEvent>(OnHescoKitActivateInWorld);
        SubscribeLocalEvent<HescoKitComponent, AfterInteractEvent>(OnHescoKitAfterInteract);
        SubscribeLocalEvent<HescoKitComponent, HescoBuildDoAfterEvent>(OnHescoBuildDoAfter);

        SubscribeLocalEvent<BarricadeComponent, AnchorAttemptEvent>(OnAnchorAttempt);
    }

    private void OnAfterInteract(Entity<EntrenchingToolComponent> tool, ref AfterInteractEvent args)
    {
        if (!args.CanReach)
            return;

        if (HasComp<BarricadeSandbagComponent>(args.Target))
        {
            DismantleSandbagBaricade(tool, ref args);
            args.Handled = true;
            return;
        }

        if (HasComp<DirtMoundComponent>(args.Target))
        {
            DismantleDirtMound(tool, ref args);
            args.Handled = true;
            return;
        }

        if (args.Target is { } disassembleTarget &&
            TryComp(disassembleTarget, out HescoDisassemblableComponent? disassemblable) &&
            disassemblable.Disassembling)
        {
            StartHescoDisassemble(tool, args.User, disassembleTarget, disassemblable);
            args.Handled = true;
            return;
        }

        if (args.Target is { } fillTarget && TryComp(fillTarget, out HescoFillableComponent? fillable))
        {
            StartHescoFill(tool, args.User, fillTarget, fillable);
            args.Handled = true;
            return;
        }

        if (args.Target is { } raiseTarget && TryComp(raiseTarget, out HescoRaisableComponent? raisable))
        {
            StartHescoRaise(tool, args.User, raiseTarget, raisable);
            args.Handled = true;
            return;
        }

        if (args.Target == null &&
            tool.Comp.TotalLayers >= tool.Comp.MoundCost &&
            TryComp(args.User, out TransformComponent? userTransform) &&
            StartBuildingDirtMound(tool, args.User, args.ClickLocation, userTransform.LocalRotation.GetCardinalDir()))
        {
            args.Handled = true;
            return;
        }

        StartDigging(tool, args.User, args.ClickLocation);
        args.Handled = true;
    }

    private void DismantleSandbagBaricade(Entity<EntrenchingToolComponent> tool, ref AfterInteractEvent args)
    {
        if (TryComp(tool, out ItemToggleComponent? toggle) && !toggle.Activated)
            return;

        _popup.PopupClient(Loc.GetString("cm-entrenching-dismantle"), args.User, args.User);

        var ev = new SandbagDismantleDoAfterEvent(GetNetCoordinates(args.ClickLocation));
        var doAfter = new DoAfterArgs(EntityManager, args.User, GetToolDelay(tool, tool.Comp.DigDelay), ev, tool, args.Target, tool)
        {
            BreakOnMove = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void DismantleDirtMound(Entity<EntrenchingToolComponent> tool, ref AfterInteractEvent args)
    {
        if (TryComp(tool, out ItemToggleComponent? toggle) && !toggle.Activated)
            return;

        _popup.PopupClient(Loc.GetString("cm-entrenching-dismantle-mound"), args.User, args.User);

        var ev = new DirtMoundDismantleDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, args.User, GetToolDelay(tool, tool.Comp.DigDelay), ev, tool, args.Target, tool)
        {
            BreakOnMove = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnDirtMoundDismantleDoAfter(Entity<EntrenchingToolComponent> tool, ref DirtMoundDismantleDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (_net.IsClient)
            return;

        if (!HasComp<DirtMoundComponent>(args.Target))
            return;

        Del(args.Target);
    }

    private void OnSandbagDismantleDoAfter(Entity<EntrenchingToolComponent> tool, ref SandbagDismantleDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (_net.IsClient)
            return;

        if (!TryComp(args.Target, out BarricadeSandbagComponent? barricade))
            return;
        var full = Spawn(barricade.Material, GetCoordinates(args.Coordinates));

        var bagsSalvaged = barricade.MaxMaterial;
        if (bagsSalvaged <= 0 && TryComp(full, out FullSandbagComponent? fullSandbag))
            bagsSalvaged = fullSandbag.StackRequired;
        if (TryComp(args.Target, out DamageableComponent? damageable))
            bagsSalvaged -= Math.Max((int) damageable.TotalDamage / barricade.MaterialLossDamageInterval - 1, 0);

        if (TryComp(args.Target, out BarbedComponent? barbed) && barbed.IsBarbed)
            Spawn(barbed.Spawn, GetCoordinates(args.Coordinates));

        Del(args.Target);

        if (bagsSalvaged <= 0)
        {
            Del(full);
            return;
        }

        if (TryComp(full, out StackComponent? fullStack))
            _stack.SetCount(full, bagsSalvaged, fullStack);
    }

    private void OnDoAfter(Entity<EntrenchingToolComponent> tool, ref EntrenchingToolDoAfterEvent args)
    {
        if (args.Handled)
            return;

        if (args.Cancelled)
        {
            _popup.PopupClient(Loc.GetString("cm-entrenching-stop-digging"), args.User, args.User);
            return;
        }

        var coordinates = GetCoordinates(args.Coordinates);
        if (!CanDig(tool, args.User, coordinates, false, out _, out _))
            return;

        args.Handled = true;
        tool.Comp.TotalLayers = tool.Comp.LayersPerDig;
        Dirty(tool);

        var userCoordinates = _transform.GetMoverCoordinates(args.User);
        var emptyNearby = _lookup.GetEntitiesInRange<EmptySandbagComponent>(userCoordinates, 1.5f);
        foreach (var empty in emptyNearby)
        {
            var ev = new SandbagFillDoAfterEvent();
            var doAfter = new DoAfterArgs(EntityManager, args.User, GetToolDelay(tool, tool.Comp.FillDelay), ev, tool, empty, tool)
            {
                BreakOnMove = true,
            };
            _doAfter.TryStartDoAfter(doAfter);
            _popup.PopupClient(Loc.GetString("cm-entrenching-begin-filling"), args.User, args.User);
            break;
        }
    }

    private void OnItemToggled(Entity<EntrenchingToolComponent> tool, ref ItemToggledEvent args)
    {
        tool.Comp.TotalLayers = 0;
        Dirty(tool);
    }

    private void OnSandbagFillDoAfter(Entity<EntrenchingToolComponent> tool, ref SandbagFillDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var userCoordinates = _transform.GetMoverCoordinates(args.User);
        var emptyNearby = _lookup.GetEntitiesInRange<EmptySandbagComponent>(userCoordinates, 1.5f);
        var filled = false;
        foreach (var empty in emptyNearby)
        {
            if (filled)
            {
                args.Repeat = true;
                break;
            }

            filled = true;
            Fill(tool, empty, args.User, tool.Comp.TotalLayers);
            if (!TerminatingOrDeleted(empty))
            {
                args.Repeat = true;
                break;
            }
        }

        if (tool.Comp.TotalLayers <= 0)
        {
            args.Repeat = false;
            StartDigging(tool, args.User, tool.Comp.LastDigLocation);
        }
    }

    private void OnDirtMoundBuildDoAfter(Entity<EntrenchingToolComponent> tool, ref DirtMoundBuildDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var coordinates = GetCoordinates(args.Coordinates);
        if (!CanBuildDirtMound(tool, args.User, coordinates, args.Direction, out var grid, out var tile))
            return;

        tool.Comp.TotalLayers -= tool.Comp.MoundCost;
        Dirty(tool);
        _audio.PlayPredicted(tool.Comp.FillSound, args.User, args.User);

        if (_net.IsServer)
        {
            var buildCoordinates = _mapSystem.GridTileToLocal(grid, grid, tile.GridIndices);
            var built = SpawnAtPosition(tool.Comp.MoundPrototype, buildCoordinates);
            _transform.SetLocalRotation(built, args.Direction.ToAngle());
        }
    }

    private void OnHescoFillDoAfter(Entity<EntrenchingToolComponent> tool, ref HescoFillDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (args.Target is not { } target || !TryComp(target, out HescoFillableComponent? fillable))
            return;

        if (TryComp(tool, out ItemToggleComponent? toggle) && !toggle.Activated)
            return;

        if (!_interaction.InRangeUnobstructed(args.User, target, popup: false))
            return;

        fillable.Progress += fillable.ProgressPerTick;
        Dirty(target, fillable);
        _audio.PlayPredicted(tool.Comp.FillSound, args.User, args.User);

        if (fillable.Progress < fillable.Required)
        {
            args.Repeat = true;
            return;
        }

        // Removed synchronously (unlike the QueueDel in AdvanceHescoStage) so that other players' fill ticks
        // completing in this same tick see no HescoFillableComponent and bail out instead of double-advancing.
        RemComp<HescoFillableComponent>(target);

        if (_net.IsServer)
            AdvanceHescoStage(target, fillable.NextStage);
    }

    private void OnHescoRaiseDoAfter(Entity<EntrenchingToolComponent> tool, ref HescoRaiseDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (args.Target is not { } target || !TryComp(target, out HescoRaisableComponent? raisable))
            return;

        RemComp<HescoRaisableComponent>(target);
        _audio.PlayPredicted(tool.Comp.DigSound, args.User, args.User);

        if (_net.IsServer)
            AdvanceHescoStage(target, raisable.NextStage);
    }

    private void OnHescoGetDisassembleVerbs(Entity<HescoDisassemblableComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (ent.Comp.Disassembling || !args.CanInteract || !args.CanAccess)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = "Disassemble",
            Act = () =>
            {
                if (ent.Comp.Progress <= 0)
                    ent.Comp.Progress = ent.Comp.Required;

                ent.Comp.Disassembling = true;
                Dirty(ent);
                _popup.PopupClient(Loc.GetString("cm-entrenching-hesco-armed-disassemble"), user, user);
            },
        });
    }

    private void OnHescoDisassembleDoAfter(Entity<EntrenchingToolComponent> tool, ref HescoDisassembleDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (args.Target is not { } target ||
            !TryComp(target, out HescoDisassemblableComponent? disassemblable) ||
            !disassemblable.Disassembling)
        {
            return;
        }

        if (TryComp(tool, out ItemToggleComponent? toggle) && !toggle.Activated)
            return;

        if (!_interaction.InRangeUnobstructed(args.User, target, popup: false))
            return;

        _audio.PlayPredicted(tool.Comp.FillSound, args.User, args.User);

        if (!disassemblable.Instant)
        {
            disassemblable.Progress -= disassemblable.ProgressPerTick;
            Dirty(target, disassemblable);

            if (disassemblable.Progress > 0)
            {
                args.Repeat = true;
                return;
            }
        }

        // Removed synchronously (unlike the QueueDel below) so that other players' ticks completing this same
        // tick see no HescoDisassemblableComponent and bail out instead of double-advancing.
        RemComp<HescoDisassemblableComponent>(target);

        if (!_net.IsServer)
            return;

        if (disassemblable.PreviousStage is { } previous)
        {
            AdvanceHescoStage(target, previous);
            return;
        }

        var xform = Transform(target);
        if (disassemblable.ReturnPrototype is { } returnPrototype)
            SpawnAtPosition(returnPrototype, xform.Coordinates);

        QueueDel(target);
    }

    private void AdvanceHescoStage(EntityUid current, EntProtoId nextStage)
    {
        var xform = Transform(current);
        var next = SpawnAtPosition(nextStage, xform.Coordinates);
        _transform.SetLocalRotation(next, xform.LocalRotation);

        QueueDel(current);
    }

    private void OnEmptyInteractUsing(Entity<EmptySandbagComponent> empty, ref InteractUsingEvent args)
    {
        if (_net.IsClient || args.Handled)
            return;

        if (!TryComp(args.Used, out EntrenchingToolComponent? toolComp))
            return;

        args.Handled = true;

        var tool = new Entity<EntrenchingToolComponent>(args.Used, toolComp);
        var ev = new SandbagFillDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, args.User, GetToolDelay(tool, tool.Comp.FillDelay), ev, tool, empty, tool)
        {
            BreakOnMove = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
        _popup.PopupClient(Loc.GetString("cm-entrenching-begin-filling"), args.User, args.User);
    }

    private void OnFullActivateInWorld(Entity<FullSandbagComponent> full, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !TryComp(args.User, out TransformComponent? transform))
            return;

        var coordinates = _transform.GetMoverCoordinates(args.User, transform);
        var direction = transform.LocalRotation.GetCardinalDir();
        if (Build(full, args.User, coordinates, direction, out var handled))
            args.Handled = handled;
    }

    private void OnFullAfterInteract(Entity<FullSandbagComponent> full, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || !TryComp(args.User, out TransformComponent? transform))
            return;

        var direction = transform.LocalRotation.GetCardinalDir();
        if (Build(full, args.User, args.ClickLocation, direction, out var handled))
            args.Handled = handled;
    }

    private void OnFullBuildDoAfter(Entity<FullSandbagComponent> full, ref SandbagBuildDoAfterEvent args)
    {
        if (_net.IsClient)
            return;

        if (args.Cancelled || args.Handled)
            return;

        var coordinates = GetCoordinates(args.Coordinates);
        if (!_mapSystem.TryFindGridAt(_transform.ToMapCoordinates(coordinates), out var gridId, out var gridComp) ||
            !_interaction.InRangeUnobstructed(full, coordinates, popup: false) ||
            !_turf.TryGetTileRef(coordinates, out var turf) ||
            !CanBuild(full, (gridId, gridComp), args.User, turf.Value, args.Direction))
        {
            return;
        }

        if (full.Comp.StackRequired > 1)
        {
            var count = _stack.GetCount(full);
            if (count < full.Comp.StackRequired)
                return;

            if (TryComp(full, out StackComponent? fullStack))
                _stack.SetCount(full, count - full.Comp.StackRequired, fullStack);
            else
                QueueDel(full);
        }

        var built = SpawnAtPosition(full.Comp.Builds, coordinates);
        _transform.SetLocalRotation(built, args.Direction.ToAngle());

        args.Handled = true;
    }

    private void OnHescoKitActivateInWorld(Entity<HescoKitComponent> kit, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !TryComp(args.User, out TransformComponent? transform))
            return;

        var coordinates = _transform.GetMoverCoordinates(args.User, transform);
        var direction = transform.LocalRotation.GetCardinalDir();
        if (BuildHescoKit(kit, args.User, coordinates, direction, out var handled))
            args.Handled = handled;
    }

    private void OnHescoKitAfterInteract(Entity<HescoKitComponent> kit, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || !TryComp(args.User, out TransformComponent? transform))
            return;

        var direction = transform.LocalRotation.GetCardinalDir();
        if (BuildHescoKit(kit, args.User, args.ClickLocation, direction, out var handled))
            args.Handled = handled;
    }

    private void OnHescoBuildDoAfter(Entity<HescoKitComponent> kit, ref HescoBuildDoAfterEvent args)
    {
        if (_net.IsClient)
            return;

        if (args.Cancelled || args.Handled)
            return;

        var coordinates = GetCoordinates(args.Coordinates);
        if (!_mapSystem.TryFindGridAt(_transform.ToMapCoordinates(coordinates), out var gridId, out var gridComp) ||
            !_interaction.InRangeUnobstructed(kit, coordinates, popup: false) ||
            !_turf.TryGetTileRef(coordinates, out var turf) ||
            !CanBuildHescoKit(kit, (gridId, gridComp), args.User, turf.Value, args.Direction))
        {
            return;
        }

        var built = SpawnAtPosition(kit.Comp.Builds, coordinates);
        _transform.SetLocalRotation(built, args.Direction.ToAngle());

        QueueDel(kit);
        args.Handled = true;
    }

    private void OnAnchorAttempt(Entity<BarricadeComponent> ent, ref AnchorAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var coordinates = ent.Owner.ToCoordinates();
        if (_transform.GetGrid(coordinates) is not { } gridId)
            return;

        if (!TryComp(gridId, out MapGridComponent? grid))
            return;

        if (_weaponMount.HasWeaponMountNearbyPopup((gridId, grid), coordinates, ent,  user: args.User))
        {
            args.Cancel();
        }
    }

    private bool StartDigging(Entity<EntrenchingToolComponent> tool, EntityUid user, EntityCoordinates clicked)
    {
        if (!CanDig(tool, user, clicked, true, out var grid, out var tile))
            return false;

        var coordinates = _mapSystem.GridTileToLocal(grid, grid, tile.GridIndices);
        tool.Comp.LastDigLocation = coordinates;
        Dirty(tool);

        var ev = new EntrenchingToolDoAfterEvent(GetNetCoordinates(coordinates));
        var doAfter = new DoAfterArgs(EntityManager, user, GetToolDelay(tool, tool.Comp.DigDelay), ev, tool, used: tool)
        {
            BreakOnMove = true,
            NeedHand = true,
            BreakOnHandChange = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
        _popup.PopupClient(Loc.GetString("cm-entrenching-start-digging"), user, user);
        _audio.PlayPredicted(tool.Comp.DigSound, user, user);

        if (TryComp(tool, out UseDelayComponent? useDelay))
            _useDelay.TryResetDelay((tool, useDelay));

        return true;
    }

    private bool StartBuildingDirtMound(Entity<EntrenchingToolComponent> tool, EntityUid user, EntityCoordinates clicked, Direction direction)
    {
        if (!CanBuildDirtMound(tool, user, clicked, direction, out var grid, out var tile))
            return false;

        var coordinates = _mapSystem.GridTileToLocal(grid, grid, tile.GridIndices);
        var ev = new DirtMoundBuildDoAfterEvent(GetNetCoordinates(coordinates), direction);
        var doAfter = new DoAfterArgs(EntityManager, user, GetToolDelay(tool, tool.Comp.MoundBuildDelay), ev, tool, used: tool)
        {
            BreakOnMove = true,
            NeedHand = true,
            BreakOnHandChange = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
        _popup.PopupClient(Loc.GetString("cm-entrenching-start-mound"), user, user);
        _audio.PlayPredicted(tool.Comp.DigSound, user, user);

        return true;
    }

    private bool CanBuildDirtMound(
        Entity<EntrenchingToolComponent> tool,
        EntityUid user,
        EntityCoordinates coordinates,
        Direction direction,
        out Entity<MapGridComponent> grid,
        out TileRef tileRef)
    {
        grid = default;
        tileRef = default;

        if (tool.Comp.TotalLayers < tool.Comp.MoundCost)
            return false;

        if (TryComp(tool, out ItemToggleComponent? toggle) && !toggle.Activated)
            return false;

        if (!_interaction.InRangeUnobstructed(user, coordinates, popup: false))
            return false;

        if (!_mapSystem.TryFindGridAt(_transform.ToMapCoordinates(coordinates), out var gridId, out var gridComp))
            return false;

        tileRef = _mapSystem.GetTileRef(gridId, gridComp, coordinates);
        if (!TileSolidAndNotBlocked(tileRef))
            return false;

        grid = (gridId, gridComp);

        var buildCoordinates = _mapSystem.GridTileToLocal(grid, grid, tileRef.GridIndices).Offset(grid.Comp.TileSizeHalfVector);
        if (!_rmcConstruction.CanBuildAt(buildCoordinates, tool.Comp.MoundPrototype, out var popupStr, direction: direction, user: user))
        {
            if (_net.IsClient)
                _popup.PopupClient(popupStr, user, user, PopupType.SmallCaution);

            return false;
        }

        return true;
    }

    private void StartHescoFill(Entity<EntrenchingToolComponent> tool, EntityUid user, EntityUid target, HescoFillableComponent fillable)
    {
        if (TryComp(tool, out ItemToggleComponent? toggle) && !toggle.Activated)
            return;

        if (!_interaction.InRangeUnobstructed(user, target, popup: false))
            return;

        var ev = new HescoFillDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, user, GetToolDelay(tool, fillable.TickDelay), ev, tool, target, tool)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupClient(Loc.GetString("cm-entrenching-begin-hesco-fill"), user, user);
    }

    private void StartHescoRaise(Entity<EntrenchingToolComponent> tool, EntityUid user, EntityUid target, HescoRaisableComponent raisable)
    {
        if (TryComp(tool, out ItemToggleComponent? toggle) && !toggle.Activated)
            return;

        if (!_interaction.InRangeUnobstructed(user, target, popup: false))
            return;

        var ev = new HescoRaiseDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, user, GetToolDelay(tool, raisable.RaiseDelay), ev, tool, target, tool)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupClient(Loc.GetString("cm-entrenching-begin-hesco-raise"), user, user);
    }

    private void StartHescoDisassemble(Entity<EntrenchingToolComponent> tool, EntityUid user, EntityUid target, HescoDisassemblableComponent disassemblable)
    {
        if (TryComp(tool, out ItemToggleComponent? toggle) && !toggle.Activated)
            return;

        if (!_interaction.InRangeUnobstructed(user, target, popup: false))
            return;

        var ev = new HescoDisassembleDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, user, GetToolDelay(tool, disassemblable.TickDelay), ev, tool, target, tool)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupClient(Loc.GetString("cm-entrenching-begin-hesco-disassemble"), user, user);
    }

    private bool BuildHescoKit(Entity<HescoKitComponent> kit, EntityUid user, EntityCoordinates coordinates, Direction direction, out bool handled)
    {
        handled = false;
        if (!_mapSystem.TryFindGridAt(_transform.ToMapCoordinates(coordinates), out var gridId, out var gridComp) ||
            !_turf.TryGetTileRef(coordinates, out var tile))
        {
            return false;
        }

        handled = true;
        if (!CanBuildHescoKit(kit, (gridId, gridComp), user, tile.Value, direction))
            return false;

        var ev = new HescoBuildDoAfterEvent(GetNetCoordinates(coordinates), direction);
        var doAfter = new DoAfterArgs(EntityManager, user, kit.Comp.BuildDelay, ev, kit, used: kit)
        {
            BreakOnMove = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
        return true;
    }

    private bool CanBuildHescoKit(
        Entity<HescoKitComponent> kit,
        Entity<MapGridComponent> grid,
        EntityUid user,
        TileRef tile,
        Direction direction)
    {
        var coordinates = new EntityCoordinates(tile.GridUid, tile.X, tile.Y).Offset(grid.Comp.TileSizeHalfVector);
        var popup = _net.IsClient;
        if (!_interaction.InRangeUnobstructed(user, coordinates, popup: popup))
            return false;

        if (!TileSolidAndNotBlocked(tile))
            return false;

        if (!_rmcConstruction.CanBuildAt(coordinates, kit.Comp.Builds, out var popupStr, direction: direction, user: user))
        {
            if (popup)
                _popup.PopupClient(popupStr, user, user, PopupType.SmallCaution);

            return false;
        }

        return true;
    }

    private bool Fill(Entity<EntrenchingToolComponent> tool, Entity<EmptySandbagComponent> empty, EntityUid user, int amount)
    {
        if (tool.Comp.TotalLayers < amount)
            return false;

        var toRemove = amount;
        var coordinates = _transform.GetMoverCoordinates(empty);

        if (TryComp(empty, out StackComponent? stack))
        {
            var stackCount = _stack.GetCount(empty, stack);
            toRemove = Math.Min(toRemove, stackCount);
            _stack.SetCount(empty, stackCount - toRemove, stack);

            if (_net.IsServer)
            {
                var filled = Spawn(empty.Comp.Filled, coordinates);
                var filledStack = EnsureComp<StackComponent>(filled);
                _stack.SetCount(filled, toRemove, filledStack);
            }
        }
        else
        {
            if (_net.IsServer)
                Del(empty);
        }

        tool.Comp.TotalLayers -= toRemove;
        Dirty(tool);
        _audio.PlayPredicted(tool.Comp.FillSound, user, user);

        return true;
    }

    private bool Build(Entity<FullSandbagComponent> full, EntityUid user, EntityCoordinates coordinates, Direction direction, out bool handled)
    {
        handled = false;
        if (!_mapSystem.TryFindGridAt(_transform.ToMapCoordinates(coordinates), out var gridId, out var gridComp) ||
            !_turf.TryGetTileRef(coordinates, out var tile))
        {
            return false;
        }

        handled = true;
        if (!CanBuild(full, (gridId, gridComp), user, tile.Value, direction))
            return false;

        var ev = new SandbagBuildDoAfterEvent(GetNetCoordinates(coordinates), direction);
        var doAfter = new DoAfterArgs(EntityManager, user, full.Comp.BuildDelay, ev, full, full)
        {
            BreakOnMove = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
        return true;
    }

    private bool CanDig(
        Entity<EntrenchingToolComponent> tool,
        EntityUid user,
        EntityCoordinates coordinates,
        bool checkUseDelay,
        out Entity<MapGridComponent> grid,
        out TileRef tileRef)
    {
        grid = default;
        tileRef = default;

        if (checkUseDelay &&
            TryComp(tool, out UseDelayComponent? useDelay) &&
            _useDelay.IsDelayed((tool, useDelay)))
        {
            return false;
        }

        if (!_interaction.InRangeUnobstructed(user, coordinates, popup: false))
            return false;

        if (TryComp(tool, out ItemToggleComponent? toggle) && !toggle.Activated)
            return false;

        if (!_mapSystem.TryFindGridAt(_transform.ToMapCoordinates(coordinates), out var gridId, out var gridComp))
            return false;

        tileRef = _mapSystem.GetTileRef(gridId, gridComp, coordinates);
        var tileDef = (ContentTileDefinition) _tiles[tileRef.Tile.TypeId];
        if (!tileDef.CanDig)
            return false;

        if (!TileSolidAndNotBlocked(tileRef))
            return false;

        grid = (gridId, gridComp);
        return true;
    }

    private TimeSpan GetToolDelay(Entity<EntrenchingToolComponent> tool, TimeSpan baseDelay)
    {
        if (TryComp(tool, out RMCShovelComponent? shovel) && shovel.SpeedMultiplier > 0)
            return baseDelay / shovel.SpeedMultiplier;

        return baseDelay;
    }

    private bool TileSolidAndNotBlocked(TileRef tile)
    {
        return !_turf.IsSpace(tile) &&
               _turf.GetContentTileDefinition(tile).Sturdy &&
               !_turf.IsTileBlocked(tile, Impassable);
    }

    private bool CanBuild(
        Entity<FullSandbagComponent> full,
        Entity<MapGridComponent> grid,
        EntityUid user,
        TileRef tile,
        Direction direction)
    {
        if (!TryComp(full, out StackComponent? stack) ||
            stack.Count < 5)
        {
            return false;
        }

        var coordinates = new EntityCoordinates(tile.GridUid, tile.X, tile.Y).Offset(grid.Comp.TileSizeHalfVector);
        var mask = Impassable | InteractImpassable | TableLayer;
        var popup = _net.IsClient;
        if (!_interaction.InRangeUnobstructed(user, coordinates, collisionMask: mask, popup: popup))
            return false;

        if (!TileSolidAndNotBlocked(tile))
            return false;

        var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(grid, grid, tile.GridIndices);
        while (anchored.MoveNext(out var uid))
        {
            if (HasComp<BarricadeComponent>(uid) &&
                TryComp(uid, out TransformComponent? transform) &&
                transform.LocalRotation.GetCardinalDir() == direction)
            {
                return false;
            }
        }

        if (!_rmcConstruction.CanBuildAt(coordinates, full.Comp.Builds, out var popupStr, user: user))
        {
            if (popup)
                _popup.PopupClient(popupStr, user, user, PopupType.SmallCaution);

            return false;
        }

        return true;
    }

    public bool HasBarricadeFacing(EntityCoordinates coordinates, Direction direction)
    {
        if (_transform.GetGrid(coordinates) is not { } gridId ||
            !TryComp(gridId, out MapGridComponent? grid))
        {
            return false;
        }

        var indices = _mapSystem.TileIndicesFor(gridId, grid, coordinates);
        var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridId, grid, indices);
        while (anchored.MoveNext(out var uid))
        {
            if (_barricadeQuery.HasComp(uid))
            {
                var barricadeDir = _transform.GetWorldRotation(uid.Value).GetCardinalDir();
                if (barricadeDir == direction)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Check if a barricade is anchored near the given coordinates.
    /// </summary>
    /// <param name="grid">The map grid being checked.</param>
    /// <param name="user">The entity performing the search</param>
    /// <param name="coordinates">The coordinates used as the center</param>
    /// <param name="range">The radius of the search area</param>
    /// <returns>True if an anchored entity with a <see cref="BarricadeComponent"/> within the specified range</returns>
    public bool HasBarricadeNearbyPopup(Entity<MapGridComponent> grid, EntityUid user, EntityCoordinates coordinates, int range)
    {
        var position = _mapSystem.LocalToTile(grid, grid, coordinates);
        var checkArea = new Box2(position.X - range + 1, position.Y - range + 1, position.X + range, position.Y + range);
        var enumerable = _mapSystem.GetLocalAnchoredEntities(grid, grid, checkArea);

        foreach (var anchored in enumerable)
        {
            if (HasComp<BarricadeComponent>(anchored))
            {
                var msg = Loc.GetString("barricade-anchored-too-close", ("barricade", anchored));
                _popup.PopupClient(msg, user, user, PopupType.SmallCaution );
                return true;
            }
        }
        return false;
    }
}
