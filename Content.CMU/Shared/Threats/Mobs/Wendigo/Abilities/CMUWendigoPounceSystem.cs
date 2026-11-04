using Content.Shared._RMC14.Xenonids.Leap;
using Content.Shared.Actions.Events;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs.Components;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.Threats.Mobs.Wendigo.Abilities;

public sealed partial class CMUWendigoPounceSystem : EntitySystem
{
    [Dependency] private CMUWendigoAllySystem _ally = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private INetManager _net = default!;

    private readonly List<string> _handIds = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUWendigoPounceComponent, XenoLeapHitEvent>(OnLeapHit);

        // XenoLeapHitAttempt is raised on the leap target, so listen on every mob and filter by the leaper.
        SubscribeLocalEvent<MobStateComponent, XenoLeapHitAttempt>(OnLeapHitAttempt);
    }

    private void OnLeapHitAttempt(Entity<MobStateComponent> target, ref XenoLeapHitAttempt args)
    {
        if (args.Cancelled || args.Leaper == target.Owner)
            return;

        if (!HasComp<CMUWendigoPounceComponent>(args.Leaper))
            return;

        if (_ally.IsWendigoAlly(args.Leaper, target))
            args.Cancelled = true;
    }

    private void OnLeapHit(Entity<CMUWendigoPounceComponent> ent, ref XenoLeapHitEvent args)
    {
        // The leap's own paralysis is server-only; keep the extra drop there too so the client never mispredicts it.
        if (_net.IsClient)
            return;

        ApplyPounceHit(ent.AsNullable(), args.Hit);
    }

    /// <summary>
    /// Drops everything the target holds, unless it is a Yautja or something cancels the disarm or the drop.
    /// Returns the number of items dropped.
    /// </summary>
    public int ApplyPounceHit(Entity<CMUWendigoPounceComponent?> wendigo, EntityUid target)
    {
        if (!Resolve(wendigo, ref wendigo.Comp, false) || !wendigo.Comp.DropHeldItems)
            return 0;

        return DropHeldItems(wendigo, target);
    }

    /// <summary>
    /// Strips every held item from <paramref name="target"/> as a disarm by <paramref name="user"/>.
    /// Yautja resist, as do targets or items that cancel the disarm attempt or the drop.
    /// </summary>
    public int DropHeldItems(EntityUid user, EntityUid target)
    {
        if (target == user || HasComp<YautjaComponent>(target))
            return 0;

        if (!TryComp(target, out HandsComponent? hands))
            return 0;

        var attempt = new DisarmAttemptEvent(target, user);
        RaiseLocalEvent(target, ref attempt);
        if (attempt.Cancelled)
            return 0;

        _handIds.Clear();
        _handIds.AddRange(hands.Hands.Keys);

        var dropped = 0;
        foreach (var handId in _handIds)
        {
            if (!_hands.TryGetHeldItem((target, hands), handId, out var held, hideVirtualItems: true))
                continue;

            var itemAttempt = new DisarmAttemptEvent(target, user, held);
            RaiseLocalEvent(held.Value, ref itemAttempt);
            if (itemAttempt.Cancelled)
                continue;

            if (_hands.TryDrop((target, hands), handId, checkActionBlocker: false))
                dropped++;
        }

        return dropped;
    }
}
