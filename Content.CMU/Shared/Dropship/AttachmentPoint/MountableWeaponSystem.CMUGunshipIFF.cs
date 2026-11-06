using Content.Shared.CMU14.Dropship.AttachmentPoint;

namespace Content.Shared._RMC14.Emplacements;

public sealed partial class MountableWeaponSystem
{
    [Dependency] private CMUGunshipCrewIFFSystem _cmuCrewIff = default!;
}
