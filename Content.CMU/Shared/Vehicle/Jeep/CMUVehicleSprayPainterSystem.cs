namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// Takes the colour picked in a vehicle spray painter's window.
/// </summary>
public sealed class CMUVehicleSprayPainterSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleSprayPainterComponent, CMUVehicleSprayPainterColorMessage>(OnColor);
    }

    private void OnColor(Entity<CMUVehicleSprayPainterComponent> ent, ref CMUVehicleSprayPainterColorMessage args)
    {
        var color = args.Color.WithAlpha(1f);
        if (ent.Comp.Color == color)
            return;

        ent.Comp.Color = color;
        Dirty(ent);
    }
}
