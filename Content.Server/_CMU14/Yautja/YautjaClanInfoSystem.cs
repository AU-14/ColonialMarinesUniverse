using Content.Server.EUI;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Database;
using Content.Shared.Verbs;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._CMU14.Yautja;

public sealed class YautjaClanInfoSystem : EntitySystem
{
    [Dependency] private EuiManager _eui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(GetVerbsEvent<Verb> args)
    {
        if (args.User != args.Target ||
            !TryComp<ActorComponent>(args.User, out var actor) ||
            !HasComp<YautjaComponent>(args.User))
        {
            return;
        }

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("cmu-yautja-clan-info-verb"),
            Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/_CMU14/Yautja/rank_icons.rsi"), "blooded"),
            Impact = LogImpact.Low,
            Act = () => _eui.OpenEui(new YautjaClanInfoEui(), actor.PlayerSession),
        });
    }
}
