using Content.Client.Items.Systems;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Hands;
using Content.Shared.Item;
using Content.Shared.Toggleable;
using Content.Shared.Wieldable.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Yautja;

public sealed partial class YautjaArrowVisualizerSystem : VisualizerSystem<YautjaArrowComponent>
{
    [Dependency] private SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, YautjaArrowComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null ||
            !AppearanceSystem.TryGetData<YautjaArrowVisualState>(uid, YautjaArrowVisuals.State, out var state, args.Component))
        {
            return;
        }

        if (AppearanceSystem.TryGetData<bool>(uid, ToggleableVisuals.Enabled, out var enabled, args.Component) &&
            enabled)
        {
            return;
        }

        if (!_sprite.LayerMapTryGet((uid, args.Sprite), "base", out var layer, false))
            return;

        _sprite.LayerSetRsiState((uid, args.Sprite), layer, StateFor(component, state));
    }

    // same icons cmss13 swaps between: an unarmed arrow shows the warhead it can arm,
    // a dynamic one stays plain until a warhead is picked
    private static string StateFor(YautjaArrowComponent arrow, YautjaArrowVisualState state)
    {
        return state switch
        {
            YautjaArrowVisualState.Explosive => "arrow_expl_active",
            YautjaArrowVisualState.Emp => "arrow_emp_active",
            YautjaArrowVisualState.Dynamic => "arrow_inert",
            YautjaArrowVisualState.Snare => "arrow_trap",
            _ when arrow.SecondaryWarhead == YautjaArrowWarhead.Emp => "arrow_emp",
            _ => "arrow_expl",
        };
    }
}

public sealed partial class YautjaBowVisualizerSystem : VisualizerSystem<YautjaBowComponent>
{
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<YautjaBowComponent, GetInhandVisualsEvent>(OnGetInhandVisuals, after: [typeof(ItemSystem)]);
    }

    protected override void OnAppearanceChange(EntityUid uid, YautjaBowComponent component, ref AppearanceChangeEvent args)
    {
        // nocking doesn't touch the held prefix, so the hands have to be told
        _item.VisualsChanged(uid);

        if (args.Sprite == null ||
            !AppearanceSystem.TryGetData<string>(uid, YautjaBowVisuals.LoadedIcon, out var loadedIcon, args.Component))
        {
            return;
        }

        if (!_sprite.LayerMapTryGet((uid, args.Sprite), "arrow", out var arrowLayer, false))
            return;

        var state = loadedIcon switch
        {
            "expl" => "bow_expl",
            "emp" => "bow_emp",
            "trap" => "bow_trap",
            "loaded" => "bow_loaded",
            _ => "bow_loaded",
        };

        _sprite.LayerSetRsiState((uid, args.Sprite), arrowLayer, state);
    }

    // cmss13 picks the held sprite from the nocked arrow plus _w when wielded. the wield prefix is fixed
    // on WieldableComponent, so build the state here instead of fighting it over HeldPrefix
    private void OnGetInhandVisuals(Entity<YautjaBowComponent> ent, ref GetInhandVisualsEvent args)
    {
        if (!TryComp(ent, out SpriteComponent? sprite) || sprite.BaseRSI is not { } rsi)
            return;

        AppearanceSystem.TryGetData<string>(ent, YautjaBowVisuals.LoadedIcon, out var loadedIcon);
        var prefix = loadedIcon switch
        {
            "expl" => "bow_expl",
            "emp" => "bow_emp",
            // no snare-loaded inhand in cmss13, it shows the plain nocked arrow
            "loaded" or "trap" => "bow_loaded",
            _ => "bow",
        };

        if (TryComp(ent, out WieldableComponent? wieldable) && wieldable.Wielded)
            prefix += "_w";

        var key = $"inhand-{args.Location.ToString().ToLowerInvariant()}";
        var state = $"{prefix}-{key}";
        if (!rsi.TryGetState(state, out _))
            return;

        args.Layers.Clear();
        args.Layers.Add((key, new PrototypeLayerData
        {
            RsiPath = rsi.Path.ToString(),
            State = state,
            MapKeys = new() { key },
        }));
    }
}
