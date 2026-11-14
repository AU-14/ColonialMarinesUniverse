using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.EUI;
using Content.Server.Actions;
using Content.Server.Roles.Jobs;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Mutiny;
using Content.Shared._RMC14.Synth;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Mind.Components;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._RMC14.Marines.Mutiny;

public sealed partial class MutinySystem : SharedMutinySystem
{
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private EuiManager _euis = default!;
    [Dependency] private JobSystem _jobs = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GetVerbsEvent<Verb>>(AddMakeMutineerVerb);
        SubscribeLocalEvent<MutineerLeaderComponent, ComponentStartup>(OnLeaderStartup);
        SubscribeLocalEvent<MutineerLeaderComponent, ComponentShutdown>(OnLeaderShutdown);
        SubscribeLocalEvent<MutineerLeaderComponent, ComponentAdd>(OnLeaderAdded);
        SubscribeLocalEvent<MutineerLeaderComponent, ComponentRemove>(OnLeaderRemoved);
        SubscribeLocalEvent<MutineerLeaderComponent, MutineerRecruitActionEvent>(OnRecruitAction);
        InitializeCMU(); // CMU14: begin mutiny, sides, styled prompts
    }

    private void AddMakeMutineerVerb(GetVerbsEvent<Verb> args)
    {
        // To the reader. I am sorry for the abhorrent mess that is this method.
        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        var player = actor.PlayerSession;

        // Upstream's permission for making antags.
        if (!_adminManager.HasAdminFlag(player, AdminFlags.Fun))
            return;

        // More boilerplate....
        if (!HasComp<MindContainerComponent>(args.Target) || !TryComp<ActorComponent>(args.Target, out var targetActor))
            return;

        if (!TryComp<MarineComponent>(args.Target, out var marine))
            return;

        if (!HasComp<MutineerComponent>(args.Target))
        {
            Verb mutineer = new()
            {
                Text = "Make mutineer",
                Category = VerbCategory.Antag,
                Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Interface/cm_job_icons.rsi"),
                    "hudmutineer"),
                Act = () => { EnsureComp<MutineerComponent>(args.Target); },
                Impact = LogImpact.High,
                Message = "Make mutineer",
            };
            args.Verbs.Add(mutineer);
        }

        if (!HasComp<MutineerLeaderComponent>(args.Target))
        {
            Verb leader = new()
            {
                Text = "Make mutineer leader",
                Category = VerbCategory.Antag,
                // Use the regular mutineer icon for the admin verb until a unique leader icon exists
                Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Interface/cm_job_icons.rsi"),
                    "hudmutineerleader"),
                Act = () => { EnsureComp<MutineerLeaderComponent>(args.Target); },
                Impact = LogImpact.High,
                Message = "Make mutineer leader",
            };
            args.Verbs.Add(leader);
        }
    }

    public void MakeMutineer(EntityUid uid)
    {
        EnsureComp<MutineerComponent>(uid);
    }

    protected override void MutineerAdded(Entity<MutineerComponent> ent, ref ComponentAdd args)
    {
        if (!TryComp<MarineComponent>(ent, out var marineComponent))
            return;

        if (TryComp<ActorComponent>(ent, out var actor))
        {
            _chatManager.DispatchServerMessage(actor.PlayerSession, Loc.GetString("mutineer-status-added"));
            _chatManager.SendAdminAnnouncement($"Player {actor.PlayerSession.Name} was made a mutineer.");
        }

        Dirty(ent);
    }

    protected override void MutineerRemoved(Entity<MutineerComponent> ent, ref ComponentRemove args)
    {
        if (!TryComp<MarineComponent>(ent, out var marineComponent))
            return;

        if (TryComp<ActorComponent>(ent, out var actorComponent))
        {
            _chatManager.DispatchServerMessage(actorComponent.PlayerSession, Loc.GetString("mutineer-status-removed"));
            _chatManager.SendAdminAnnouncement($"Player {actorComponent.PlayerSession.Name} is no longer a mutineer.");
        }

        Dirty(ent);
    }

    private void OnLeaderStartup(Entity<MutineerLeaderComponent> ent, ref ComponentStartup args)
    {
        EnsureComp<MutineerComponent>(ent.Owner);
        _actions.AddAction(ent, ref ent.Comp.RecruitActionEntity, ent.Comp.RecruitAction);
        CMULeaderStartup(ent); // CMU14: begin mutiny action
        Dirty(ent);
    }

    private void OnLeaderShutdown(Entity<MutineerLeaderComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.RecruitActionEntity);
        CMULeaderShutdown(ent); // CMU14: release sworn recruits, drop begin mutiny
    }

    private void OnLeaderAdded(Entity<MutineerLeaderComponent> ent, ref ComponentAdd args)
    {
        if (TryComp<ActorComponent>(ent, out var actor))
        {
            _chatManager.DispatchServerMessage(actor.PlayerSession, Loc.GetString("mutineer-leader-status-added"));
            _chatManager.SendAdminAnnouncement($"Player {actor.PlayerSession.Name} was made a mutineer leader.");
        }
        Dirty(ent);
    }

    private void OnLeaderRemoved(Entity<MutineerLeaderComponent> ent, ref ComponentRemove args)
    {
        if (TryComp<ActorComponent>(ent, out var actor))
        {
            _chatManager.DispatchServerMessage(actor.PlayerSession, Loc.GetString("mutineer-leader-status-removed"));
            _chatManager.SendAdminAnnouncement($"Player {actor.PlayerSession.Name} is no longer a mutineer leader.");
        }
        // CMU14: OnLeaderShutdown already removed the recruit action, a second remove logged an error every time
        Dirty(ent);
    }

    private void OnRecruitAction(Entity<MutineerLeaderComponent> ent, ref MutineerRecruitActionEvent args)
    {
        // CMU14: the RMC checks needed MutinyEligible, which no CMU job has, and failed silently. CMU
        // checks side, MPs/command and synths, tells the leader why, and swears people in quietly
        CMURecruit(ent, ref args);
    }
}
