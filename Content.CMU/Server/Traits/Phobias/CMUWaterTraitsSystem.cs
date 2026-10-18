using Content.Shared._RMC14.Emote;
using Content.Shared._RMC14.Water;
using Content.Shared.Chat.Prototypes;
using Content.Shared.CMU14.Traits.Phobias;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.IdentityManagement;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Traits.Phobias;

/// <summary>
///     Fear of water makes you dizzy in water. Can't swim slows you in water, more in deep water, and once you've been
///     in water over your head for a few seconds you start flailing and drowning.
/// </summary>
public sealed class CMUWaterTraitsSystem : EntitySystem
{
    private static readonly EntProtoId Woozy = "StatusEffectWoozy";
    private static readonly ProtoId<DamageTypePrototype> Asphyxiation = "Asphyxiation";
    private static readonly ProtoId<EmotePrototype> Gasp = "Gasp";
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan DrownInterval = TimeSpan.FromSeconds(1);
    private const int FearMessages = 3;
    private const int FlailMessages = 3;

    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedRMCEmoteSystem _emote = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private CMUCantSwimSpeedSystem _swimSpeed = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private RMCWaterSystem _water = default!;

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;

        var fears = EntityQueryEnumerator<CMUFearOfWaterComponent>();
        while (fears.MoveNext(out var uid, out var fear))
        {
            if (now < fear.NextCheck)
                continue;

            fear.NextCheck = now + CheckInterval;
            var scared = !_mobState.IsIncapacitated(uid) && WaterDepth(uid) > 0;
            if (scared)
            {
                _status.TryUpdateStatusEffectDuration(uid, Woozy, fear.Dizziness);
                if (!fear.WasScared || now >= fear.NextMessage)
                {
                    fear.NextMessage = now + fear.MessageCooldown;
                    var message = Loc.GetString($"cmu-fear-of-water-scared-{_random.Next(1, FearMessages + 1)}");
                    _popup.PopupEntity(message, uid, uid, PopupType.MediumCaution);
                }
            }
            else if (fear.WasScared)
            {
                _popup.PopupEntity(Loc.GetString("cmu-fear-of-water-calm"), uid, uid);
            }

            fear.WasScared = scared;
        }

        var swimmers = EntityQueryEnumerator<CMUCantSwimComponent>();
        while (swimmers.MoveNext(out var uid, out var swim))
        {
            if (now >= swim.NextCheck)
            {
                swim.NextCheck = now + CheckInterval;
                UpdateWater((uid, swim), now);
            }

            if (swim.Drowning && now >= swim.NextDrown)
                Drown((uid, swim), now);
        }
    }

    private float WaterDepth(EntityUid uid)
    {
        if (!_water.CanSubmerge(uid) ||
            !_water.TryGetWaterSurface(uid, out _, out var depth, out var covered) ||
            covered)
        {
            return 0;
        }

        return depth;
    }

    private void UpdateWater(Entity<CMUCantSwimComponent> ent, TimeSpan now)
    {
        var depth = WaterDepth(ent);
        var water = depth <= 0 ? CMUSwimWater.None :
            depth >= ent.Comp.DeepDepth ? CMUSwimWater.Deep :
            CMUSwimWater.Shallow;

        var previous = ent.Comp.Water;
        _swimSpeed.SetWater(ent, water);

        if (water == CMUSwimWater.Deep)
        {
            if (previous != CMUSwimWater.Deep)
            {
                ent.Comp.DeepSince = now;
                _popup.PopupEntity(Loc.GetString("cmu-cant-swim-deep"), ent, ent, PopupType.LargeCaution);
            }
            else if (!ent.Comp.Drowning && now >= ent.Comp.DeepSince + ent.Comp.DrownAfter &&
                     !_mobState.IsDead(ent))
            {
                ent.Comp.Drowning = true;
                ent.Comp.NextDrown = now;
                ent.Comp.NextFlailMessage = now;
                _popup.PopupEntity(Loc.GetString("cmu-cant-swim-going-under"), ent, ent, PopupType.LargeCaution);
            }

            return;
        }

        if (water == CMUSwimWater.Shallow && previous == CMUSwimWater.None)
            _popup.PopupEntity(Loc.GetString("cmu-cant-swim-shallow"), ent, ent, PopupType.SmallCaution);

        if (ent.Comp.Drowning)
        {
            ent.Comp.Drowning = false;
            _popup.PopupEntity(Loc.GetString("cmu-cant-swim-out"), ent, ent, PopupType.Medium);
        }
    }

    private void Drown(Entity<CMUCantSwimComponent> ent, TimeSpan now)
    {
        ent.Comp.NextDrown = now + DrownInterval;
        if (_mobState.IsDead(ent))
            return;

        var damage = new DamageSpecifier();
        damage.DamageDict[Asphyxiation] = ent.Comp.DrownDamage;
        _damageable.TryChangeDamage(ent.Owner, damage, interruptsDoAfters: false);
        _jitter.DoJitter(ent, DrownInterval * 2, true, 6, 4);

        if (now < ent.Comp.NextFlailMessage)
            return;

        ent.Comp.NextFlailMessage = now + ent.Comp.FlailMessageCooldown;
        var n = _random.Next(1, FlailMessages + 1);
        _popup.PopupEntity(Loc.GetString($"cmu-cant-swim-drowning-self-{n}"), ent, ent, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString($"cmu-cant-swim-drowning-others-{n}", ("target", Identity.Entity(ent, EntityManager))),
            ent,
            Filter.PvsExcept(ent),
            true,
            PopupType.MediumCaution);

        if (!_mobState.IsIncapacitated(ent))
            _emote.TryEmoteWithChat(ent, Gasp, forceEmote: true, cooldown: TimeSpan.Zero);
    }
}
