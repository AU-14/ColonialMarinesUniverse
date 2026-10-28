using Content.Shared.CMU14.Threats.Mobs.Xeno.Gambler;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.CMU14.Threats.Mobs.Xeno.Gambler;

[RegisterComponent]
public sealed partial class CMUXenoGambleStateComponent : Component
{
    [DataField]
    public EntityUid? GambleAction;

    /// <summary>last icon step of the next reel</summary>
    [DataField]
    public int LastTickStep;

    [DataField]
    public int ReelsStopped;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan LastStepSound;

    [DataField]
    public List<CMUXenoGambleGrant> Grants = new();

    [DataField]
    public Dictionary<string, CMUXenoGambleGrantedComponent> GrantedComponents = new();
}

[DataDefinition]
public sealed partial class CMUXenoGambleGrant
{
    [DataField]
    public EntityUid Action;

    [DataField]
    public CMUXenoGambleRoll Roll = new();

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan GrantedAt;

    [DataField]
    public bool Used;
}

[DataDefinition]
public sealed partial class CMUXenoGambleGrantedComponent
{
    [DataField]
    public CMUXenoGambleRoll Source = new();

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan? RemoveAt;
}
