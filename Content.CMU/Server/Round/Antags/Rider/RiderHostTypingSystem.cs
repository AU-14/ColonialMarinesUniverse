using Content.Shared.ActionBlocker;
using Content.Shared.Chat.TypingIndicator;
using Content.Shared.CMU14.Round.Antags.Rider;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Round.Antags.Rider;

public sealed class RiderHostTypingSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    // rider -> (host, session) for every host we've lit up, so we can turn it off again
    private readonly Dictionary<EntityUid, (EntityUid Host, ICommonSession Session)> _lit = new();
    private readonly List<EntityUid> _stale = new();

    public override void Initialize()
    {
        SubscribeNetworkEvent<RiderHostTypingEvent>(OnHostTyping);
    }

    private void OnHostTyping(RiderHostTypingEvent ev, EntitySessionEventArgs args)
    {
        if (!TryGetRider(args.SenderSession.AttachedEntity, out var rider)
            || rider.Comp.Host is not { } host
            || rider.Comp.SeizeActive)
            return;

        var state = ev.State;
        if (!Enum.IsDefined(state))
            return;

        // same gate stock typing uses, a gagged host shouldn't look like they're about to talk
        if (state != TypingIndicatorState.None && !_blocker.CanSpeak(host) && !_blocker.CanEmote(host))
            state = TypingIndicatorState.None;

        SetState(host, state);

        if (state == TypingIndicatorState.None)
            _lit.Remove(rider.Owner);
        else
            _lit[rider.Owner] = (host, args.SenderSession);
    }

    public override void Update(float frameTime)
    {
        if (_lit.Count == 0)
            return;

        // eject, withdraw, seize or a disconnect mid-sentence never sends a None, so sweep for those
        _stale.Clear();
        foreach (var (riderUid, (host, session)) in _lit)
        {
            if (TryComp<RiderComponent>(riderUid, out var rider)
                && rider.Host == host
                && !rider.SeizeActive
                && TryGetRider(session.AttachedEntity, out var attached)
                && attached.Owner == riderUid)
                continue;

            _stale.Add(riderUid);
        }

        foreach (var riderUid in _stale)
        {
            if (_lit.Remove(riderUid, out var entry) && !TerminatingOrDeleted(entry.Host))
                SetState(entry.Host, TypingIndicatorState.None);
        }
    }

    private bool TryGetRider(EntityUid? player, out Entity<RiderComponent> rider)
    {
        rider = default;
        if (player is not { } uid)
            return false;

        if (TryComp<RiderComponent>(uid, out var hatchling))
        {
            rider = (uid, hatchling);
            return true;
        }

        if (TryComp<RiderManifestComponent>(uid, out var manifest)
            && TryComp<RiderComponent>(manifest.Rider, out var projected))
        {
            rider = (manifest.Rider, projected);
            return true;
        }

        return false;
    }

    private void SetState(EntityUid host, TypingIndicatorState state)
    {
        if (TryComp<AppearanceComponent>(host, out var appearance))
            _appearance.SetData(host, TypingIndicatorVisuals.State, state, appearance);
    }
}
