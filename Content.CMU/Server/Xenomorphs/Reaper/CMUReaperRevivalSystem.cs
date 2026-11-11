using Content.Server.EUI;
using Content.Server.Ghost;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.Xenomorphs.Reaper;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Robust.Shared.Timing;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Xenomorphs.Reaper;

public sealed partial class CMUReaperRevivalSystem : EntitySystem
{
    [Dependency] private EuiManager _eui = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private ISharedPlayerManager _players = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoComponent, CMUReaperRevivedEvent>(OnRevived);
        SubscribeLocalEvent<XenoComponent, CMUReaperRevivalStartedEvent>(OnRevivalStarted);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (!HasComp<XenoComponent>(args.Target))
            return;

        if (args.NewMobState == MobState.Dead)
            EnsureComp<CMUReaperCorpseComponent>(args.Target).DiedAt = _timing.CurTime;
        else
            RemComp<CMUReaperCorpseComponent>(args.Target);
    }

    private void OnRevivalStarted(Entity<XenoComponent> xeno, ref CMUReaperRevivalStartedEvent args)
    {
        OfferReturnToBody(xeno);
    }

    private void OnRevived(Entity<XenoComponent> xeno, ref CMUReaperRevivedEvent args)
    {
        OfferReturnToBody(xeno);
    }

    private void OfferReturnToBody(EntityUid xeno)
    {
        // Offer the same return-to-body prompt as defibrillation; never take a player out of another body.
        if (_mind.TryGetMind(xeno, out var uid, out var mind) && mind.CurrentEntity != xeno
            && _players.TryGetSessionById(mind.UserId, out var session))
            _eui.OpenEui(new ReturnToBodyEui(mind, _mind, _players), session);
    }
}
