using Content.Server.Database;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost;
using Content.Server.Mind;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Robust.Shared.Player;
using Content.Server.NPC.HTN;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Xenonids.Leap;

public sealed partial class XenoParasiteSystem : SharedXenoParasiteSystem
{
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private GhostSystem _ghostSystem = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private HTNSystem _htn = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private DialogSystem _dialog = default!;

    private static readonly ProtoId<HTNCompoundPrototype> ActiveTask = "RMCParasiteActiveCompound";

    private static readonly ProtoId<HTNCompoundPrototype> DyingTask = "RMCParasiteDyingCompound";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoParasiteLarvaClaimChoiceEvent>(OnLarvaClaimChoice);
    }

    protected override void ParasiteLeapHit(Entity<XenoParasiteComponent> parasite)
    {
        if (!TryComp(parasite, out ActorComponent? actor))
            return;

        RemComp<GhostTakeoverAvailableComponent>(parasite);

        var session = actor.PlayerSession;

        Entity<MindComponent> mind;
        if (_mind.TryGetMind(session, out var mindId, out var mindComp))
            mind = (mindId, mindComp);
        else
            mind = _mind.CreateMind(session.UserId);

        var ghost = _ghostSystem.SpawnGhost((mind.Owner, mind.Comp), parasite);

        if (ghost != null)
        {
            EnsureComp<InfectionSuccessComponent>(ghost.Value);
            _popup.PopupEntity(Loc.GetString("rmc-xeno-egg-ghost-bypass-time"), ghost.Value, ghost.Value, PopupType.Medium);
            OpenLarvaClaimPrompt(ghost.Value, parasite);
        }

        _db.IncreaseInfects(session.UserId);
    }

    private void OpenLarvaClaimPrompt(EntityUid ghost, Entity<XenoParasiteComponent> parasite)
    {
        if (parasite.Comp.InfectedVictim is not { } victim)
            return;

        var ghostNet = GetNetEntity(ghost);
        var parasiteNet = GetNetEntity(parasite);
        var victimNet = GetNetEntity(victim);
        var options = new List<DialogOption>
        {
            new(
                Loc.GetString("rmc-xeno-parasite-larva-claim-yes"),
                new XenoParasiteLarvaClaimChoiceEvent(ghostNet, parasiteNet, victimNet, true)),
            new(
                Loc.GetString("rmc-xeno-parasite-larva-claim-no"),
                new XenoParasiteLarvaClaimChoiceEvent(ghostNet, parasiteNet, victimNet, false)),
        };

        _dialog.OpenOptions(
            ghost,
            ghost,
            Loc.GetString("rmc-xeno-parasite-larva-claim-title"),
            options,
            Loc.GetString("rmc-xeno-parasite-larva-claim-message"));
    }

    private void OnLarvaClaimChoice(XenoParasiteLarvaClaimChoiceEvent ev)
    {
        if (!TryGetEntity(ev.Ghost, out var ghost) ||
            !TryGetEntity(ev.Parasite, out var parasite) ||
            !TryGetEntity(ev.Victim, out var victim) ||
            !TryComp(ghost.Value, out ActorComponent? actor) ||
            !TryComp<XenoParasiteComponent>(parasite.Value, out var parasiteComp))
        {
            return;
        }

        if (actor.PlayerSession.AttachedEntity != ghost.Value)
            return;

        if (!TrySetLarvaClaimChoice((parasite.Value, parasiteComp), victim.Value, actor.PlayerSession.UserId, ev.Claim))
            return;

        if (!ev.Claim ||
            !TryComp<VictimInfectedComponent>(victim.Value, out var infected) ||
            infected.SpawnedLarva is not { } spawned)
        {
            return;
        }

        TryClaimLarva((victim.Value, infected), spawned);
    }

    protected override void LarvaLinked(Entity<VictimInfectedComponent> victim, EntityUid spawned)
    {
        TryClaimLarva(victim, spawned);
    }

    private bool TryClaimLarva(Entity<VictimInfectedComponent> victim, EntityUid spawned)
    {
        if (!TryGetLarvaClaimUser(victim, out var userId))
            return false;

        if (!_player.TryGetSessionById(userId, out var session))
            return false;

        if (session.AttachedEntity is not { } attached || !HasComp<GhostComponent>(attached))
            return false;

        if (!_mind.TryGetMind(session, out var mindId, out var mind))
            return false;

        _mind.TransferTo(mindId, spawned, mind: mind);
        ClearInfectorUser(victim);
        return true;
    }

    protected override void ChangeHTN(EntityUid parasite, ParasiteMode mode)
    {
        if (!TryComp<HTNComponent>(parasite, out var hTN))
            return;

        ProtoId<HTNCompoundPrototype>? RootTask = null;

        switch (mode)
        {
            case ParasiteMode.Active:
                RootTask = ActiveTask;
                break;
            case ParasiteMode.Dying:
                RootTask = DyingTask;
                break;
            default:
                return;
        }

        hTN.RootTask = new HTNCompoundTask { Task = RootTask.Value };
        _htn.Replan(hTN);
    }
}
