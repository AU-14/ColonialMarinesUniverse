using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;

#pragma warning disable IDE0130
namespace Content.Shared._RMC14.PowerLoader;
#pragma warning restore IDE0130

public sealed partial class PowerLoaderSystem
{
    /// <summary>
    /// Rechecked when attachment completes so takeoff and simultaneous installs cannot bypass the limits.
    /// </summary>
    public bool CanAttachDropshipPayload(EntityUid user, EntityUid point, EntityUid payload)
    {
        if (!_dropship.TryGetGridDropship(point, out var ship))
            return true;

        string? message = null;
        if (HasComp<DropshipAmmoComponent>(payload) &&
            (_dropship.IsInFlight((ship.Owner, ship.Comp)) || ship.Comp.State == FTLState.Starting ||
             TryComp(ship, out FTLComponent? ftl) &&
             ftl.State is FTLState.Starting or FTLState.Travelling or FTLState.Arriving))
        {
            message = "cmu-dropship-load-flight";
        }
        else if (HasComp<DropshipUtilityPointComponent>(point) && HasComp<DropshipWeaponComponent>(payload))
        {
            foreach (var attachment in ship.Comp.AttachmentPoints)
            {
                if (!TryComp(attachment, out DropshipUtilityPointComponent? utility) ||
                    !_container.TryGetContainer(attachment, utility.UtilitySlotId, out var slot))
                    continue;

                foreach (var installed in slot.ContainedEntities)
                {
                    if (HasComp<DropshipWeaponComponent>(installed))
                    {
                        message = "cmu-dropship-internal-weapon-limit";
                        break;
                    }
                }

                if (message != null)
                    break;
            }
        }

        if (message == null)
            return true;

        foreach (var buckled in GetBuckled(user))
            _popup.PopupEntity(Loc.GetString(message), point, buckled, PopupType.SmallCaution);
        return false;
    }
}
