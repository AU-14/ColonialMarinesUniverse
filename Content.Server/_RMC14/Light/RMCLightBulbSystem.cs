using Content.Server.Light.EntitySystems;
using Content.Shared.Light;
using Content.Shared.Light.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.Audio;

namespace Content.Server._RMC14.Light;

public sealed partial class RMCLightBulbSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private LightBulbSystem _lightBulb = default!;
    [Dependency] private SharedPointLightSystem _pointLight = default!;
    [Dependency] private PoweredLightSystem _poweredLight = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RMCBreakLightOnAttackComponent, AttackedEvent>(OnBreakLightAttacked);
    }

    private void OnBreakLightAttacked(Entity<RMCBreakLightOnAttackComponent> ent, ref AttackedEvent args)
    {
        // Populated fixtures keep their bulb in a container, not on the fixture entity.
        if (HasComp<PoweredLightComponent>(ent))
        {
            _poweredLight.TryDestroyBulb(ent, user: args.User);
            return;
        }

        if (TryComp(ent, out LightBulbComponent? lightBulb))
        {
            if (lightBulb.State == LightBulbState.Broken)
                return;

            _lightBulb.SetState(ent, LightBulbState.Broken, lightBulb);
        }
        else
        {
            // Always-powered fixtures have a light directly on the housing and no removable bulb.
            if (!_pointLight.TryGetLight(ent, out var light) || !light.Enabled)
                return;

            _pointLight.SetEnabled(ent, false, light);
            _appearance.SetData(ent, PoweredLightVisuals.BulbState, PoweredLightState.Broken);
        }

        _audio.PlayPvs(ent.Comp.Sound, ent);
    }
}
