using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// Takes the colour, the mode and the rim design picked in a vehicle spray painter's window.
/// </summary>
public sealed class CMUVehicleSprayPainterSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    /// <summary>
    /// A design's colours: base, accent, lip and cap.
    /// </summary>
    public const int DesignColorCount = 4;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleSprayPainterComponent, CMUVehicleSprayPainterColorMessage>(OnColor);
        SubscribeLocalEvent<CMUVehicleSprayPainterComponent, CMUVehicleSprayPainterModeMessage>(OnMode);
        SubscribeLocalEvent<CMUVehicleSprayPainterComponent, CMUVehicleSprayPainterDesignMessage>(OnDesign);
    }

    private void OnColor(Entity<CMUVehicleSprayPainterComponent> ent, ref CMUVehicleSprayPainterColorMessage args)
    {
        var color = args.Color.WithAlpha(1f);
        if (ent.Comp.Color == color)
            return;

        ent.Comp.Color = color;
        Dirty(ent);
    }

    private void OnMode(Entity<CMUVehicleSprayPainterComponent> ent, ref CMUVehicleSprayPainterModeMessage args)
    {
        if (ent.Comp.Mode == args.Mode || !Enum.IsDefined(args.Mode))
            return;

        ent.Comp.Mode = args.Mode;
        Dirty(ent);
    }

    private void OnDesign(Entity<CMUVehicleSprayPainterComponent> ent, ref CMUVehicleSprayPainterDesignMessage args)
    {
        // Sent by a client, either can arrive missing.
        if (args.Colors == null ||
            string.IsNullOrEmpty(args.Design.Id) ||
            !_prototypes.HasIndex(args.Design) ||
            args.Colors.Count != DesignColorCount)
            return;

        ent.Comp.Design = args.Design;
        ent.Comp.DesignColors = args.Colors.ConvertAll(color => color.WithAlpha(1f));
        Dirty(ent);
    }

    /// <summary>
    /// The colours a painter sprays its design in: those picked, or the design's own with the
    /// painter's colour as the base.
    /// </summary>
    public static List<Color> DesignColors(CMUVehicleSprayPainterComponent painter, CMURimDesignPrototype design)
    {
        if (painter.DesignColors.Count == DesignColorCount)
            return new List<Color>(painter.DesignColors);

        return new List<Color> { painter.Color.WithAlpha(1f), design.Accent, design.LipColor, design.CapColor };
    }
}
