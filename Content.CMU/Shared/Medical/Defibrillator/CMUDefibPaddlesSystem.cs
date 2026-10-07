using Content.Shared.Medical;

namespace Content.Shared.CMU14.Medical.Defibrillator;

/// <summary>
/// Takes the paddles off the unit when it starts charging and docks them again once the shock is delivered or the
/// charge is abandoned.
/// </summary>
public sealed class CMUDefibPaddlesSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUDefibPaddlesComponent, CMUDefibZapStartedEvent>(OnZapStarted);
        SubscribeLocalEvent<CMUDefibPaddlesComponent, DefibrillatorZapDoAfterEvent>(OnZapDoAfter);
    }

    private void OnZapStarted(Entity<CMUDefibPaddlesComponent> ent, ref CMUDefibZapStartedEvent args)
    {
        SetPaddlesOut(ent, true);
    }

    private void OnZapDoAfter(Entity<CMUDefibPaddlesComponent> ent, ref DefibrillatorZapDoAfterEvent args)
    {
        // Shocked or interrupted, the paddles go back on the unit.
        SetPaddlesOut(ent, false);
    }

    public void SetPaddlesOut(EntityUid defib, bool paddlesOut)
    {
        if (HasComp<CMUDefibPaddlesComponent>(defib))
            _appearance.SetData(defib, CMUDefibVisuals.PaddlesOut, paddlesOut);
    }
}
