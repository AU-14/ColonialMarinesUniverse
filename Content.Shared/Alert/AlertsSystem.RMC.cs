using Robust.Shared.Prototypes;

namespace Content.Shared.Alert;

public abstract partial class AlertsSystem
{
    /// <summary>
    /// Compatibility overload for RMC component entities and legacy dynamic alert text.
    /// </summary>
    public void ShowAlert(
        EntityUid entity,
        ProtoId<AlertPrototype> alertType,
        short? severity = null,
        (TimeSpan, TimeSpan)? cooldown = null,
        bool autoRemove = false,
        bool showCooldown = true,
        string? dynamicMessage = null)
    {
        _ = dynamicMessage;
        ShowAlert((Entity<AlertsComponent?>) entity, alertType, severity, cooldown, autoRemove, showCooldown);
    }

    public void ClearAlert(EntityUid entity, ProtoId<AlertPrototype> alertType)
    {
        ClearAlert((Entity<AlertsComponent?>) entity, alertType);
    }

    public void ClearAlertCategory(EntityUid entity, ProtoId<AlertCategoryPrototype> category)
    {
        ClearAlertCategory((Entity<AlertsComponent?>) entity, category);
    }
}
