using Content.Shared.Kitchen.EntitySystems;

namespace Content.Server.Kitchen.EntitySystems;

/// <inheritdoc />
public sealed partial class ReagentGrinderSystem : SharedReagentGrinderSystem
{
    public override void Initialize()
    {
        base.Initialize();
        InitializeCMU();
    }

    partial void InitializeCMU();
}
