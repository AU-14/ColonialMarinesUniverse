using Robust.Shared.Serialization;
using System;

namespace Content.Shared.CMU14.Hygiene;

[Serializable, NetSerializable]
public enum CMUHygieneVisuals : byte
{
    Powered,
    Running,
    Water,
    Indicator,
    Drum,
    Broken,
    DamageLayer,
}
