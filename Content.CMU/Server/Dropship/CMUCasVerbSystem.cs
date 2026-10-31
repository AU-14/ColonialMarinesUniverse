using Content.Server.Administration.Managers;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared.Administration;
using Content.Shared.Administration.Logs;
using Content.Shared.CMU14.Dropship.DirectFire;
using Content.Shared.Database;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Content.Shared.CMU14.Dropship.Cas;
using Content.Shared.CMU14.Radio;
using Content.Shared.Popups;

namespace Content.Server.CMU14.Dropship;

/// <summary>
/// Right-click verbs for RTO requests and admin servicing of dropship fire control.
/// </summary>
public sealed class CMUCasVerbSystem : EntitySystem
{
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private DropshipFireControlBootSystem _boot = default!;
    [Dependency] private SharedDropshipSystem _dropships = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private CMUCasRequestSystem _requests = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedDropshipWeaponSystem _weapons = default!;

    public const float MarkSearchRange = CMUCasRequestSystem.CallRange;

    private static readonly VerbCategory AdminCategory = new("cmu-cas-admin-verb-category", null);
    private static readonly VerbCategory CasCategory = new("cmu-cas-verb-category", null);

    private readonly HashSet<Entity<DropshipTargetComponent>> _marks = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(GetVerbsEvent<Verb> args)
    {
        AddAdminVerbs(args);
        AddRtoVerbs(args);
    }

    private void AddAdminVerbs(GetVerbsEvent<Verb> args)
    {
        if (!TryComp(args.User, out ActorComponent? actor) ||
            !_admin.HasAdminFlag(actor.PlayerSession, AdminFlags.Admin))
            return;

        var user = args.User;
        var target = args.Target;
        if (HasComp<DropshipWeaponPointComponent>(target) || HasComp<DropshipUtilityPointComponent>(target))
        {
            AddAdmin(args, "cmu-cas-admin-clear-fault", () => _boot.ClearFault(target), "cleared the fault and wear on");
            AddAdmin(args, "cmu-cas-admin-force-fault", () => _boot.ForceFault(target), "forced a fault on");
            AddAdmin(args, "cmu-cas-admin-refill", () => _weapons.AdminRefillAmmo(target), "refilled ammunition on");
        }

        if (HasComp<DropshipTerminalWeaponsComponent>(target) && _dropships.TryGetGridDropship(target, out var ship))
        {
            var shipUid = ship.Owner;
            AddAdmin(args, "cmu-cas-admin-skip-boot", () => _boot.SkipBoot(shipUid), "skipped the fire-control boot of");
            AddAdmin(args, "cmu-cas-admin-landing-reboot", () => _boot.ForceLandingReboot(shipUid, false),
                "forced a landing reboot on");
            AddAdmin(args, "cmu-cas-admin-refill-all", () =>
            {
                foreach (var point in ship.Comp.AttachmentPoints)
                    _weapons.AdminRefillAmmo(point);
            }, "refilled every hardpoint on");
            AddAdmin(args, "cmu-cas-admin-clear-all", () =>
            {
                foreach (var point in ship.Comp.AttachmentPoints)
                    _boot.ClearFault(point);
                _boot.SkipBoot(shipUid);
            }, "cleared every fault on");
        }

        void AddAdmin(GetVerbsEvent<Verb> verbs, string loc, Action act, string logText)
        {
            verbs.Verbs.Add(new Verb
            {
                Text = Loc.GetString(loc),
                Category = AdminCategory,
                Impact = LogImpact.Medium,
                Act = () =>
                {
                    act();
                    _adminLog.Add(LogType.RMCDropshipWeapon, LogImpact.Medium,
                        $"{ToPrettyString(user)} {logText} {ToPrettyString(target)} via admin verb");
                },
            });
        }
    }
    private void AddRtoVerbs(GetVerbsEvent<Verb> args)
    {
        var user = args.User;
        if (!HasComp<WearingANPRCComponent>(user) || _weapons.GetUserFaction(user) is not { } faction)
            return;

        EntityUid? mark = null;
        if (HasComp<DropshipTargetComponent>(args.Target))
            mark = args.Target;
        else if (args.Target == user)
            mark = FindNearestMark(user, faction);

        if (mark is { } target)
        {
            var priority = 10;
            foreach (var munition in Enum.GetValues<CMUCasMunition>())
            {
                var selected = munition;
                args.Verbs.Add(new Verb
                {
                    Text = Loc.GetString("cmu-cas-verb-request", ("munition", CMUCasRequestSystem.MunitionName(munition))),
                    Category = CasCategory,
                    Priority = priority--,
                    Act = () =>
                    {
                        if (_requests.TryCreateRequest(user, target, selected, out var id))
                            _popup.PopupEntity(Loc.GetString("cmu-cas-request-sent", ("id", id)), user, user);
                    },
                });
            }
        }
        else if (args.Target == user)
        {
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("cmu-cas-request-no-target"),
                Category = CasCategory,
                Disabled = true,
            });
        }

        if (args.Target != user)
            return;

        foreach (var request in _requests.GetOwnRequests(user))
        {
            var id = request.Id;
            if (request.Status == CMUCasRequestStatus.Accepted)
            {
                args.Verbs.Add(new Verb
                {
                    Text = Loc.GetString("cmu-cas-verb-clear-hot", ("id", id), ("target", request.TargetName)),
                    Category = CasCategory,
                    Priority = -10,
                    Act = () => _requests.TryClearHot(user, id),
                });
            }

            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("cmu-cas-verb-abort", ("id", id), ("target", request.TargetName)),
                Category = CasCategory,
                Priority = -20,
                Act = () => _requests.TryAbort(user, id),
            });
        }
    }

    private EntityUid? FindNearestMark(EntityUid user, string faction)
    {
        var origin = _transform.GetMapCoordinates(user);
        _marks.Clear();
        _lookup.GetEntitiesInRange(origin, MarkSearchRange, _marks);
        EntityUid? best = null;
        var bestDistance = float.MaxValue;
        foreach (var mark in _marks)
        {
            if (!string.IsNullOrEmpty(mark.Comp.CreatorFaction) &&
                !string.Equals(mark.Comp.CreatorFaction, faction, StringComparison.OrdinalIgnoreCase))
                continue;
            var distance = (_transform.GetMapCoordinates(mark).Position - origin.Position).LengthSquared();
            if (distance >= bestDistance || !_requests.CanCallTarget(user, mark))
                continue;
            bestDistance = distance;
            best = mark;
        }

        return best;
    }

}
