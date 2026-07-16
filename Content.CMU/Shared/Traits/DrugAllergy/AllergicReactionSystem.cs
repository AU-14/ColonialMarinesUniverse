using Content.Shared._RMC14.Body;
using Content.Shared._RMC14.Xenonids.Screech;
using Content.Shared.Alert;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Jittering;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Traits.DrugAllergy;

public sealed partial class AllergicReactionSystem : EntitySystem
{
    private static readonly ProtoId<DamageTypePrototype> ToxinType = "Poison";
    private static readonly ProtoId<DamageTypePrototype> AsphyxiationType = "Asphyxiation";

    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private SharedRMCBloodstreamSystem _rmcBloodstream = default!;

    public void TriggerReaction(EntityUid uid)
    {
        if (!TryComp<DrugAllergyComponent>(uid, out var comp) || comp.ReactionActive)
            return;

        var time = _timing.CurTime;

        // Prevents an instant re-trigger loop if the allergen is still metabolizing
        // shortly after the reaction was just cured with naloxone/epinephrine.
        if (time < comp.NextTriggerAllowed)
            return;

        comp.ReactionActive = true;
        comp.ReactionStartTime = time;
        comp.NextTick = time;
        comp.NextStunCheck = time + comp.AsphyxiationDelay;
        comp.NextMessage = time + comp.MessageCooldown;
        comp.NextTriggerAllowed = time + comp.TriggerCooldown;
        Dirty(uid, comp);

        _alerts.ShowAlert(uid, comp.ReactionAlert);
        _popup.PopupEntity(Loc.GetString("au14-drugallergy-reaction"), uid, uid, PopupType.LargeCaution);
    }

    public void CureReaction(EntityUid uid)
    {
        if (!TryComp<DrugAllergyComponent>(uid, out var comp))
            return;

        if (comp.Allergen is { } allergen)
            _rmcBloodstream.RemoveBloodstreamChemical(uid, allergen, FixedPoint2.MaxValue);

        if (!comp.ReactionActive)
            return;

        var time = _timing.CurTime;
        comp.ReactionActive = false;
        comp.NextTriggerAllowed = time + comp.TriggerCooldown;
        Dirty(uid, comp);

        _alerts.ClearAlert(uid, comp.ReactionAlert);
        RemComp<ScreechBlindComponent>(uid);
        _popup.PopupEntity(Loc.GetString("au14-drugallergy-cured"), uid, uid, PopupType.Medium);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<DrugAllergyComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.ReactionActive)
                continue;

            var pastDelay = time - comp.ReactionStartTime >= comp.AsphyxiationDelay;

            if (time >= comp.NextTick)
            {
                comp.NextTick = time + comp.TimeBetweenTicks;
                Dirty(uid, comp);

                // Dims vision for as long as the reaction is active; refreshed each tick so it
                // doesn't lapse between ticks, and removed instantly on cure.
                var blind = EnsureComp<ScreechBlindComponent>(uid);
                blind.Radius = comp.VisionDimRadius;
                blind.EndsAt = time + comp.TimeBetweenTicks + TimeSpan.FromSeconds(2);
                Dirty(uid, blind);

                var damage = new DamageSpecifier();
                damage.DamageDict[ToxinType] = comp.ToxinDamagePerTick;
                if (pastDelay)
                    damage.DamageDict[AsphyxiationType] = comp.AsphyxiationDamagePerTick;

                _damageable.TryChangeDamage(uid, damage, interruptsDoAfters: false);

                if (time >= comp.NextMessage)
                {
                    comp.NextMessage = time + comp.MessageCooldown;
                    _popup.PopupEntity(
                        Loc.GetString(pastDelay ? "au14-drugallergy-suffering" : "au14-drugallergy-building"),
                        uid, uid, PopupType.MediumCaution);
                }
            }

            if (pastDelay && time >= comp.NextStunCheck)
            {
                comp.NextStunCheck = time + TimeSpan.FromSeconds(
                    _random.NextFloat((float)comp.StunIntervalMin.TotalSeconds, (float)comp.StunIntervalMax.TotalSeconds));
                Dirty(uid, comp);

                var stunTime = TimeSpan.FromSeconds(_random.NextFloat(comp.StunMinDuration, comp.StunMaxDuration));
                _stun.TryStun(uid, stunTime, true);
                _jitter.DoJitter(uid, stunTime, true, 8, 5);
                _popup.PopupEntity(Loc.GetString("au14-drugallergy-stun"), uid, uid, PopupType.LargeCaution);
            }
        }
    }
}
