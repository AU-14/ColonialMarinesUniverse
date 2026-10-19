using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.DropPod;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.DropPod;

public sealed partial class CMUDropPodConsoleBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private IGameTiming _timing = default!;

    private CMUDropPodConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMUDropPodConsoleWindow>();
        _window.Map.OnQueenEyeMove += tile => SendMessage(new CMUDropPodSelectTargetMsg(tile));
        _window.FireButton.OnPressed += _ => SendMessage(new CMUDropPodLaunchMsg(_window.GetSelected()));
        _window.RefreshButton.OnPressed += _ => SendMessage(new CMUDropPodRefreshMsg());
        RefreshMap();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not CMUDropPodConsoleBuiState s || _window == null)
            return;

        RefreshMap();
        _window.UpdateState(s, _timing.CurTime);
    }

    private void RefreshMap()
    {
        if (_window == null || !EntMan.TryGetComponent(Owner, out TacticalMapComputerComponent? computer))
            return;

        if (computer.Map is { } map && EntMan.TryGetComponent(map, out AreaGridComponent? areaGrid))
            _window.Map.UpdateTexture((map, areaGrid));

        var blips = new TacticalMapBlip[computer.Blips.Count];
        var i = 0;
        foreach (var blip in computer.Blips.Values)
            blips[i++] = blip;

        _window.Map.UpdateBlips(blips);
    }
}
