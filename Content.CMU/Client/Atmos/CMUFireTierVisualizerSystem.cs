using Content.Shared.CMU14.Atmos;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client.CMU14.Atmos;

/// <summary>
/// FLOW-22: tints a tiered tile fire to its intensity colour. RMC's red/blue/green/white/purple fire states are
/// pre-coloured and do not tint, so the base layer is switched to the matching greyscale <c>dynamic_N</c> state of
/// the same size; the sprite colour and the point light then carry the tier colour. Runs after
/// <see cref="GenericVisualizerSystem"/>, which resets the state whenever the fire grows.
/// </summary>
public sealed partial class CMUFireTierVisualizerSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;

    private const string BaseLayer = "base";
    private const string DynamicPrefix = "dynamic_";

    private static readonly string[] TintlessPrefixes = { "red_", "blue_", "green_", "white_", "purple_" };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUFireTierComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CMUFireTierComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<CMUFireTierComponent, AppearanceChangeEvent>(OnAppearanceChange,
            after: new[] { typeof(GenericVisualizerSystem) });
    }

    private void OnStartup(Entity<CMUFireTierComponent> ent, ref ComponentStartup args)
    {
        Apply(ent);
    }

    private void OnState(Entity<CMUFireTierComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        Apply(ent);
    }

    private void OnAppearanceChange(Entity<CMUFireTierComponent> ent, ref AppearanceChangeEvent args)
    {
        Apply(ent);
    }

    private void Apply(Entity<CMUFireTierComponent> ent)
    {
        if (TryComp(ent, out SpriteComponent? sprite))
        {
            Entity<SpriteComponent?> spriteEnt = (ent.Owner, sprite);
            if (_sprite.LayerMapTryGet(spriteEnt, BaseLayer, out var index, false))
            {
                var state = _sprite.LayerGetRsiState(spriteEnt, index).Name;
                if (ToDynamicState(state) is { } dynamic)
                    _sprite.LayerSetRsiState(spriteEnt, index, new RSI.StateId(dynamic));
            }

            _sprite.SetColor(spriteEnt, ent.Comp.Color);
        }

        _lights.SetColor(ent, ent.Comp.Color);
    }

    /// <summary><c>red_3</c> becomes <c>dynamic_3</c>; states that already tint, or are unknown, return null.</summary>
    private static string? ToDynamicState(string? state)
    {
        if (string.IsNullOrEmpty(state) || state.StartsWith(DynamicPrefix, StringComparison.Ordinal))
            return null;

        foreach (var prefix in TintlessPrefixes)
        {
            if (state.StartsWith(prefix, StringComparison.Ordinal))
                return DynamicPrefix + state.Substring(prefix.Length);
        }

        return null;
    }
}
