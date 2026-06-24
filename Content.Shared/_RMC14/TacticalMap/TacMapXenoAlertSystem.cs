using Content.Shared.Alert;

namespace Content.Shared._RMC14.TacticalMap;

public sealed partial class TacMapXenoAlertSystem : EntitySystem
{
    [Dependency] private AlertsSystem _alerts = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<TacMapXenoAlertComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<TacMapXenoAlertComponent, ComponentRemove>(OnRemove);
    }

    private void OnStartup(Entity<TacMapXenoAlertComponent> ent, ref ComponentStartup args)
    {
        _alerts.ShowAlert(ent, ent.Comp.Alert);
    }
    private void OnRemove(Entity<TacMapXenoAlertComponent> ent, ref ComponentRemove args)
    {
        _alerts.ClearAlert(ent, ent.Comp.Alert);
    }
}
