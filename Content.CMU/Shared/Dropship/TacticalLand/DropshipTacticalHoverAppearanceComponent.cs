using System;
using System.Collections.Generic;
using System.Numerics;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Dropship.TacticalLand;

/// <summary>Hull-local lift engine positions, independent of the landing clearance rectangle.</summary>
[RegisterComponent]
public sealed partial class DropshipTacticalHoverAppearanceComponent : Component
{
    [DataField]
    public List<Vector2> DownwashOffsets = new();

    [DataField]
    public EntProtoId ShadowPrototype = "CMUGunshipTacticalHoverShadow";

    [DataField]
    public EntProtoId DownwashPrototype = "CMUGunshipTacticalHoverDownwash";

    [DataField]
    public EntProtoId NozzleLightPrototype = "CMUGunshipTacticalHoverNozzleLight";
}

/// <summary>A snapshot lets observers below the ship see its silhouette without receiving the upper grid.</summary>
[Serializable, NetSerializable]
public readonly record struct DropshipShadowTile(Vector2 Position, string Texture, byte Variant);

[Serializable, NetSerializable]
public readonly record struct DropshipShadowPart(string Prototype, Vector2 Position, Angle Rotation);
