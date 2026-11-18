using Content.Shared.CMU14.Yautja;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Yautja.BadBlood;

// the gates on the Bad Blood caster: the lethal bolt and the eradicator mode. the immobilizer is always there, and
// the stun bolt for everyone but a Berserker (CMU14 ARCH-605), the immobilizer for everyone but a Medic (ECS-702)
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

    /// <summary>
    /// The immobilizer, where a Berserker's caster falls back to from the stun bolt.
    /// </summary>
    public const int ImmobilizerMode = 1;

    [Dependency] private YautjaCasterSystem _caster = default!;
    [Dependency] private INetManager _net = default!;
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
        // CMU14 ARCH-605: a Berserker carries no stun bolt. use in hand steps past it (a silent ask). a caster left
        // in it (its first shot, a mode forced back to it, or the lethal toggle back to it) falls back to the
        // immobilizer, so the Berserker is never stuck on a mode it cannot fire
        if (!args.Cancelled && args.Mode == StunMode && HasComp<BadBloodBerserkerComponent>(ent.Owner))
        {
            args.Cancelled = true;
            if (args.Silent)
                return;

            _popup.PopupClient(Loc.GetString("cmu-bad-blood-berserker-no-stun"), ent.Owner, ent.Owner, PopupType.SmallCaution);
            if (_net.IsServer &&
                TryComp(args.Caster, out YautjaCasterComponent? caster) &&
                caster.Modes.Count > ImmobilizerMode)
            {
                _caster.ForceMode((args.Caster, caster), ImmobilizerMode);
            }

            return;
        }

        // CMU14 ECS-702 (D17): a Medic carries no immobilizer, the price of the field kit. refused the same way as
        // the Berserker's stun bolt, falling back to the stun bolt
        if (!args.Cancelled && args.Mode == ImmobilizerMode && HasComp<BadBloodFieldMendComponent>(ent.Owner))
        {
            args.Cancelled = true;
            if (args.Silent)
                return;

            _popup.PopupClient(Loc.GetString("cmu-bad-blood-medic-no-immobilizer"), ent.Owner, ent.Owner, PopupType.SmallCaution);
            if (_net.IsServer &&
                TryComp(args.Caster, out YautjaCasterComponent? medicCaster) &&
                medicCaster.Modes.Count > StunMode)
            {
                _caster.ForceMode((args.Caster, medicCaster), StunMode);
            }

            return;
        }

        if (args.Cancelled || args.Mode < LethalMode)
            return;

        // the eradicator sits behind the lethal bolt, so this refuses both
        if (!HasComp<BadBloodLethalBoltComponent>(ent.Owner))
        {
            args.Cancelled = true;
            if (!args.Silent)
                _popup.PopupClient(Loc.GetString("cmu-bad-blood-lethal-locked"), ent.Owner, ent.Owner, PopupType.SmallCaution);

            return;
        }

        if (args.Mode != EradicatorMode)
            return;

        if (!TryComp(ent.Owner, out BadBloodEradicatorComponent? eradicator))
        {
            args.Cancelled = true;
            if (!args.Silent)
                _popup.PopupClient(Loc.GetString("cmu-bad-blood-eradicator-locked"), ent.Owner, ent.Owner, PopupType.SmallCaution);

            return;
        }

        var now = _timing.CurTime;
        if (now >= eradicator.NextUseAt)
            return;

        args.Cancelled = true;
        if (args.Silent)
            return;

        var seconds = (int) Math.Ceiling((eradicator.NextUseAt - now).TotalSeconds);
        _popup.PopupClient(Loc.GetString("cmu-bad-blood-eradicator-cooldown", ("seconds", seconds)), ent.Owner, ent.Owner, PopupType.SmallCaution);
    }
}
