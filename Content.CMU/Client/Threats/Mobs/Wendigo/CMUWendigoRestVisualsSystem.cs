using Content.Client.DamageState;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Rest;
using Content.Shared.CMU14.Threats.Mobs.Wendigo.Rest;
using Content.Shared.Mobs;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Threats.Mobs.Wendigo;

/// <summary>
/// Shows a resting Wendigo in its curled pose, the "dead" sprite standing in until resting art exists, with the
/// glow mask hidden because it is drawn for the standing pose. Runs after the damage-state visualizer so the mob
/// state does not overwrite it.
/// </summary>
public sealed partial class CMUWendigoRestVisualsSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUWendigoRestComponent, AppearanceChangeEvent>(OnAppearanceChange,
            after: [typeof(DamageStateVisualizerSystem)]);
    }

    private void OnAppearanceChange(Entity<CMUWendigoRestComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not { } sprite)
            return;

        // Dead (and crit) bodies are the damage-state visualizer's business.
        if (_appearance.TryGetData<MobState>(ent, MobStateVisuals.State, out var state, args.Component) &&
            state != MobState.Alive)
        {
            return;
        }

        var resting = _appearance.TryGetData<XenoRestState>(ent, XenoVisualLayers.Base, out var rest, args.Component) &&
                      rest == XenoRestState.Resting;

        if (_sprite.LayerMapTryGet((ent, sprite), DamageStateVisualLayers.Base, out _, false))
        {
            _sprite.LayerSetRsiState((ent, sprite), DamageStateVisualLayers.Base,
                resting ? ent.Comp.RestingState : ent.Comp.StandingState);
        }

        if (_sprite.LayerMapTryGet((ent, sprite), DamageStateVisualLayers.BaseUnshaded, out _, false))
            _sprite.LayerSetVisible((ent, sprite), DamageStateVisualLayers.BaseUnshaded, !resting);
    }
}
