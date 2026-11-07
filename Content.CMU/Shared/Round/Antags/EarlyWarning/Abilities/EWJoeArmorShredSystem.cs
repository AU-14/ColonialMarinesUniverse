using Content.Shared._RMC14.Armor;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// Temporary xeno-armor shred (W7 2A Chitin Crack, Desecrate on xenofauna). It works through
/// <see cref="CMGetArmorEvent"/>, the same event the Joe's own Sculpted Form armor uses, and runs after
/// RMC's armor so it can clamp at zero instead of driving armor negative.
/// </summary>
public sealed class EWJoeArmorShredSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeArmorShredComponent, CMGetArmorEvent>(OnGetArmor, after: [typeof(CMArmorSystem)]);
    }

    private void OnGetArmor(Entity<EWJoeArmorShredComponent> ent, ref CMGetArmorEvent args)
    {
        if (_timing.CurTime >= ent.Comp.ExpiresAt)
            return;

        args.XenoArmor = Math.Max(0, args.XenoArmor - ent.Comp.Armor);
    }

    /// <summary>
    /// Takes <paramref name="armor"/> off <paramref name="target"/>'s xeno armor for
    /// <paramref name="duration"/>. A second shred does not stack: the larger amount and the later
    /// expiry win.
    /// </summary>
    public void Shred(EntityUid target, int armor, TimeSpan duration)
    {
        if (armor <= 0 || duration <= TimeSpan.Zero)
            return;

        var expires = _timing.CurTime + duration;
        var shred = EnsureComp<EWJoeArmorShredComponent>(target);
        var active = _timing.CurTime < shred.ExpiresAt;
        shred.Armor = active ? Math.Max(shred.Armor, armor) : armor;
        if (!active || expires > shred.ExpiresAt)
            shred.ExpiresAt = expires;
        Dirty(target, shred);
    }

    /// <summary>The armor currently shredded off <paramref name="target"/>, zero when none or expired.</summary>
    public int GetShred(EntityUid target)
    {
        return TryComp(target, out EWJoeArmorShredComponent? shred) && _timing.CurTime < shred.ExpiresAt
            ? shred.Armor
            : 0;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<EWJoeArmorShredComponent>();
        while (query.MoveNext(out var uid, out var shred))
        {
            if (now >= shred.ExpiresAt)
                RemCompDeferred<EWJoeArmorShredComponent>(uid);
        }
    }
}
