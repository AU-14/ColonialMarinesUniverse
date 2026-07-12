using System.Collections.Generic;
using System.Linq;
using Content.Shared._AU14.Insurgency;
using Content.Shared._AU14.Insurgency.CellKit;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._AU14.Insurgency.CellKit;

/// <summary>
///     Drives the Heavy Cell Kit UI. On first open it snapshots the active faction's manifest into the
///     kit's deployable list; the player picks an entry, a short do-after runs, and then the entry is
///     spawned where they stand and consumed from the kit. Vendors are configured after they spawn.
///
///     Event-driven: reacts to the UI open, the deploy message, and the deploy do-after. No polling.
/// </summary>
public sealed class InsurgencyCellKitSystem : EntitySystem
{
    [Dependency] private InsurgencyFactionApplySystem _apply = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<InsurgencyCellKitComponent>(InsurgencyCellKitUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<InsurgencyCellKitDeployMessage>(OnDeploy);
        });

        SubscribeLocalEvent<InsurgencyCellKitComponent, InsurgencyCellKitDeployDoAfterEvent>(OnDeployDoAfter);
        SubscribeLocalEvent<InsurgencyCellKitComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
    }

    // Hard block: the kit is inert until a faction is applied. The leader picks a faction after spawn,
    // so the kit they spawn with would otherwise open blank. Cancel the open and tell them why.
    private void OnOpenAttempt(Entity<InsurgencyCellKitComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (_apply.GetActiveFaction() == null)
        {
            _popup.PopupEntity(Loc.GetString("insfor-cell-kit-no-faction"), ent, args.User, PopupType.SmallCaution);
            args.Cancel();
        }
    }

    private void OnOpened(Entity<InsurgencyCellKitComponent> ent, ref BoundUIOpenedEvent args)
    {
        EnsureInitialized(ent);
        PushState(ent);
    }

    // Snapshots the active faction's manifest into the kit the first time it is opened. Placeables
    // come first, then vendors, matching the old deploy order.
    private void EnsureInitialized(Entity<InsurgencyCellKitComponent> ent)
    {
        if (ent.Comp.Initialized)
            return;

        ent.Comp.Initialized = true;

        var faction = _apply.GetActiveFaction();
        if (faction == null)
            return;

        foreach (var placeable in faction.CellKit.PlaceableEntities)
            ent.Comp.Remaining.Add(new CellKitDeployable { Proto = placeable.Id, IsVendor = false });

        for (var i = 0; i < faction.CellKit.VendorDefinitions.Count; i++)
        {
            var vendor = faction.CellKit.VendorDefinitions[i];
            ent.Comp.Remaining.Add(new CellKitDeployable
            {
                Proto = vendor.BaseModel.Id,
                DisplayName = vendor.Name,
                IsVendor = true,
                VendorIndex = i,
            });
        }
    }

    private void OnDeploy(Entity<InsurgencyCellKitComponent> ent, ref InsurgencyCellKitDeployMessage args)
    {
        if (_apply.GetActiveFaction() == null)
        {
            _popup.PopupEntity(Loc.GetString("insfor-cell-kit-no-faction"), ent, args.Actor, PopupType.SmallCaution);
            return;
        }

        if (args.Index < 0 || args.Index >= ent.Comp.Remaining.Count)
            return;

        var doAfter = new DoAfterArgs(
            EntityManager,
            args.Actor,
            System.TimeSpan.FromSeconds(ent.Comp.DeployTime),
            new InsurgencyCellKitDeployDoAfterEvent(args.Index),
            ent.Owner,
            used: ent.Owner)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnDeployDoAfter(Entity<InsurgencyCellKitComponent> ent, ref InsurgencyCellKitDeployDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        // Re-check the index: the list may have shrunk while the do-after was running.
        if (args.Index < 0 || args.Index >= ent.Comp.Remaining.Count)
            return;

        args.Handled = true;

        var user = args.Args.User;
        var deployable = ent.Comp.Remaining[args.Index];
        var coords = Transform(user).Coordinates;

        var spawned = Spawn(deployable.Proto, coords);
        if (deployable.IsVendor)
            _apply.ConfigureFactionVendor(spawned, GetVendorDefinition(deployable.VendorIndex), deployable.VendorIndex);
        else
            // Machines like the analyzer pick up the faction's submittable-for-points table when placed.
            _apply.ConfigureFactionAnalyzer(spawned);

        ent.Comp.Remaining.RemoveAt(args.Index);

        var remaining = ent.Comp.Remaining.Count;
        _popup.PopupEntity(Loc.GetString("insfor-cell-kit-deployed", ("remaining", remaining)), ent, user, PopupType.Small);

        PushState(ent);
    }

    private FactionVendorDefinition GetVendorDefinition(int index)
    {
        var faction = _apply.GetActiveFaction();
        return faction!.CellKit.VendorDefinitions[index];
    }

    private void PushState(Entity<InsurgencyCellKitComponent> ent)
    {
        var entries = ent.Comp.Remaining.Select(d => d.Proto).ToList();
        var names = ent.Comp.Remaining.Select(d => d.DisplayName).ToList();
        _ui.SetUiState(ent.Owner, InsurgencyCellKitUiKey.Key, new InsurgencyCellKitBuiState(entries, names, ent.Comp.DeployTime));
    }
}
