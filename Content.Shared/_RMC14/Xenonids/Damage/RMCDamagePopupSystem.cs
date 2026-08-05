using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Robust.Shared.Network;

namespace Content.Shared._RMC14.Xenonids.Damage;

public sealed partial class RMCDamagePopupSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<DamagePopupComponent, ProjectileDamageDealtEvent>(OnDamagePopup);
    }

    private void OnDamagePopup(Entity<DamagePopupComponent> ent, ref ProjectileDamageDealtEvent args)
    {
        if (!TryComp(ent, out DamageableComponent? damageable))
            return;

        var totalDamage = _damageable.GetTotalDamage((ent.Owner, damageable));
        ShowClientDamagePopup(
            ent,
            totalDamage,
            ent.Comp.Type,
            args.Origin,
            args.DamageDelta,
            args.AuthoritativeTotal);
    }

    private void ShowClientDamagePopup(
        EntityUid target,
        FixedPoint2 damageTotal,
        DamagePopupType type,
        EntityUid? origin,
        DamageSpecifier? damageDelta,
        FixedPoint2? authoritativeTotal)
    {
        if (damageDelta == null)
            return;

        var delta = damageDelta.GetTotal();
        // Predicted client damage has not changed the networked total yet. Direct
        // authoritative feedback carries the post-hit total so state/event order
        // cannot double-count the delta.
        var total = authoritativeTotal ??
                    damageTotal + (_net.IsClient ? delta : FixedPoint2.Zero);
        var msg = type switch
        {
            DamagePopupType.Delta => delta.ToString(),
            DamagePopupType.Total => total.ToString(),
            DamagePopupType.Combined => delta + " | " + total,
            DamagePopupType.Hit => "!",
            _ => "Invalid type",
        };
        _popupSystem.PopupClient(msg, target, origin);
    }
}
