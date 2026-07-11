// ReSharper disable CheckNamespace

using Content.Shared._CMU14.Item.Stain;

namespace Content.Server._RMC14.Xenonids.AcidBloodSplash;

public sealed partial class AcidBloodSplashSystem
{
    [Dependency] private CMUItemStainSystem _cmuItemStains = default!;

    private void CMUStainTarget(Entity<AcidBloodSplashComponent> source, EntityUid target)
    {
        _cmuItemStains.StainExposedEquipment(
            target,
            CMUItemStainKind.Blood,
            source.Comp.StainColor,
            source);
    }
}
