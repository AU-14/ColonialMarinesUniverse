using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Medical.Defibrillator;

/// <summary>
/// The energy, in joules, a defibrillator delivers. 150 J is the stock behaviour; the component only exists on
/// manual defibrillators once someone changes it. AEDs set <see cref="Fixed"/> and choose their own energy.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUDefibChargeSystem))]
public sealed partial class CMUDefibChargeComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Joules = CMUDefibChargeSystem.DefaultJoules;

    /// <summary>
    /// Automated defibrillators can't be tuned by hand.
    /// </summary>
    [DataField]
    public bool Fixed;
}
