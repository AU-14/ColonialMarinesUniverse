using System.Diagnostics.CodeAnalysis;
using Content.Shared._RMC14.Emplacements;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Targeting;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Foldable;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Weapons.ATGM;

/// <summary>
///     A.T.G.M. crew interactions: loading a missile, wiring the spotter's finder to the launcher, and the
///     two-man fire interlock. Missile flight and guidance are server-only (see the server guidance system).
/// </summary>
/// <remarks>
///     Loading is handled on the tripod because <see cref="SharedWeaponMountSystem"/> only reloads through a
///     <c>gun_magazine</c> item slot, which the launcher deliberately lacks, and returns early for a locked
///     weapon, so the two InteractUsing handlers never act on the same click.
/// </remarks>
public sealed partial class CMUATGMSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private FoldableSystem _foldable = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private SharedRMCTargetingSystem _targeting = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUATGMMountComponent, InteractUsingEvent>(OnMountInteractUsing);
        SubscribeLocalEvent<CMUATGMMountComponent, CMUATGMLoadDoAfterEvent>(OnLoadDoAfter);
        SubscribeLocalEvent<CMUATGMMountComponent, CMUATGMLinkDoAfterEvent>(OnLinkDoAfter);
        SubscribeLocalEvent<CMUATGMMountComponent, ExaminedEvent>(OnMountExamined);

        SubscribeLocalEvent<CMUATGMLauncherComponent, AttemptShootEvent>(OnLauncherAttemptShoot);
        SubscribeLocalEvent<CMUATGMLauncherComponent, ComponentShutdown>(OnLauncherShutdown);

        SubscribeLocalEvent<CMUATGMFinderComponent, ComponentShutdown>(OnFinderShutdown);
        SubscribeLocalEvent<CMUATGMFinderComponent, ExaminedEvent>(OnFinderExamined);

        InitializeLock();
    }

    private void OnMountInteractUsing(Entity<CMUATGMMountComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled ||
            !TryComp(ent, out WeaponMountComponent? mount) ||
            !TryGetLauncher(ent, out var launcher))
        {
            return;
        }

        if (HasComp<CMUATGMFinderComponent>(args.Used))
        {
            args.Handled = true;
            TryStartLink(ent, args.User, args.Used);
            return;
        }

        if (TryComp(launcher, out BallisticAmmoProviderComponent? ballistic) &&
            _whitelist.IsWhitelistPass(ballistic.Whitelist, args.Used))
        {
            args.Handled = true;
            TryStartLoad((ent, ent.Comp, mount), (launcher, launcher.Comp, ballistic), args.User, args.Used);
        }
    }

    private bool TryStartLoad(
        Entity<CMUATGMMountComponent, WeaponMountComponent> mount,
        Entity<CMUATGMLauncherComponent, BallisticAmmoProviderComponent> launcher,
        EntityUid user,
        EntityUid missile)
    {
        if (!CanLoadPopup(mount, launcher, user))
            return false;

        var doAfter = new DoAfterArgs(EntityManager, user, mount.Comp1.LoadDelay, new CMUATGMLoadDoAfterEvent(), mount, mount, missile)
        {
            BreakOnMove = true,
            BreakOnHandChange = true,
            NeedHand = true,
            ForceVisible = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _popup.PopupEntity(Loc.GetString("cmu-atgm-load-start-self", ("launcher", mount.Owner), ("missile", missile)), mount, user);
        if (_net.IsServer)
        {
            _popup.PopupEntity(Loc.GetString("cmu-atgm-load-start-others", ("user", user), ("launcher", mount.Owner), ("missile", missile)),
                mount,
                Filter.PvsExcept(user),
                true);
        }

        return true;
    }

    private bool CanLoadPopup(
        Entity<CMUATGMMountComponent, WeaponMountComponent> mount,
        Entity<CMUATGMLauncherComponent, BallisticAmmoProviderComponent> launcher,
        EntityUid user)
    {
        string? reason = null;
        if (!IsDeployed(mount))
            reason = "cmu-atgm-not-deployed";
        else if (mount.Comp2.User == user)
            reason = "cmu-atgm-gunner-cannot-load";
        else if (!_skills.HasSkill(user, launcher.Comp1.Skill, 1))
            reason = "cmu-atgm-unskilled";
        else if (_gun.IsFull((launcher, launcher.Comp2)))
            reason = "cmu-atgm-already-loaded";

        if (reason == null)
            return true;

        _popup.PopupEntity(Loc.GetString(reason, ("launcher", mount.Owner)), mount, user, PopupType.SmallCaution);
        return false;
    }

    private void OnLoadDoAfter(Entity<CMUATGMMountComponent> ent, ref CMUATGMLoadDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } missile)
            return;

        args.Handled = true;
        if (!TryComp(ent, out WeaponMountComponent? mount) ||
            !TryGetLauncher(ent, out var launcher) ||
            !TryComp(launcher, out BallisticAmmoProviderComponent? ballistic))
        {
            return;
        }

        var user = args.User;
        if (!CanLoadPopup((ent, ent.Comp, mount), (launcher, launcher.Comp, ballistic), user) ||
            !_gun.TryBallisticInsert((launcher, ballistic), missile, user))
        {
            return;
        }

        _appearance.SetData(ent, WeaponMountComponentVisualLayers.MountedAmmo, true);
        _popup.PopupEntity(Loc.GetString("cmu-atgm-loaded", ("launcher", ent.Owner)), ent, user);
    }

    private bool TryStartLink(Entity<CMUATGMMountComponent> mount, EntityUid user, EntityUid finder)
    {
        if (!CanLinkPopup(mount, user))
            return false;

        var doAfter = new DoAfterArgs(EntityManager, user, mount.Comp.LinkDelay, new CMUATGMLinkDoAfterEvent(), mount, mount, finder)
        {
            BreakOnMove = true,
            BreakOnHandChange = true,
            NeedHand = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _popup.PopupEntity(Loc.GetString("cmu-atgm-link-start", ("launcher", mount.Owner), ("finder", finder)), mount, user);
        return true;
    }

    private bool CanLinkPopup(Entity<CMUATGMMountComponent> mount, EntityUid user)
    {
        string? reason = null;
        if (!TryGetLauncher(mount, out var launcher))
            return false;

        if (!IsDeployed(mount))
            reason = "cmu-atgm-not-deployed";
        else if (!_skills.HasSkill(user, launcher.Comp.Skill, 1))
            reason = "cmu-atgm-unskilled";

        if (reason == null)
            return true;

        _popup.PopupEntity(Loc.GetString(reason, ("launcher", mount.Owner)), mount, user, PopupType.SmallCaution);
        return false;
    }

    private void OnLinkDoAfter(Entity<CMUATGMMountComponent> ent, ref CMUATGMLinkDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } finderUid)
            return;

        args.Handled = true;
        if (!TryComp(finderUid, out CMUATGMFinderComponent? finder) ||
            !TryGetLauncher(ent, out var launcher) ||
            !CanLinkPopup(ent, args.User))
        {
            return;
        }

        Link(launcher, (finderUid, finder));
        _popup.PopupEntity(Loc.GetString("cmu-atgm-linked", ("launcher", ent.Owner), ("finder", finderUid)), ent, args.User);
    }

    private void OnMountExamined(Entity<CMUATGMMountComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !TryGetLauncher(ent, out var launcher))
            return;

        using (args.PushGroup(nameof(CMUATGMMountComponent)))
        {
            var loaded = TryComp(launcher, out BallisticAmmoProviderComponent? ballistic) && ballistic.Count > 0;
            args.PushMarkup(Loc.GetString(loaded ? "cmu-atgm-examine-loaded" : "cmu-atgm-examine-empty"));
            args.PushMarkup(Loc.GetString(IsLinked(launcher) ? "cmu-atgm-examine-linked" : "cmu-atgm-examine-unlinked"));
        }
    }

    private void OnFinderExamined(Entity<CMUATGMFinderComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var linked = ent.Comp.LinkedLauncher is { } launcher &&
                     TryComp(launcher, out CMUATGMLauncherComponent? comp) &&
                     comp.LinkedFinder == ent.Owner;
        args.PushMarkup(Loc.GetString(linked ? "cmu-atgm-finder-examine-linked" : "cmu-atgm-finder-examine-unlinked"));
        if (ent.Comp.LockedTarget is { } target && !TerminatingOrDeleted(target))
            args.PushMarkup(Loc.GetString("cmu-atgm-finder-examine-locked", ("target", target)));
    }

    private void OnLauncherAttemptShoot(Entity<CMUATGMLauncherComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled)
            return;

        if (TryGetSpotter(ent, args.User, out _, out _, out var reason))
            return;

        args.Cancelled = true;
        _popup.PopupEntity(Loc.GetString(reason), args.User, args.User, PopupType.SmallCaution);
    }

    private void OnLauncherShutdown(Entity<CMUATGMLauncherComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.LinkedFinder is not { } finder ||
            !TryComp(finder, out CMUATGMFinderComponent? finderComp) ||
            finderComp.LinkedLauncher != ent.Owner)
        {
            return;
        }

        finderComp.LinkedLauncher = null;
        Dirty(finder, finderComp);
        ClearLock((finder, finderComp));
    }

    private void OnFinderShutdown(Entity<CMUATGMFinderComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.LinkedLauncher is not { } launcher ||
            !TryComp(launcher, out CMUATGMLauncherComponent? launcherComp) ||
            launcherComp.LinkedFinder != ent.Owner)
        {
            return;
        }

        launcherComp.LinkedFinder = null;
        Dirty(launcher, launcherComp);
    }

    /// <summary>
    ///     Wires a finder to a launcher, breaking any link either of them had before.
    /// </summary>
    public void Link(Entity<CMUATGMLauncherComponent> launcher, Entity<CMUATGMFinderComponent> finder)
    {
        if (launcher.Comp.LinkedFinder is { } oldFinder &&
            oldFinder != finder.Owner &&
            TryComp(oldFinder, out CMUATGMFinderComponent? oldFinderComp) &&
            oldFinderComp.LinkedLauncher == launcher.Owner)
        {
            oldFinderComp.LinkedLauncher = null;
            Dirty(oldFinder, oldFinderComp);
            ClearLock((oldFinder, oldFinderComp));
        }

        if (finder.Comp.LinkedLauncher is { } oldLauncher &&
            oldLauncher != launcher.Owner &&
            TryComp(oldLauncher, out CMUATGMLauncherComponent? oldLauncherComp) &&
            oldLauncherComp.LinkedFinder == finder.Owner)
        {
            oldLauncherComp.LinkedFinder = null;
            Dirty(oldLauncher, oldLauncherComp);
        }

        // A lock belongs to the launcher it was taken for.
        if (finder.Comp.LinkedLauncher != launcher.Owner)
            ClearLock(finder);

        launcher.Comp.LinkedFinder = finder;
        Dirty(launcher);
        finder.Comp.LinkedLauncher = launcher;
        Dirty(finder);
    }

    public bool IsLinked(Entity<CMUATGMLauncherComponent> launcher)
    {
        return launcher.Comp.LinkedFinder is { } finder &&
               !TerminatingOrDeleted(finder) &&
               TryComp(finder, out CMUATGMFinderComponent? finderComp) &&
               finderComp.LinkedLauncher == launcher.Owner;
    }

    /// <summary>
    ///     Gets the fixed launcher inside an A.T.G.M. tripod.
    /// </summary>
    public bool TryGetLauncher(EntityUid mount, out Entity<CMUATGMLauncherComponent> launcher)
    {
        launcher = default;
        if (!TryComp(mount, out WeaponMountComponent? mountComp) ||
            mountComp.MountedEntity is not { } weapon ||
            !TryComp(weapon, out CMUATGMLauncherComponent? launcherComp))
        {
            return false;
        }

        launcher = (weapon, launcherComp);
        return true;
    }

    /// <summary>
    ///     Whether the tripod is anchored and unfolded.
    /// </summary>
    public bool IsDeployed(EntityUid mount)
    {
        return Transform(mount).Anchored && !_foldable.IsFolded(mount);
    }

    /// <summary>
    ///     Whether <paramref name="guider"/> can currently steer with <paramref name="finder"/>: alive, conscious
    ///     and holding it.
    /// </summary>
    public bool CanGuide(EntityUid guider, EntityUid finder)
    {
        return !TerminatingOrDeleted(guider) &&
               !TerminatingOrDeleted(finder) &&
               _mobState.IsAlive(guider) &&
               !_mobState.IsIncapacitated(guider) &&
               _hands.IsHolding(guider, finder);
    }

    /// <summary>
    ///     The two-man interlock: finds the spotter who would guide a missile fired by <paramref name="gunner"/>.
    /// </summary>
    /// <param name="reason">Locale id explaining why no spotter is available.</param>
    public bool TryGetSpotter(
        Entity<CMUATGMLauncherComponent> launcher,
        EntityUid gunner,
        out EntityUid spotter,
        out EntityUid finder,
        [NotNullWhen(false)] out string? reason)
    {
        spotter = default;
        finder = default;

        if (!IsLinked(launcher) || launcher.Comp.LinkedFinder is not { } linked)
        {
            reason = "cmu-atgm-no-finder";
            return false;
        }

        if (!_hands.IsHeld(linked, out var holder))
        {
            reason = "cmu-atgm-finder-not-held";
            return false;
        }

        if (holder == gunner)
        {
            reason = "cmu-atgm-spotter-is-gunner";
            return false;
        }

        if (!CanGuide(holder.Value, linked))
        {
            reason = "cmu-atgm-spotter-incapacitated";
            return false;
        }

        if (!_skills.HasSkill(holder.Value, launcher.Comp.Skill, 1))
        {
            reason = "cmu-atgm-spotter-unskilled";
            return false;
        }

        var launcherPos = _transform.GetMapCoordinates(launcher);
        var spotterPos = _transform.GetMapCoordinates(holder.Value);
        var range = CompOrNull<CMUATGMFinderComponent>(linked)?.MaxLinkRange ?? 0f;
        if (launcherPos.MapId != spotterPos.MapId ||
            (launcherPos.Position - spotterPos.Position).LengthSquared() > range * range)
        {
            reason = "cmu-atgm-spotter-too-far";
            return false;
        }

        spotter = holder.Value;
        finder = linked;
        reason = null;
        return true;
    }
}
