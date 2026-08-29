using Content.Shared.Body.Events;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Traits;

public sealed partial class AnemicSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnemicComponent, BleedModifierEvent>(
            OnBleedModifier,
            after: [typeof(StatusEffectsSystem)]);
    }

    private void OnBleedModifier(Entity<AnemicComponent> ent, ref BleedModifierEvent args)
    {
        args.BleedAmount *= ent.Comp.BleedRateMultiplier;

        if (_timing.CurTime < ent.Comp.NextWarnMessage)
            return;

        ent.Comp.NextWarnMessage = _timing.CurTime + ent.Comp.WarnCooldown;
        _popup.PopupEntity(Loc.GetString("au14-anemic-bleeding"), ent, ent, PopupType.MediumCaution);
    }
}
