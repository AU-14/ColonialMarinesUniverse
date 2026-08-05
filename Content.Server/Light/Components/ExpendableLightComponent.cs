using Content.Shared.Light.Components;

namespace Content.Server.Light.Components
{
    /// <summary>
    ///     Component that represents a handheld expendable light which can be activated and eventually dies over time.
    /// </summary>
    [RegisterComponent]
    public sealed partial class ExpendableLightComponent : SharedExpendableLightComponent
    {
        [ViewVariables] public float StateExpiryTime = default;
    }
}
