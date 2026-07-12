using Content.Server._CMU14.Language;
using Content.Server.Administration.Logs;
using Content.Server.Antag;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Roles;
using Content.Shared._CMU14.Threats.Mobs.CLF;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using CLFMemberComponent = Content.Shared._CMU14.Threats.Mobs.CLF.CLFMemberComponent;
using TattooDoAfterEvent = Content.Shared._CMU14.Threats.Mobs.CLF.TattooDoAfterEvent;
using TattooGunComponent = Content.Shared._CMU14.Threats.Mobs.CLF.TattooGunComponent;
using TattooPendingComponent = Content.Shared._CMU14.Threats.Mobs.CLF.TattooPendingComponent;

namespace Content.Server._CMU14.Threats.Mobs.CLF;

public sealed partial class CLFTattooSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private FactionLanguageSystem _factionLanguage = default!;
    [Dependency] private GunIFFSystem _gunIFF = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private NpcFactionSystem _npcFaction = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _protoManager = default!;
    [Dependency] private RoleSystem _role = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TattooGunComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<TattooPendingComponent, TattooAcceptEvent>(OnTattooAccepted);
        SubscribeLocalEvent<TattooGunComponent, TattooDoAfterEvent>(OnTattooDoAfter);
    }

    private bool IsDeadOrCrit(EntityUid target) => _mobState.IsDead(target) || _mobState.IsCritical(target);

    /// <summary>
    ///     Returns true if the target entity belongs to any of the blocked departments on the tattoo gun.
    /// </summary>
    private bool IsInBlockedDepartment(EntityUid target, TattooGunComponent comp)
    {
        if (comp.BlockedDepartments.Count == 0)
            return false;

        if (!_mind.TryGetMind(target, out EntityUid mindId, out _))
            return false;

        if (!_jobs.MindTryGetJob(mindId, out JobPrototype? job))
            return false;

        // Use the same proven pattern as BuyerDepartmentCondition:
        // enumerate all departments and check if the job is in any blocked one.
        foreach (DepartmentPrototype department in _protoManager.EnumeratePrototypes<DepartmentPrototype>())
        {
            if (department.Roles.Contains(job.ID))
            {
                foreach (ProtoId<DepartmentPrototype> blocked in comp.BlockedDepartments)
                {
                    if (blocked.Id == department.ID)
                        return true;
                }
            }
        }

        return false;
    }

    private void OnAfterInteract(EntityUid uid, TattooGunComponent comp, AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target || !args.CanReach)
            return;

        EntityUid user = args.User;

        // Only CLF members can use the tattoo gun
        if (!HasComp<CLFMemberComponent>(user))
        {
            _popup.PopupEntity(Loc.GetString("clf-tattoo-not-clf"), user, user);
            args.Handled = true;

            return;
        }

        // Must be a humanoid
        if (!HasComp<HumanoidAppearanceComponent>(target))
            return;

        // Must be alive
        if (IsDeadOrCrit(target))
        {
            _popup.PopupEntity(Loc.GetString("clf-tattoo-target-dead"), user, user);
            args.Handled = true;

            return;
        }

        // Already a CLF member
        if (HasComp<CLFMemberComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("clf-tattoo-already-member"), user, user);
            args.Handled = true;

            return;
        }

        // Check storage for ink
        if (!TryComp(uid, out StorageComponent? storage) ||
            storage.Container.ContainedEntities.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("clf-tattoo-no-ink"), user, user);
            args.Handled = true;

            return;
        }

        // Check if target's job is in a blocked department
        if (IsInBlockedDepartment(target, comp))
        {
            _popup.PopupEntity(Loc.GetString("clf-tattoo-target-blocked"), user, user);
            args.Handled = true;

            return;
        }

        // Check if target already has a pending tattoo offer
        if (TryComp<TattooPendingComponent>(target, out _))
        {
            if (HasComp<DialogComponent>(target))
            {
                _popup.PopupEntity(Loc.GetString("clf-tattoo-already-pending"), user, user);
                args.Handled = true;

                return;
            }

            // Stale pending component from a cancelled dialog, clean up
            RemComp<TattooPendingComponent>(target);
        }

        // Add pending component to track the tattoo artist and gun
        var pending = EnsureComp<TattooPendingComponent>(target);
        pending.User = user;
        pending.TattooGun = uid;

        // Open confirmation dialog on the target player
        _dialog.OpenConfirmation(
            target,
            target,
            Loc.GetString("clf-tattoo-dialog-title"),
            Loc.GetString("clf-tattoo-dialog-message"),
            new TattooAcceptEvent()
        );

        args.Handled = true;
    }

    private void OnTattooAccepted(EntityUid uid, TattooPendingComponent comp, TattooAcceptEvent args)
    {
        EntityUid user = comp.User;
        EntityUid tattooGun = comp.TattooGun;
        EntityUid target = uid;

        // Clean up pending component
        RemComp<TattooPendingComponent>(target);

        // Validate entities still exist
        if (Deleted(user) || Deleted(tattooGun) || Deleted(target))
            return;

        if (!TryComp(tattooGun, out TattooGunComponent? gunComp))
            return;

        // Re-check ink
        if (!TryComp(tattooGun, out StorageComponent? storage) || storage.Container.ContainedEntities.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("clf-tattoo-no-ink"), user, user);

            return;
        }

        // Re-check target is alive
        if (IsDeadOrCrit(target))
            return;

        // Re-check not already a member
        if (HasComp<CLFMemberComponent>(target))
            return;

        // Start DoAfter on the user, event raised on the tattoo gun
        var doAfterArgs = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(gunComp.DoAfterDuration),
            new TattooDoAfterEvent(), tattooGun, target, tattooGun)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BreakOnHandChange = true,
            NeedHand = true
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs)) return;
        _popup.PopupEntity(Loc.GetString("clf-tattoo-begin", ("user", user)), target, target);
        _popup.PopupEntity(Loc.GetString("clf-tattoo-begin-user", ("target", target)), user, user);
    }

    private void OnTattooDoAfter(EntityUid uid, TattooGunComponent comp, TattooDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (args.Target is not { } target)
            return;

        args.Handled = true;

        // Final safety checks
        if (HasComp<CLFMemberComponent>(target))
            return;

        if (IsDeadOrCrit(target))
            return;

        // Consume one ink cartridge
        if (!TryComp(uid, out StorageComponent? storage) ||
            storage.Container.ContainedEntities.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("clf-tattoo-no-ink"), args.User, args.User);

            return;
        }

        EntityUid ink = storage.Container.ContainedEntities[0];
        _container.Remove(ink, storage.Container, force: true);
        QueueDel(ink);

        // Add CLF faction
        _npcFaction.AddFaction(target, comp.Faction);
        _gunIFF.AddUserFaction(target, comp.IFF);

        // Add CLF member component
        EnsureComp<CLFMemberComponent>(target);
        _factionLanguage.ApplyFactionLanguageToNewMember(target, "CLF"); // for the lang system

        // Add mind role and send briefing
        if (_mind.TryGetMind(target, out EntityUid mindId, out MindComponent? mind))
        {
            _role.MindAddRole(mindId, comp.Role);

            if (mind is { UserId: not null } && _player.TryGetSessionById(mind.UserId, out ICommonSession? session))
            {
                _antag.SendBriefing(session,
                    Loc.GetString(comp.Briefing),
                    Color.Red,
                    comp.Sound);
            }
        }

        _adminLog.Add(
            LogType.Mind,
            LogImpact.Medium,
            $"{ToPrettyString(target)} was tattooed into the CLF by {ToPrettyString(args.User)}");

        _popup.PopupEntity(Loc.GetString("clf-tattoo-success", ("target", target)), args.User, args.User);
        _popup.PopupEntity(Loc.GetString("clf-tattoo-success-target"), target, target);
    }
}
