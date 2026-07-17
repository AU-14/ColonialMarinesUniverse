using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Traits.NicotineAddiction;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NicotineAddictionComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan LastSmoked;

    [DataField]
    public TimeSpan CravingThreshold = TimeSpan.FromMinutes(15);

    [DataField]
    public TimeSpan ShakeThreshold = TimeSpan.FromMinutes(20);

    [DataField, AutoNetworkedField]
    public bool Craving;

    [DataField]
    public ProtoId<AlertPrototype> CravingAlert = "CMUNicotineCraving";

    [DataField]
    public TimeSpan CravingMessageCooldown = TimeSpan.FromSeconds(90);

    [ViewVariables]
    public TimeSpan NextCravingMessage;

    [DataField]
    public TimeSpan ShakeIntervalMin = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan ShakeIntervalMax = TimeSpan.FromSeconds(90);

    [DataField, AutoNetworkedField]
    public TimeSpan NextShake;

    [DataField]
    public TimeSpan ShakeDuration = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan TimeBetweenChecks = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public TimeSpan NextCheck;
}
