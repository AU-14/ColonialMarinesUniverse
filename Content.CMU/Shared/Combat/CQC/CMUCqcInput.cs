using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Combat.CQC;

/// <summary>
/// One close-quarters input the CQC sequence buffer records (and that owners such as the Early Warning Joe's
/// Liturgy meter count through <see cref="CMUCqcInputEvent"/>).
/// </summary>
[Serializable, NetSerializable]
public enum CMUCqcInput : byte
{
    /// <summary>An unarmed or bladed hit on a living body.</summary>
    Hit,

    /// <summary>A wide swing that hit at least one living body.</summary>
    Wide,

    /// <summary>A disarm (tackle) attempt on a body.</summary>
    Disarm,

    /// <summary>A pull started. A sequence step <c>Grab</c> also accepts an aggressive grab.</summary>
    Grab,

    /// <summary>A pull upgraded to an aggressive grab (a one-click grip or the second Ctrl-click).</summary>
    AggressiveGrab,

    /// <summary>A close-quarters hotbar ability that landed.</summary>
    Ability,
}

/// <summary>
/// One recorded input: what, on whom (null for an untargeted ability, which matches any target) and when.
/// <paramref name="FromBlade"/>: the swing came from a bladed weapon, not the body's own fists.
/// </summary>
public readonly record struct CMUCqcBufferEntry(CMUCqcInput Input, EntityUid? Target, TimeSpan Time, bool FromBlade = false);
