using Content.Shared.CMU14.Guidance;
using Content.Shared._RMC14.Medical.MedevacStretcher;
using Content.Shared._RMC14.Dropship.Utility.Components;
using static Content.Shared._RMC14.Dropship.Weapon.DropshipTerminalWeaponsComponent;

namespace Content.Shared._RMC14.Dropship.Weapon;

public abstract partial class SharedDropshipWeaponSystem
{
    public void RefreshGuidanceTargets()
    {
        if (_net.IsClient)
            return;
        var guidance = EntityManager.System<CMUGuidanceSystem>();
        var terminals = EntityQueryEnumerator<DropshipTerminalWeaponsComponent>();
        while (terminals.MoveNext(out var uid, out var terminal))
        {
            var faction = GetConsoleFaction(uid);
            terminal.Targets.Clear();
            var targets = EntityQueryEnumerator<DropshipTargetComponent>();
            while (targets.MoveNext(out var target, out var mark))
            {
                if (!guidance.CanDetect(uid, target) ||
                    !string.IsNullOrEmpty(mark.CreatorFaction) && !string.IsNullOrEmpty(faction) &&
                    !string.Equals(mark.CreatorFaction, faction, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (HasComp<MedevacStretcherComponent>(target) || HasComp<RMCActiveFultonComponent>(target))
                    continue;
                terminal.Targets.Add(new TargetEnt(GetNetEntity(target), mark.Abbreviation));
            }
            terminal.Targets.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            if (terminal.Target is { } selected && !guidance.CanDetect(uid, selected))
                SetTarget((uid, terminal), null);
            if (terminal.CameraTarget is { } camera && !guidance.CanDetect(uid, camera))
                TrySetCameraTarget(uid, null, terminal);
            Dirty(uid, terminal);
            RefreshWeaponsUI((uid, terminal));
        }
    }
}
