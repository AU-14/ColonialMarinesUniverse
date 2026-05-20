using Content.Server.Administration.Managers;
using Content.Server.Antag;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Mutiny;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.Administration;
using Content.Shared.AU14.CLF;
using Content.Shared.Database;
using Content.Shared.Mind.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Server._AU14.Antags.CLF;

public sealed partial class CLFMemberSystem : EntitySystem
{
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private NpcFactionSystem _npcFaction = default!;
    [Dependency] private RoleSystem _role = default!;
    [Dependency] private GunIFFSystem _gunIFF = default!;
    [Dependency] private ISharedPlayerManager _player = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(AddMakeCLFVerb);
    }

    private void AddMakeCLFVerb (GetVerbsEvent<Verb> args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        var player = actor.PlayerSession;

        if (!_admin.HasAdminFlag(player, AdminFlags.Fun))
            return;

        if (!HasComp<MindContainerComponent>(args.Target) || !TryComp<ActorComponent>(args.Target, out var targetActor))
            return;

        if (!TryComp<MarineComponent>(args.Target, out var marine))
            return;


        if (!HasComp<CLFMemberComponent>(args.Target))
        {
            Verb clf = new()
            {
                Text = "Make CLF Recruit",
                Category = VerbCategory.Antag,
                Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Interface/cm_job_icons.rsi"),
                    "hudCLF"),
                Act = () => { MakeCLF(args.Target); },
                Impact = LogImpact.High,
                Message = "Make CLF Recruit",
            };
            args.Verbs.Add(clf);
        }
    }

    private void MakeCLF(EntityUid Target)
    {
        EnsureComp<CLFMemberComponent>(Target, out var comp);
        _npcFaction.AddFaction(Target, comp.Faction);
        _gunIFF.AddUserFaction(Target, comp.IFF);

        if (_mind.TryGetMind(Target, out var mindId, out var mind))
        {
            _role.MindAddRole(mindId, "MindRoleCLFRecruit");

            if (mind is { UserId: not null } && _player.TryGetSessionById(mind.UserId, out var session))
            {
                _antag.SendBriefing(
                    session,
                    Loc.GetString("clf-tattoo-recruit-briefing"),
                    Color.Red,
                    new SoundPathSpecifier("/Audio/Ambience/Antag/headrev_start.ogg"));
            }
        }
    }
}
