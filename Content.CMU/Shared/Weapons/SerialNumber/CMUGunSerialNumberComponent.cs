using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Weapons.SerialNumber;

/// <summary>
///     The serial number stamped on a gun, given to every handheld gun when it spawns and shown in its own examine
///     window.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUGunSerialNumberComponent : Component
{
    [DataField, AutoNetworkedField]
    public string Serial = string.Empty;

    /// <summary>
    ///     The marks this gun's firing pin and ejector leave on every casing it fires. A forensic scanner reads it off
    ///     the gun and its casings, and it stays even once the serial number is scratched off.
    /// </summary>
    [DataField]
    public string Signature = string.Empty;

    /// <summary>Someone scratched or burned the number off, so it can't be read any more.</summary>
    [DataField, AutoNetworkedField]
    public bool Scratched;

    /// <summary>How long it takes to scratch the number off with a blade.</summary>
    [DataField]
    public TimeSpan ScratchDelay = TimeSpan.FromSeconds(8);

    /// <summary>How long it takes to burn the number off with a lit blowtorch.</summary>
    [DataField]
    public TimeSpan WeldDelay = TimeSpan.FromSeconds(4);
}

[Serializable, NetSerializable]
public sealed partial class CMUScratchSerialDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
///     Firing-pin marks on a spent casing, from the gun that fired it. Holding the casing up to a records console looks
///     the gun up in the colony firearm registry.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUBallisticMarksComponent : Component
{
    [DataField]
    public string Signature = string.Empty;
}
