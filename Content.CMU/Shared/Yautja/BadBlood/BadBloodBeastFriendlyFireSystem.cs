using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.Yautja;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// CMU14 COUPLING-701 (docs/GraphAudit-BadBlood-Playtest5Plan.md): a clan beast's xeno abilities, xeno projectiles
/// and tackle spare the clan. RMC spares only the same hive (CanAbilityAttackTarget, the xeno projectile hive check,
/// TackleSystem); the one-line Tier C hooks there raise <see cref="XenoAbilityTargetAttemptEvent"/> on any xeno that
/// carries <see cref="XenoTargetFilterComponent"/>, which every clan beast gets here. Bad Bloods and the clan's other
/// beasts are spared; marines and other xenos are not. Shared, and the filter is networked, so the client predicts
/// the same misses the server decides.
/// </summary>
public sealed class BadBloodBeastFriendlyFireSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodBeastComponent, ComponentStartup>(OnBeastStartup);
        SubscribeLocalEvent<BadBloodBeastComponent, XenoAbilityTargetAttemptEvent>(OnTargetAttempt);
    }

    // the RMC hooks raise nothing for a xeno without the filter, so every clan beast opts in
    private void OnBeastStartup(Entity<BadBloodBeastComponent> ent, ref ComponentStartup args)
    {
        // the filter is networked: the server adds it and the client gets it with the beast
        if (_net.IsServer)
            EnsureComp<XenoTargetFilterComponent>(ent);
    }

    private void OnTargetAttempt(Entity<BadBloodBeastComponent> ent, ref XenoAbilityTargetAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (IsClan(args.Target))
            args.Cancelled = true;
    }

    /// <summary>
    /// Whether a clan beast must spare <paramref name="target"/>: a Bad Blood or another of the clan's beasts.
    /// </summary>
    public bool IsClan(EntityUid target)
    {
        return HasComp<YautjaBadBloodComponent>(target) || HasComp<BadBloodBeastComponent>(target);
    }
}
