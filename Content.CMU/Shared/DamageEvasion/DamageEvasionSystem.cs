using Content.Shared._RMC14.Evasion;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Robust.Shared.Timing;
using Content.Shared.Jittering;
using Content.Shared.Popups;

namespace Content.Shared.CMU;

public sealed partial class DamageEvasionSystem : EntitySystem
{
    [Dependency] private readonly EvasionSystem _evasion = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private readonly HashSet<EntityUid> _activeEntities = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<DamageEvasionComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<DamageEvasionComponent, EvasionRefreshModifiersEvent>(OnGetEvasion);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_activeEntities.Count == 0)
            return;

        var currentTime = _timing.CurTime;

        foreach (var uid in _activeEntities)
        {
            if (!TryComp(uid, out DamageEvasionComponent? component))
                continue;

            if (currentTime < component.EvasionExpires)
                continue;

            component.EvasionActive = false;
            _evasion.RefreshEvasionModifiers(uid);
        }

        _activeEntities.RemoveWhere(uid =>
            !TryComp(uid, out DamageEvasionComponent? component) ||
            !component.EvasionActive);
    }

    private void OnGetEvasion(
        Entity<DamageEvasionComponent> ent,
        ref EvasionRefreshModifiersEvent args)
    {
        if (!ent.Comp.EvasionActive)
            return;

        args.Evasion += ent.Comp.EvasionBonus;
    }

    private void OnDamageChanged(
        Entity<DamageEvasionComponent> ent,
        ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta == null)
            return;

        var damage = args.DamageDelta.GetTotal();

        if (damage <= 0)
            return;

        var currentTime = _timing.CurTime;

        if (currentTime - ent.Comp.DamageWindowStarted > ent.Comp.DamageWindow)
        {
            ent.Comp.DamageWindowStarted = currentTime;
            ent.Comp.AccumulatedDamage = 0;
        }

        ent.Comp.AccumulatedDamage += damage;

        if (ent.Comp.AccumulatedDamage < ent.Comp.DamageThreshold)
            return;

        if (ent.Comp.EvasionActive)
            return;

        ActivateEvasion(ent, currentTime);
    }

    private void ActivateEvasion(
        Entity<DamageEvasionComponent> ent,
        TimeSpan currentTime)
    {
        ent.Comp.AccumulatedDamage = 0;
        ent.Comp.DamageWindowStarted = currentTime;
        ent.Comp.EvasionActive = true;
        ent.Comp.EvasionExpires = currentTime + ent.Comp.EvasionDuration;

        _activeEntities.Add(ent.Owner);
        _evasion.RefreshEvasionModifiers(ent.Owner);

        _jitter.DoJitter(
            ent.Owner,
            TimeSpan.FromSeconds(1),
            true,
            80,
            8,
            true);

        _popup.PopupEntity(
            Loc.GetString("cmu-damageevasion-energy"),
            ent.Owner,
            ent.Owner,
            PopupType.MediumCaution);
    }
}
