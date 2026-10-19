using Content.Server.Administration.Logs;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.DropPod;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Database;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.DropPod;

public sealed partial class CMUDropPodConsoleSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private CMUCustomDropUnitSystem _customUnit = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private CMUDropPodSystem _pod = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTacticalMapSystem _tacticalMap = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUDropPodConsoleComponent, AfterActivatableUIOpenEvent>(OnUIOpened);
        Subs.BuiEvents<CMUDropPodConsoleComponent>(CMUDropPodConsoleUiKey.Key,
            subs =>
            {
                subs.Event<CMUDropPodSelectTargetMsg>(OnSelectTarget);
                subs.Event<CMUDropPodLaunchMsg>(OnLaunch);
                subs.Event<CMUDropPodRefreshMsg>(OnRefresh);
            });
    }

    private void OnUIOpened(Entity<CMUDropPodConsoleComponent> ent, ref AfterActivatableUIOpenEvent args)
    {
        UpdateUI(ent);
    }

    private void OnRefresh(Entity<CMUDropPodConsoleComponent> ent, ref CMUDropPodRefreshMsg args)
    {
        UpdateUI(ent);
    }

    private void OnSelectTarget(Entity<CMUDropPodConsoleComponent> ent, ref CMUDropPodSelectTargetMsg args)
    {
        ent.Comp.Target = args.Target;
        Dirty(ent);
        UpdateUI(ent);
    }

    private void OnLaunch(Entity<CMUDropPodConsoleComponent> ent, ref CMUDropPodLaunchMsg args)
    {
        var user = args.Actor;
        if (_timing.CurTime < ent.Comp.NextLaunch)
        {
            _popup.PopupCursor(Loc.GetString("cmu-drop-pod-console-cooldown"), user, PopupType.SmallCaution);
            return;
        }

        if (ent.Comp.Target is not { } target || !_tacticalMap.TryGetTacticalMap(out var planet))
        {
            _popup.PopupCursor(Loc.GetString("cmu-drop-pod-console-no-target"), user, PopupType.SmallCaution);
            return;
        }

        var delay = TimeSpan.Zero;
        var launched = 0;
        foreach (var netPod in args.Pods)
        {
            if (!TryGetEntity(netPod, out var uid) || !IsLinked(ent, uid.Value))
                continue;

            string? reason;
            var success = TryComp(uid, out CMUDropPodComponent? pod)
                ? _pod.TryLaunch((uid.Value, pod), planet.Owner, target, delay, out reason)
                : _customUnit.TryLaunch(uid.Value, planet.Owner, target, out reason);

            if (!success)
            {
                if (reason != null)
                    _popup.PopupCursor(reason, user, PopupType.SmallCaution);

                continue;
            }

            launched++;
            delay += ent.Comp.LaunchStagger;
            _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(user):user} launched drop unit {ToPrettyString(uid.Value):pod} toward tile {target} from {ToPrettyString(ent):console}");
        }

        if (launched > 0)
        {
            ent.Comp.NextLaunch = _timing.CurTime + ent.Comp.Cooldown;
            Dirty(ent);
            _audio.PlayPvs(ent.Comp.LaunchSound, ent);
            _popup.PopupCursor(Loc.GetString("cmu-drop-pod-console-launched", ("count", launched)), user);
        }

        UpdateUI(ent);
    }

    private bool IsLinked(Entity<CMUDropPodConsoleComponent> console, EntityUid unit)
    {
        string group;
        if (TryComp(unit, out CMUDropPodComponent? pod))
            group = pod.Group;
        else if (TryComp(unit, out CMUCustomDropUnitComponent? custom))
            group = custom.Group;
        else
            return false;

        return group == console.Comp.Group
            && Transform(console).MapUid is { } consoleMap
            && _zLevels.IsSameZNetwork(Transform(unit).MapUid, consoleMap);
    }

    private void UpdateUI(Entity<CMUDropPodConsoleComponent> ent)
    {
        var entries = new List<CMUDropPodEntry>();
        var pods = EntityQueryEnumerator<CMUDropPodComponent>();
        while (pods.MoveNext(out var uid, out var pod))
        {
            if (!IsLinked(ent, uid))
                continue;

            var occupant = pod.Container.ContainedEntity is { } contained ? Name(contained) : null;
            var weapons = 0;
            foreach (var slotId in pod.WeaponSlots)
            {
                if (_itemSlots.GetItemOrNull(uid, slotId) != null)
                    weapons++;
            }

            var ready = _pod.CanLaunch((uid, pod), out _);
            entries.Add(new CMUDropPodEntry(GetNetEntity(uid), Name(uid), occupant, weapons, pod.ScatterRadius, false, ready));
        }

        var units = EntityQueryEnumerator<CMUCustomDropUnitComponent>();
        while (units.MoveNext(out var uid, out var unit))
        {
            if (!IsLinked(ent, uid))
                continue;

            var ready = unit.State == CMUDropPodState.Ready;
            entries.Add(new CMUDropPodEntry(GetNetEntity(uid), Name(uid), null, 0, unit.ScatterRadius, true, ready));
        }

        _ui.SetUiState(ent.Owner, CMUDropPodConsoleUiKey.Key, new CMUDropPodConsoleBuiState(entries, ent.Comp.Target, ent.Comp.NextLaunch));
    }
}
