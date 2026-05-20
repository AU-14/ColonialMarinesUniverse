using Content.Server.Administration.Managers;
using Content.Server.Antag;
using Content.Server.Mind;
using Content.Server.Radio.Components;
using Content.Server.Roles;
using Content.Shared._AU14.Xeno;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Administration;
using Content.Shared.AU14;
using Content.Shared.AU14.CLF;
using Content.Shared.Database;
using Content.Shared.Mind.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._AU14.Antags.Cultists;

public sealed partial class CultistSystem : EntitySystem
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
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(AddMakeCultistVerb);

    }


    private void AddMakeCultistVerb(GetVerbsEvent<Verb> args)
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


        if (!HasComp<CultistComponent>(args.Target))
        {
            Verb clf = new()
            {
                Text = "Make Cultist",
                Category = VerbCategory.Antag,
                Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/_AU14/Interface/job_icons.rsi"),
                    "threat_member"),
                Act = () => { MakeCultist(args.Target); },
                Impact = LogImpact.High,
                Message = "Make Cultist",
            };
            args.Verbs.Add(clf);
        }
    }

    private void MakeCultist(EntityUid Target)
    {
        EnsureComp<CultistComponent>(Target);
        EnsureComp<HasKnowledgeOfXenoLanguageComponent>(Target);
        RemCompDeferred<InfectableComponent>(Target);
        EnsureComp<IntrinsicRadioReceiverComponent>(Target);
        EnsureComp<IntrinsicRadioTransmitterComponent>(Target, out var radio);
        radio.Channels.Add("Hivemind");
        EnsureComp<ActiveRadioComponent>(Target, out var actrad);
        actrad.Channels.Add("Hivemind");
        string s = "Xeno";
        _npcFaction.AddFaction(Target, s);

        if (_mind.TryGetMind(Target, out var mindId, out var mind))
        {
            _role.MindAddRole(mindId, "MindRoleCultist");

            if (mind is { UserId: not null } && _player.TryGetSessionById(mind.UserId, out var session))
            {
                _antag.SendBriefing(
                    session,
                    Loc.GetString("roles-antag-cultist-greeting"),
                    Color.Red, null);
            }
        }
    }
}
