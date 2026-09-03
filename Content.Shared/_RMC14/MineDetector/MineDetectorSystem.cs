using Content.Shared._RMC14.Inventory;
using Content.Shared._RMC14.MotionDetector;
using Content.Shared._RMC14.Weapons.Ranged.Battery;
using Content.Shared.Actions;
using Content.Shared.Coordinates;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.LandMines;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Toggleable;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.MineDetector;

public sealed partial class MineDetectorSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private EntityLookupSystem _entityLookup = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RMCGunBatterySystem _rmcGunBattery = default!;
    [Dependency] private SharedCMInventorySystem _rmcInventory = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private EntityQuery<MineDetectorComponent> _detectorQuery = default!;
    private EntityQuery<StorageComponent> _storageQuery = default!;
    private EntityQuery<LandMineComponent> _mineQuery = default!;

    private readonly HashSet<EntityUid> _mines = new();

    public override void Initialize()
    {
        _detectorQuery = GetEntityQuery<MineDetectorComponent>();
        _storageQuery = GetEntityQuery<StorageComponent>();
        _mineQuery = GetEntityQuery<LandMineComponent>();

        SubscribeLocalEvent<MineDetectorComponent, UseInHandEvent>(OnMineDetectorUseInHand);
        SubscribeLocalEvent<MineDetectorComponent, GetVerbsEvent<AlternativeVerb>>(OnMineDetectorGetVerbs);
        SubscribeLocalEvent<MineDetectorComponent, DroppedEvent>(OnMineDetectorDropped);
        SubscribeLocalEvent<MineDetectorComponent, RMCDroppedEvent>(OnMineDetectorDropped);
        SubscribeLocalEvent<MineDetectorComponent, ExaminedEvent>(OnMineDetectorExamined);
        SubscribeLocalEvent<MineDetectorComponent, ActivateInWorldEvent>(OnActivateInWorld);
    }

    private void OnMineDetectorUseInHand(Entity<MineDetectorComponent> ent, ref UseInHandEvent args)
    {
        if (!ent.Comp.HandToggleable)
            return;

        if (!_hands.IsHolding(args.User, ent))
            return;

        args.Handled = true;
        Toggle(ent);

        var user = args.User;
        ent.Comp.LastUser = user;
        Dirty(ent);

        _audio.PlayPredicted(ent.Comp.ToggleSound, ent, user);
    }

    private void OnActivateInWorld(Entity<MineDetectorComponent> ent, ref ActivateInWorldEvent args)
    {
        if (!ent.Comp.HandToggleable)
            return;

        if (!_container.TryGetContainingContainer(ent.Owner, out var container))
            return;

        if (!_hands.IsHolding(args.User, ent.Owner) &&
            HasComp<StorageComponent>(container.Owner) &&
            !_container.TryGetContainingContainer(container.Owner, out _))
            return;

        args.Handled = true;
        Toggle(ent);

        var user = args.User;
        ent.Comp.LastUser = user;
        Dirty(ent);

        _audio.PlayPredicted(ent.Comp.ToggleSound, ent, user);
    }

    private void OnMineDetectorGetVerbs(Entity<MineDetectorComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!ent.Comp.CanToggleRange)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = ent.Comp.Short ? "Change to long range mode" : "Change to short range mode",
            Act = () =>
            {
                ent.Comp.Short = !ent.Comp.Short;
                Dirty(ent);
                _audio.PlayPredicted(ent.Comp.ToggleSound, ent, user);
                _popup.PopupClient($"You change the {Name(ent)} to {(ent.Comp.Short ? "short" : "long")} range mode", ent, user);
            },
        });
    }

    private void OnMineDetectorDropped<T>(Entity<MineDetectorComponent> ent, ref T args)
    {
        if (!ent.Comp.DeactivateOnDrop)
            return;

        ent.Comp.Enabled = false;
        Dirty(ent);
        UpdateAppearance(ent);
    }

    private void OnMineDetectorExamined(Entity<MineDetectorComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(MineDetectorComponent)))
        {
            var mode = ent.Comp.Short ? "short" : "long";
            args.PushMarkup($"The mine detector is in [color=cyan]{mode}[/color] scanning mode.");
        }
    }

    private void UpdateAppearance(Entity<MineDetectorComponent> ent)
    {
        _appearance.SetData(ent, MineDetectorLayer.Setting, ent.Comp.Short ? MineDetectorSetting.Short : MineDetectorSetting.Long);
        _appearance.SetData(ent, ToggleableVisuals.Enabled, ent.Comp.Enabled);
    }

    private void DisableMineDetectors(EntityUid ent)
    {
        if (_detectorQuery.TryComp(ent, out var detector))
        {
            detector.Enabled = false;
            Dirty(ent, detector);
            UpdateAppearance((ent, detector));
        }

        if (_storageQuery.TryComp(ent, out var storage))
        {
            foreach (var stored in storage.StoredItems.Keys)
            {
                DisableMineDetectors(stored);
            }
        }
    }

    private TimeSpan GetRefreshRate(Entity<MineDetectorComponent> ent)
    {
        return ent.Comp.Short ? ent.Comp.ShortRefresh : ent.Comp.LongRefresh;
    }

    public void Toggle(Entity<MineDetectorComponent> ent)
    {
        ref var enabled = ref ent.Comp.Enabled;
        enabled = !enabled;

        if (enabled)
            ent.Comp.NextScanAt = _timing.CurTime + GetRefreshRate(ent);

        ent.Comp.Blips.Clear();
        Dirty(ent);
        UpdateAppearance(ent);
    }

    public void Disable(Entity<MineDetectorComponent> ent)
    {
        if (!ent.Comp.Enabled)
            return;

        Toggle(ent);
    }

    public bool IsEnabled(Entity<MineDetectorComponent?> ent)
    {
        return Resolve(ent, ref ent.Comp, false) && ent.Comp.Enabled;
    }

    public bool IsEnabled(EntityUid uid)
    {
        if (!TryComp<MineDetectorComponent>(uid, out var comp))
            return false;

        return comp.Enabled;
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;

        var detectors = EntityQueryEnumerator<MineDetectorComponent>();
        while (detectors.MoveNext(out var uid, out var detector))
        {
            if (!detector.Enabled)
                continue;

            if (time < detector.NextScanAt)
                continue;

            if (detector.LastUser is not { } lastUser)
                continue;

            // Only scan if the detector is being held in hand, not stored
            if (!_hands.IsHolding(lastUser, uid))
                continue;

            detector.LastScan = time;
            detector.NextScanAt = time + GetRefreshRate((uid, detector));
            Dirty(uid, detector);

            var range = detector.Short ? detector.ShortRange : detector.LongRange;
            _mines.Clear();
            _entityLookup.GetEntitiesInRange(uid.ToCoordinates(), range, _mines, LookupFlags.Uncontained);

            detector.Blips.Clear();
            foreach (var mine in _mines)
            {
                if (!_mineQuery.HasComp(mine))
                    continue;

                detector.Blips.Add(new Blip(_transform.GetMapCoordinates(mine), false));
            }

            UpdateAppearance((uid, detector));
            if (detector.Blips.Count == 0)
            {
                _audio.PlayEntity(detector.ScanEmptySound, lastUser, uid);
                continue;
            }

            _audio.PlayPvs(detector.ScanSound, uid);
        }
    }
}
