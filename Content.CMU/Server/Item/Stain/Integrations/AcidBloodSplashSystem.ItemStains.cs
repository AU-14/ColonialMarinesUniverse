// ReSharper disable CheckNamespace

using Content.Shared.CMU14.Item.Stain;
using Content.Shared.CMU14.Hygiene;

namespace Content.Server._RMC14.Xenonids.AcidBloodSplash;

public sealed partial class AcidBloodSplashSystem
{
    [Dependency] private CMUResidueSystem _cmuResidue = default!;
    [Dependency] private CMUItemStainSystem _cmuItemStains = default!;

    private void CMUStainTarget(Entity<AcidBloodSplashComponent> source, EntityUid target)
    {
        _cmuResidue.Stain(target, source.Comp.StainColor, alien: true);
        _cmuItemStains.StainExposedEquipment(
            target,
            CMUItemStainKind.Blood,
            source.Comp.StainColor,
            source);
    }
}
