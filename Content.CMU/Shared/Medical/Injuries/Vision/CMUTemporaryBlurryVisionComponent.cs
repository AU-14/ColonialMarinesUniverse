using System;
using System.Collections.Generic;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Injuries.Vision;

[RegisterComponent]
public sealed partial class CMUTemporaryBlurryVisionComponent : Component
{
    [DataField]
    public List<CMUTemporaryBlurModifier> Modifiers = new();
}

[DataDefinition, Serializable]
public sealed partial class CMUTemporaryBlurModifier
{
    [DataField]
    public TimeSpan ExpiresAt;

    [DataField]
    public float Strength;
}
