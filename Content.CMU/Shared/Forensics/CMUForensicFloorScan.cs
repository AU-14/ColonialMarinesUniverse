using Content.Shared.DoAfter;
using Content.Shared.Forensics.Components;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Forensics;

/// <summary>
///     Raised broadcast when a forensic scanner is used on the floor rather than on an entity, so blood drawn as decals
///     can be scanned.
/// </summary>
[ByRefEvent]
public readonly record struct CMUForensicScanFloorEvent(Entity<ForensicScannerComponent> Scanner, EntityUid User, EntityCoordinates Location);

[Serializable, NetSerializable]
public sealed partial class CMUBloodScanDoAfterEvent : SimpleDoAfterEvent;
