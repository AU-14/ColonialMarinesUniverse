using Content.Shared.CMU14.Yautja;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;

namespace Content.Server.CMU14.Yautja;

public sealed partial class YautjaVitalsWarningSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _mobThresholds = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaVitalsWarningComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(Entity<YautjaVitalsWarningComponent> ent, ref DamageChangedEvent args)
    {
        if (!_mobThresholds.TryGetThresholdForState(ent, MobState.Critical, out var crit))
            return;

        var damage = _damageable.GetTotalDamage((ent, args.Damageable));
        if (damage < crit.Value * ent.Comp.RearmFraction)
        {
            ent.Comp.Warned = false;
            return;
        }

        // already down, the crit overlay is the warning now
        if (ent.Comp.Warned || damage < crit.Value * ent.Comp.WarnFraction || !_mobState.IsAlive(ent))
            return;

        ent.Comp.Warned = true;
        _popup.PopupEntity(Loc.GetString("cmu-yautja-vitals-failing"), ent, ent, PopupType.LargeCaution);
        _audio.PlayGlobal(ent.Comp.Sound, ent);
    }
}
