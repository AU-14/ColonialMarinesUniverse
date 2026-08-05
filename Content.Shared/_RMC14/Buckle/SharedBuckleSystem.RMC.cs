using System.Numerics;
using Content.Shared._RMC14.Buckle;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Buckle.Components;
using Content.Shared.Popups;

namespace Content.Shared.Buckle;

public abstract partial class SharedBuckleSystem
{
    private Vector2 GetRMCBuckleOffset(EntityUid buckle, StrapComponent strap)
    {
        return strap.BuckleOffset + (CompOrNull<RMCBuckleOffsetComponent>(buckle)?.Offset ?? Vector2.Zero);
    }

    private bool CanRMCUserBuckle(EntityUid? user, EntityUid buckle, bool popup)
    {
        if (!HasComp<XenoComponent>(user))
            return true;

        if (popup)
        {
            _popup.PopupPredicted("You don't have the dexterity to do that, try a nest.",
                buckle,
                user.Value,
                PopupType.SmallCaution);
        }

        return false;
    }
}
