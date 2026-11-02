using Content.Shared._RMC14.Sentry;
using Content.Shared.CMU14.Fob;
using Content.Shared.CMU14.Sentry;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Tag;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Fob;

public sealed partial class CMUFobDefenseSystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private SharedToolSystem _tools = default!;

    private static readonly ProtoId<ToolQualityPrototype> Screwing = "Screwing";
    private static readonly ProtoId<ToolQualityPrototype> Anchoring = "Anchoring";

    private void InitializeInteractions()
    {
        SubscribeLocalEvent<CMUFobSupportGunComponent, CMUSentryRepairAttemptEvent>(OnRepairAttempt);
        SubscribeLocalEvent<CMUFobSupportGunComponent, InteractUsingEvent>(OnInteractUsing,
            before: [typeof(SentrySystem), typeof(CMUSentryRepairSystem)]);
        SubscribeLocalEvent<CMUFobSupportGunComponent, CMUFobReloadDoAfterEvent>(OnReloadFinished);
        SubscribeLocalEvent<CMUFobSupportGunComponent, DoAfterAttemptEvent<CMUFobReloadDoAfterEvent>>(OnReloadAttempt);
    }

    private bool CanService(Entity<CMUFobSupportGunComponent> gun, EntityUid user)
        => TryGetMount(gun, out _, out var network) && _controls.CanOperate(gun, user, network.Comp.Faction);

    private void OnRepairAttempt(Entity<CMUFobSupportGunComponent> gun, ref CMUSentryRepairAttemptEvent args)
    {
        if (!CanService(gun, args.User))
            args.Cancelled = true;
    }

    private void OnInteractUsing(Entity<CMUFobSupportGunComponent> gun, ref InteractUsingEvent args)
    {
        if (args.Handled || HasComp<CMUSentryBrokenComponent>(gun))
            return;
        args.Handled = true;
        if (!CanService(gun, args.User))
            return;
        if (_tools.HasQuality(args.Used, Anchoring))
        {
            TryRemoveModule(gun, args.User);
            return;
        }
        if (_tools.HasQuality(args.Used, Screwing))
        {
            if (!TryGetMount(gun, out var mount, out var bank)
                || bank.Comp.State is CMUFobReserveState.Active or CMUFobReserveState.Starting)
            {
                _popup.PopupEntity(Loc.GetString("cmu-fob-gun-rotation-denied"), gun, args.User);
                return;
            }
            mount.Comp.Facing += Angle.FromDegrees(90);
            _transform.SetWorldRotation(gun, mount.Comp.Facing);
            ClearTarget(gun);
            return;
        }
        TryStartReload(gun, args.User, args.Used);
    }

    private bool CanReload(Entity<CMUFobSupportGunComponent> gun, EntityUid user, EntityUid magazine)
    {
        return CanService(gun, user) && !HasComp<CMUSentryBrokenComponent>(gun)
            && _hands.TryGetActiveItem(user, out var held) && held == magazine
            && HasComp<BallisticAmmoProviderComponent>(magazine) && _tags.HasTag(magazine, gun.Comp.MagazineTag)
            && _guns.GetAmmoCount(gun) == 0;
    }

    public bool TryStartReload(Entity<CMUFobSupportGunComponent> gun, EntityUid user, EntityUid magazine)
    {
        if (_doAfter.IsRunning(gun.Comp.Reload) || !CanReload(gun, user, magazine))
            return false;
        var args = new DoAfterArgs(EntityManager, user, gun.Comp.ReloadDelay,
            new CMUFobReloadDoAfterEvent(), gun, gun, magazine)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnHandChange = true,
            BreakOnDamage = true,
            AttemptFrequency = AttemptFrequency.EveryTick,
        };
        if (!_doAfter.TryStartDoAfter(args, out var id))
            return false;
        gun.Comp.Reload = id;
        ClearTarget(gun);
        UpdatePresentation(gun);
        return true;
    }

    private void OnReloadAttempt(Entity<CMUFobSupportGunComponent> gun,
        ref DoAfterAttemptEvent<CMUFobReloadDoAfterEvent> args)
    {
        if (args.DoAfter.Args.Used is not { } magazine || !CanReload(gun, args.DoAfter.Args.User, magazine))
            args.Cancel();
    }

    private void OnReloadFinished(Entity<CMUFobSupportGunComponent> gun, ref CMUFobReloadDoAfterEvent args)
    {
        if (args.Handled)
            return;
        gun.Comp.Reload = null;
        args.Handled = true;
        if (args.Cancelled || args.Used is not { } magazine || !CanReload(gun, args.User, magazine))
            return;
        var slot = _containers.EnsureContainer<ContainerSlot>(gun, "gun_magazine");
        if (!_containers.CanInsert(magazine, slot, true))
            return;
        _containers.EmptyContainer(slot, destination: _transform.GetMoverCoordinates(args.User));
        if (_containers.Insert(magazine, slot))
        {
            _effects.Play(gun, CMUFobPresentationCue.Reload);
            _popup.PopupEntity(Loc.GetString("cmu-fob-gun-reloaded"), gun, args.User);
        }
        UpdatePresentation(gun);
    }
}
