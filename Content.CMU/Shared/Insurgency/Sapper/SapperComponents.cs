using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._AU14.Insurgency.Sapper;

/// <summary>
///     Marks a mob as a trained CLF Sapper. Only mobs with this component know how to plant sapper traps
///     (and use the other sapper-only fieldcraft); anyone else fumbling with a trap is told they don't know
///     how to set it up. Granted by the AU14JobCLFSapper job's round components.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SapperComponent : Component
{
}

/// <summary>
///     The sapper's siphon rig (built at the Sapper's Workbench). Using it on a colony ATM runs a
///     long hack; when it lands, the ATM spits out an equal share of the colony budget in cash - the
///     budget divided by however many un-hacked ATMs are left on the map, so ten ATMs pay ten equal
///     slices - and the ATM is left temporarily malfunctioning (see <see cref="SapperAtmHackedComponent"/>).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SapperAtmHackingComponent : Component
{
    // ---------------------------------------------------------------------
    // Tunables.
    // ---------------------------------------------------------------------

    /// <summary>How long the hack takes on the big finance devices (budget console, ASRS). Long on
    /// purpose: it's a vulnerable, committed action.</summary>
    [DataField]
    public float HackTime = 60f;

    /// <summary>How long the hack takes on a plain ATM. Shorter than <see cref="HackTime"/> since an ATM
    /// pays out far less than the console/ASRS drains.</summary>
    [DataField]
    public float AtmHackTime = 30f;

    /// <summary>Played at the ATM when the hack starts, so the theft isn't silent.</summary>
    [DataField]
    public SoundSpecifier? StartSound = new SoundPathSpecifier("/Audio/Machines/circuitprinter.ogg");

    /// <summary>Played at the ATM when the cash comes out.</summary>
    [DataField]
    public SoundSpecifier? SuccessSound = new SoundPathSpecifier("/Audio/Machines/beep.ogg");

    /// <summary>What the payout is dispensed as.</summary>
    [DataField]
    public string CashPrototype = "RMCSpaceCash";
}

/// <summary>
///     A colony ATM in the temporary disrupted state after a siphon. While disrupted it will not open
///     (swiping gets a red malfunction message and a buzz) and it throws visible sparks every few seconds
///     so anyone can see it is in an abnormal state. It repairs itself once <see cref="RecoverAt"/> passes.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SapperAtmHackedComponent : Component
{
    /// <summary>How long the disruption lasts before the machine restores itself.</summary>
    [DataField]
    public TimeSpan RecoverDelay = TimeSpan.FromMinutes(5);

    /// <summary>Seconds between spark bursts while disrupted.</summary>
    [DataField]
    public float SparkInterval = 3f;

    /// <summary>The buzz played when someone tries to use the disrupted machine.</summary>
    [DataField]
    public SoundSpecifier BuzzSound = new SoundPathSpecifier("/Audio/Machines/buzz-two.ogg");

    /// <summary>The sparks thrown while disrupted.</summary>
    [DataField]
    public EntProtoId SparkEffect = "EffectSparks";

    [ViewVariables]
    public TimeSpan RecoverAt;

    [ViewVariables]
    public TimeSpan NextSpark;
}

/// <summary>
///     Lives on a finance device only while a siphon do-after is running against it. Makes the machine
///     throw sparks for the whole duration of the hack, so the counterplay window is visible as well as
///     audible. Removed when the do-after finishes or is cancelled.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SapperAtmSiphoningComponent : Component
{
    /// <summary>Seconds between spark bursts while being siphoned.</summary>
    [DataField]
    public float SparkInterval = 1.5f;

    /// <summary>The sparks thrown while being siphoned.</summary>
    [DataField]
    public EntProtoId SparkEffect = "EffectSparks";

    [ViewVariables]
    public TimeSpan NextSpark;
}

/// <summary>DoAfter for hacking an ATM with the siphon rig.</summary>
[Serializable, NetSerializable]
public sealed partial class SapperAtmHackDoAfterEvent : SimpleDoAfterEvent
{
}
