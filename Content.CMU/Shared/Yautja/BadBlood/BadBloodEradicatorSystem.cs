using Content.Shared.CMU14.Yautja;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Yautja.BadBlood;

// the gates on the Bad Blood caster: the lethal bolt and the eradicator mode. stun and immobilizer are always there
public sealed partial class BadBloodEradicatorSystem : EntitySystem
{
    /// <summary>
    /// The caster mode the eradicator sits in, on every caster that has one.
    /// </summary>
    public const int EradicatorMode = 3;

    /// <summary>
    /// The lethal bolt, where a caster goes back to after an eradicator shot.
    /// </summary>
    public const int LethalMode = 2;

    /// <summary>
    /// The stun bolt, where a caster drops back to when it is left in a mode its carrier has not earned.
    /// </summary>
    public const int StunMode = 0;

    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaBadBloodComponent, YautjaCasterModeAttemptEvent>(OnCasterModeAttempt);
    }

    /// <summary>
    /// Whether <paramref name="uid"/> could switch a caster to the eradicator right now. Always true for anyone
    /// who is not a Bad Blood.
    /// </summary>
    public bool CanUseEradicator(EntityUid uid)
    {
        if (!HasComp<YautjaBadBloodComponent>(uid))
            return true;

        return TryComp(uid, out BadBloodEradicatorComponent? eradicator) && _timing.CurTime >= eradicator.NextUseAt;
    }

    /// <summary>
    /// Whether <paramref name="uid"/> could switch a caster to the lethal bolt right now. Always true for anyone
    /// who is not a Bad Blood.
    /// </summary>
    public bool CanUseLethal(EntityUid uid)
    {
        return !HasComp<YautjaBadBloodComponent>(uid) || HasComp<BadBloodLethalBoltComponent>(uid);
    }

    private void OnCasterModeAttempt(Entity<YautjaBadBloodComponent> ent, ref YautjaCasterModeAttemptEvent args)
    {
        if (args.Cancelled || args.Mode < LethalMode)
            return;

        // the eradicator sits behind the lethal bolt, so this refuses both
        if (!HasComp<BadBloodLethalBoltComponent>(ent.Owner))
        {
            args.Cancelled = true;
            _popup.PopupClient(Loc.GetString("cmu-bad-blood-lethal-locked"), ent.Owner, ent.Owner, PopupType.SmallCaution);
            return;
        }

        if (args.Mode != EradicatorMode)
            return;

        if (!TryComp(ent.Owner, out BadBloodEradicatorComponent? eradicator))
        {
            args.Cancelled = true;
            _popup.PopupClient(Loc.GetString("cmu-bad-blood-eradicator-locked"), ent.Owner, ent.Owner, PopupType.SmallCaution);
            return;
        }

        var now = _timing.CurTime;
        if (now >= eradicator.NextUseAt)
            return;

        args.Cancelled = true;
        var seconds = (int) Math.Ceiling((eradicator.NextUseAt - now).TotalSeconds);
        _popup.PopupClient(Loc.GetString("cmu-bad-blood-eradicator-cooldown", ("seconds", seconds)), ent.Owner, ent.Owner, PopupType.SmallCaution);
    }
}
