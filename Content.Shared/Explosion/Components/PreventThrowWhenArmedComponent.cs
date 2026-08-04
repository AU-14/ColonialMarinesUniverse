using Robust.Shared.GameStates;

namespace Content.Shared.Explosion.Components
{
    /// <summary>
    /// Attach this to explosives that should not be thrown/inserted into containers while their
    /// timer trigger is active (i.e. primed).
    /// </summary>
    [RegisterComponent]
    public sealed partial class PreventThrowWhenArmedComponent : Component
    {
    }
}

