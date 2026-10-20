using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared.CMU14.Dropship.Cas;
using Content.Shared.CMU14.Radio;
using Content.Shared.Popups;
using Content.Shared.Verbs;

namespace Content.Server.CMU14.Dropship;

/// <summary>
/// Right-click verbs for RTOs filing and managing CAS requests.
/// </summary>
public sealed class CMUCasVerbSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private CMUCasRequestSystem _requests = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedDropshipWeaponSystem _weapons = default!;

    public const float MarkSearchRange = CMUCasRequestSystem.CallRange;

    private static readonly VerbCategory CasCategory = new("cmu-cas-verb-category", null);

    private readonly HashSet<Entity<DropshipTargetComponent>> _marks = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(GetVerbsEvent<Verb> args)
    {
        AddRtoVerbs(args);
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
