using System;
using System.Collections.Generic;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._CMU14.Medical.TemporaryBlurryVision;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CMUTemporaryBlurryVisionComponent : Component
{
    [DataField]
    public TimeSpan UpdateRate = TimeSpan.FromSeconds(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextUpdate;

    [DataField]
    public List<CMUTemporaryBlurModifier> Modifiers = new();
}

[DataDefinition, Serializable]
public sealed partial class CMUTemporaryBlurModifier
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan ExpiresAt;

    [DataField]
    public float Strength;
}
