using Content.Shared.CMU14.Emplacements;
using Content.Shared.Popups;
using Robust.Shared.Map;

namespace Content.Shared._RMC14.Emplacements;

public abstract partial class SharedWeaponMountSystem
{
    private bool CMUBlocksEmplacementDeployment(EntityCoordinates coordinates)
        => HasComp<CMUBlockEmplacementDeploymentComponent>(_transform.GetGrid(coordinates));

    private bool CMUCanDeployEmplacement(EntityUid user, EntityCoordinates coordinates)
    {
        if (!CMUBlocksEmplacementDeployment(Transform(user).Coordinates)
            && !CMUBlocksEmplacementDeployment(coordinates))
            return true;

        _popup.PopupClient(Loc.GetString("cmu-gunship-emplacement-deploy-blocked"), user, user,
            PopupType.SmallCaution);
        return false;
    }
}
