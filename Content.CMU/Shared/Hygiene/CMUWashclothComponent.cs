using Content.Shared.DoAfter;
using Robust.Shared.Serialization;
using System;

namespace Content.Shared.CMU14.Hygiene;

[RegisterComponent]
public sealed partial class CMUWashclothComponent : Component
{
    [DataField] public int UsesRemaining = 2;
}

[Serializable, NetSerializable]
public sealed partial class CMUWashclothDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public enum CMUWashclothVisuals : byte { Used, Layer }
