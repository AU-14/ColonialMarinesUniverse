using Content.Shared._RMC14.Intel.Detector;
using Content.Shared._RMC14.MotionDetector;
using Content.Shared.Storage;
using Robust.Shared.Player;

namespace Content.Shared.Interaction;

public sealed partial class SmartEquipSystem
{
    [Dependency] private MotionDetectorSystem _rmcMotionDetector = default!;
    [Dependency] private IntelDetectorSystem _rmcIntelDetector = default!;

    private void HandleSmartEquipUniform(ICommonSession? session)
    {
        HandleSmartEquip(session, "jumpsuit");
    }

    private void HandleSmartEquipArmor(ICommonSession? session)
    {
        HandleSmartEquip(session, "outerClothing");
    }

    private void HandleSmartEquipHelmet(ICommonSession? session)
    {
        HandleSmartEquip(session, "head");
    }

    private bool CanUseRMCStorage(EntityUid user, Entity<StorageComponent> storage)
    {
        return _actionBlocker.CanInteract(user, storage.Owner) &&
               _storage.CanInteractRMC(user, storage, silent: false);
    }

    private void DisableRMCDetector(EntityUid item)
    {
        if (TryComp(item, out MotionDetectorComponent? motion) && motion.Enabled)
        {
            _rmcMotionDetector.Toggle((item, motion));
            return;
        }

        if (TryComp(item, out IntelDetectorComponent? intel) && intel.Enabled)
            _rmcIntelDetector.Toggle((item, intel));
    }
}
