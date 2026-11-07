using Content.Shared._RMC14.Xenonids.Leap;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Random.Helpers;
using Content.Shared.Stunnable;
using Content.Shared._RMC14.Barricade;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Yautja;

// cmss13 XenoProcs.dm handle_xeno_leap_collision: a pounce into a yautja's front gets body slammed 75% of the time,
// KnockDown(3) + Stun(3). runs after shields so a raised shield still gets the bonk first, same order as cm
public sealed partial class YautjaPounceBodySlamSystem : EntitySystem
{
    private const float BodySlamChance = 0.75f;
    // 3 cm status units, 2s each. xenos get their 2/3 off in RMCStatusEffectSystem
    private static readonly TimeSpan BodySlamStun = TimeSpan.FromSeconds(6);

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedDirectionalAttackBlockSystem _directionalBlock = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaComponent, XenoLeapHitAttempt>(OnLeapHitAttempt, after: [typeof(XenoLeapSystem)]);
    }

    private void OnLeapHitAttempt(Entity<YautjaComponent> ent, ref XenoLeapHitAttempt args)
    {
        if (args.Cancelled ||
            HasComp<YautjaBadBloodComponent>(ent) ||
            _mobState.IsIncapacitated(ent) ||
            !TryComp(args.Leaper, out XenoLeapingComponent? leaping) ||
            TryComp(args.Leaper, out XenoLeapComponent? leap) && !leap.CanBeShieldBlocked ||
            !_directionalBlock.IsFacingTarget(ent, args.Leaper, leaping.Origin))
        {
            return;
        }

        if (!SharedRandomExtensions.PredictedProb(_timing, BodySlamChance, GetNetEntity(ent), GetNetEntity(args.Leaper)))
            return;

        args.Cancelled = true;
        _stun.TryParalyze(args.Leaper, BodySlamStun, true);
        _popup.PopupPredicted(
            Loc.GetString("cmu-yautja-pounce-body-slam-self", ("yautja", ent.Owner)),
            Loc.GetString("cmu-yautja-pounce-body-slam-others", ("yautja", ent.Owner), ("xeno", args.Leaper)),
            args.Leaper,
            args.Leaper,
            PopupType.MediumCaution);
    }
}
