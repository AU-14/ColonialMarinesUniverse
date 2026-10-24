using Content.Shared.CMU14.Hygiene;
using Content.Shared.Damage.Systems;

namespace Content.Server.CMU14.Hygiene;

public sealed partial class CMUHygieneSystem
{
    [Dependency] private DamageableSystem _damageable = default!;

    private void OnLaundryDamage(Entity<CMULaundryComponent> ent, ref DamageChangedEvent args)
    {
        RefreshLaundryDamage(ent, announce: true);
    }

    private void RefreshLaundryDamage(Entity<CMULaundryComponent> ent, bool announce)
    {
        var wasBroken = ent.Comp.Broken;
        ent.Comp.Broken = _damageable.GetTotalDamage(ent.Owner) >= ent.Comp.BreakdownDamage;
        _appearance.SetData(ent.Owner, CMUHygieneVisuals.Broken, ent.Comp.Broken);
        _appearance.SetData(ent.Owner, CMUHygieneVisuals.Powered, HasLaundryPower(ent.Owner) && !ent.Comp.Broken);

        if (ent.Comp.Broken)
            StopLaundry(ent);

        if (announce && wasBroken != ent.Comp.Broken)
            _popup.PopupEntity(Loc.GetString(ent.Comp.Broken ? "cmu-laundry-breakdown" : "cmu-laundry-repaired"), ent);
    }
}
