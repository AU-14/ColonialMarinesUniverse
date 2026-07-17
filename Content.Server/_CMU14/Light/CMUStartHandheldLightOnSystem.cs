using Content.Server.Light.EntitySystems;
using Content.Shared.Light.Components;

namespace Content.Server._CMU14.Light;

public sealed partial class CMUStartHandheldLightOnSystem : EntitySystem
{
    [Dependency] private HandheldLightSystem _handheldLight = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUStartHandheldLightOnComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<CMUStartHandheldLightOnComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp(ent, out HandheldLightComponent? light) ||
            light.Activated)
        {
            return;
        }

        _handheldLight.TurnOn(ent.Owner, (ent.Owner, light));
    }
}
