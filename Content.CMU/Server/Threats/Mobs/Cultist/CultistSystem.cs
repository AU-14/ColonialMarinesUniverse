using Content.Server.Administration.Managers;
using Content.Server.Antag;
using Content.Server.Mind;
using Content.Server.Radio.Components;
using Content.Server.Roles;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using CultistComponent = Content.Shared._CMU14.Threats.Mobs.Cultist.CultistComponent;
using HasKnowledgeOfXenoLanguageComponent = Content.Shared._CMU14.Threats.Mobs.Xeno.HasKnowledgeOfXenoLanguageComponent;

namespace Content.Server._CMU14.Threats.Mobs.Cultist;

public sealed partial class CultistSystem : EntitySystem
{
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private NpcFactionSystem _npcFaction = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private RoleSystem _role = default!;
    [Dependency] private GunIFFSystem _gunIFF = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(AddMakeCultistVerb);
    }

    private void AddMakeCultistVerb(GetVerbsEvent<Verb> args)
    {
        if (!TryComp(args.User, out ActorComponent? actor))
            return;

        ICommonSession player = actor.PlayerSession;

        if (!_admin.HasAdminFlag(player, AdminFlags.Fun))
            return;

        if (!HasComp<MindContainerComponent>(args.Target)
            || !TryComp(args.Target, out ActorComponent? targetActor))
            return;

        if (!TryComp(args.Target, out MarineComponent? marine))
            return;


        if (!HasComp<CultistComponent>(args.Target))
        {
            Verb clf = new()
            {
                Text = "Make Cultist",
                Category = VerbCategory.Antag,
                Icon = new SpriteSpecifier.Rsi(new("/Textures/_AU14/Interface/job_icons.rsi"),
                    "threat_member"),
                Act = () => { MakeCultist(args.Target); },
                Impact = LogImpact.High,
                Message = "Make Cultist"
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
        EnsureComp(Target, out IntrinsicRadioTransmitterComponent radio);
        radio.Channels.Add("Hivemind");
        EnsureComp(Target, out ActiveRadioComponent actrad);
        actrad.Channels.Add("Hivemind");
        var s = "Xeno";
        _npcFaction.AddFaction(Target, s);

        if (_mind.TryGetMind(Target, out EntityUid mindId, out MindComponent? mind))
        {
            _role.MindAddRole(mindId, "MindRoleCultist");

            if (mind is { UserId: not null } && _player.TryGetSessionById(mind.UserId, out ICommonSession? session))
            {
                _antag.SendBriefing(session,
                    Loc.GetString("roles-antag-cultist-greeting"),
                    Color.Red, null);
            }
        }
    }
}
