using Content.Shared.FootPrint;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Random;

namespace Content.Client.FootPrint;

public sealed partial class FootPrintsVisualizerSystem : VisualizerSystem<FootPrintComponent>
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FootPrintComponent, ComponentInit>(OnInitialized);
        SubscribeLocalEvent<FootPrintComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnInitialized(EntityUid uid, FootPrintComponent comp, ComponentInit args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        _sprite.LayerMapReserve((uid, sprite), FootPrintVisualLayers.Print);
        UpdateAppearance(uid, comp, sprite);
    }

    private void OnShutdown(EntityUid uid, FootPrintComponent comp, ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(uid, out var sprite)
            && _sprite.LayerMapTryGet((uid, sprite), FootPrintVisualLayers.Print, out var layer, false))
            _sprite.RemoveLayer((uid, sprite), layer);
    }

    private void UpdateAppearance(EntityUid uid, FootPrintComponent component, SpriteComponent sprite)
    {
        if (!_sprite.LayerMapTryGet((uid, sprite), FootPrintVisualLayers.Print, out var layer, false)
            || !TryComp<FootPrintsComponent>(component.PrintOwner, out var printsComponent)
            || !TryComp<AppearanceComponent>(uid, out var appearance)
            || !_appearance.TryGetData<FootPrintVisuals>(uid, FootPrintVisualState.State, out var printVisuals, appearance))
            return;

        _sprite.LayerSetRsi((uid, sprite), layer, printsComponent.RsiPath, new RSI.StateId(printVisuals switch
        {
            FootPrintVisuals.BareFootPrint => printsComponent.RightStep ? printsComponent.RightBarePrint : printsComponent.LeftBarePrint,
            FootPrintVisuals.ShoesPrint => printsComponent.ShoesPrint,
            FootPrintVisuals.SuitPrint => printsComponent.SuitPrint,
            FootPrintVisuals.Dragging => _random.Pick(printsComponent.DraggingPrint),
            _ => throw new ArgumentOutOfRangeException($"Unknown {printVisuals} parameter.")
        }));

        if (_appearance.TryGetData<Color>(uid, FootPrintVisualState.Color, out var printColor, appearance))
            _sprite.LayerSetColor((uid, sprite), layer, printColor);
    }

    protected override void OnAppearanceChange(EntityUid uid, FootPrintComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not { } sprite)
            return;

        UpdateAppearance(uid, component, sprite);
    }
}
