using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// Who holds the lethal bolt and the eradicator. While the Bad Blood rule runs, the lethal bolt: nobody, then every
/// Bad Blood once the armory has sold the clan's lethal unlock or the clan ship has arrived. The eradicator: nobody,
/// then the clan's Leader once the armory has sold that unlock (which needs the lethal one first), then every Bad
/// Blood once the clan ship has arrived (tier IV). Without a rule every Bad Blood holds both, as before the Lodge
/// loop existed. The gate itself is the shared <see cref="BadBloodEradicatorSystem"/>.
/// </summary>
public sealed partial class BadBloodEradicatorUnlockSystem : EntitySystem
{
    private static readonly TimeSpan UpdateEvery = TimeSpan.FromSeconds(1);

    [Dependency] private YautjaCasterSystem _caster = default!;
    [Dependency] private BadBloodCampRuleSystem _rule = default!;
    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan _nextUpdate;
    private readonly List<EntityUid> _scratch = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaBadBloodComponent, YautjaCasterFiredEvent>(OnCasterFired);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + UpdateEvery;
        SyncHolders();
    }

    /// <summary>
    /// The clan has bought the eradicator this round.
    /// </summary>
    public bool IsEradicatorUnlocked()
    {
        return _rule.TryGetRule(out var rule) &&
               TryComp(rule.Owner, out BadBloodArmoryUnlocksComponent? unlocks) &&
               unlocks.Eradicator;
    }

    /// <summary>
    /// The clan has bought the lethal bolt this round.
    /// </summary>
    public bool IsLethalUnlocked()
    {
        return _rule.TryGetRule(out var rule) &&
               TryComp(rule.Owner, out BadBloodArmoryUnlocksComponent? unlocks) &&
               unlocks.Lethal;
    }

    /// <summary>
    /// Unlocks the lethal bolt for the whole clan, once per round. Squads that arrive later hold it too.
    /// False without a rule or when it is already unlocked.
    /// </summary>
    public bool UnlockLethal()
    {
        if (!_rule.TryGetRule(out var rule))
            return false;

        var unlocks = EnsureComp<BadBloodArmoryUnlocksComponent>(rule.Owner);
        if (unlocks.Lethal)
            return false;

        unlocks.Lethal = true;
        SyncHolders();
        return true;
    }

    /// <summary>
    /// Whether <paramref name="uid"/> should hold the lethal bolt right now.
    /// </summary>
    public bool ShouldHoldLethal(EntityUid uid)
    {
        if (!HasComp<YautjaBadBloodComponent>(uid))
            return false;

        if (!_rule.TryGetRule(out var rule) || rule.Comp.TierFourUnlocked)
            return true;

        return IsLethalUnlocked();
    }

    /// <summary>
    /// Unlocks the eradicator for the clan, once per round. The Leader holds it from then on, whoever that is.
    /// False without a rule, before the lethal bolt is unlocked, or when it is already unlocked.
    /// </summary>
    public bool UnlockEradicator()
    {
        if (!_rule.TryGetRule(out var rule))
            return false;

        var unlocks = EnsureComp<BadBloodArmoryUnlocksComponent>(rule.Owner);
        if (unlocks.Eradicator || !unlocks.Lethal)
            return false;

        unlocks.Eradicator = true;
        SyncHolders();
        return true;
    }

    /// <summary>
    /// Whether <paramref name="uid"/> should hold the eradicator right now.
    /// </summary>
    public bool ShouldHoldEradicator(EntityUid uid)
    {
        if (!HasComp<YautjaBadBloodComponent>(uid))
            return false;

        if (!_rule.TryGetRule(out var rule) || rule.Comp.TierFourUnlocked)
            return true;

        return IsEradicatorUnlocked() && _rule.GetLeader() == uid;
    }

    /// <summary>
    /// Gives and takes <see cref="BadBloodEradicatorComponent"/> and <see cref="BadBloodLethalBoltComponent"/> so
    /// they match <see cref="ShouldHoldEradicator"/> and <see cref="ShouldHoldLethal"/>, then drops any caster a
    /// Bad Blood carries in a mode they do not hold back to the stun bolt. Runs once a second by itself.
    /// </summary>
    public void SyncHolders()
    {
        _scratch.Clear();
        var lethalHolders = EntityQueryEnumerator<BadBloodLethalBoltComponent>();
        while (lethalHolders.MoveNext(out var uid, out _))
        {
            if (!ShouldHoldLethal(uid))
                _scratch.Add(uid);
        }

        foreach (var uid in _scratch)
        {
            RemComp<BadBloodLethalBoltComponent>(uid);
        }

        _scratch.Clear();
        var holders = EntityQueryEnumerator<BadBloodEradicatorComponent>();
        while (holders.MoveNext(out var uid, out _))
        {
            if (!ShouldHoldEradicator(uid))
                _scratch.Add(uid);
        }

        foreach (var uid in _scratch)
        {
            RemComp<BadBloodEradicatorComponent>(uid);
        }

        var badBloods = EntityQueryEnumerator<YautjaBadBloodComponent>();
        while (badBloods.MoveNext(out var uid, out _))
        {
            if (ShouldHoldEradicator(uid))
                EnsureComp<BadBloodEradicatorComponent>(uid);

            if (ShouldHoldLethal(uid))
                EnsureComp<BadBloodLethalBoltComponent>(uid);
        }

        // without a rule nothing is locked, so there is nothing to drop
        if (_rule.IsRuleActive())
            DropLockedCasters();
    }

    // a caster can be left in a mode its carrier has no right to: picked up from another Bad Blood, or carried by
    // a Leader who was deposed. it goes back to the stun bolt. a caster nobody carries is left alone
    private void DropLockedCasters()
    {
        var casters = EntityQueryEnumerator<YautjaCasterComponent>();
        while (casters.MoveNext(out var uid, out var caster))
        {
#pragma warning disable RA0002 // read-only lookup of the caster's mode
            var mode = caster.CurrentMode;
#pragma warning restore RA0002
            if (mode < BadBloodEradicatorSystem.LethalMode || !TryGetBadBloodCarrier(uid, out var carrier))
                continue;

            if (HasComp<BadBloodLethalBoltComponent>(carrier) &&
                (mode != BadBloodEradicatorSystem.EradicatorMode || HasComp<BadBloodEradicatorComponent>(carrier)))
            {
                continue;
            }

            _caster.ForceMode((uid, caster), BadBloodEradicatorSystem.StunMode);
        }
    }

    // the caster sits in a hand, or in the bracer or pack its carrier wears
    private bool TryGetBadBloodCarrier(EntityUid caster, out EntityUid carrier)
    {
        carrier = Transform(caster).ParentUid;
        for (var i = 0; i < 4 && carrier.IsValid(); i++)
        {
            if (HasComp<YautjaBadBloodComponent>(carrier))
                return true;

            carrier = Transform(carrier).ParentUid;
        }

        carrier = default;
        return false;
    }

    // one eradicator shot, then the caster drops back to the lethal bolt and the eradicator rests
    private void OnCasterFired(Entity<YautjaBadBloodComponent> ent, ref YautjaCasterFiredEvent args)
    {
        if (args.Mode != BadBloodEradicatorSystem.EradicatorMode || !_rule.IsRuleActive())
            return;

        _caster.ForceMode(args.Caster, BadBloodEradicatorSystem.LethalMode);

        if (!TryComp(ent.Owner, out BadBloodEradicatorComponent? eradicator))
            return;

        eradicator.NextUseAt = _timing.CurTime + eradicator.Cooldown;
        Dirty(ent.Owner, eradicator);
    }
}
