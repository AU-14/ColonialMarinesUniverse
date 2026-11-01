using Content.Client.CMU14.Interface;
using Content.Client.Message;
using Content.Shared._RMC14.SupplyDrop;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Overwatch;

/// <summary>
/// Supply drop readiness on the squad dashboard header, so the operator sees whether a crate is on
/// the pad and the launcher is charged without opening the supply drop tab.
/// </summary>
public sealed class CMUSupplyReadinessLabel : RichTextLabel
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly EntityUid _console;
    private string _last = string.Empty;

    public CMUSupplyReadinessLabel(EntityUid console)
    {
        IoCManager.InjectDependencies(this);
        _console = console;
        VerticalAlignment = VAlignment.Center;
        Margin = new Thickness(0, 0, 12, 0);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_entities.TryGetComponent(_console, out SupplyDropComputerComponent? supplyDrop))
        {
            Visible = false;
            return;
        }

        Visible = true;

        string state;
        Color color;
        var left = supplyDrop.NextLaunchAt - _timing.CurTime;
        if (left > TimeSpan.Zero)
        {
            state = Loc.GetString("cmu-overwatch-supply-recharging", ("seconds", (int) Math.Ceiling(left.TotalSeconds)));
            color = CrtTerminalPalette.Caution;
        }
        else if (!supplyDrop.HasCrate)
        {
            state = Loc.GetString("cmu-overwatch-supply-no-crate");
            color = CrtTerminalPalette.Alert;
        }
        else
        {
            state = Loc.GetString("cmu-overwatch-supply-ready");
            color = CrtTerminalPalette.Accent;
        }

        var markup = $"[color={CrtTerminalPalette.TextDim.ToHex()}]{FormattedMessage.EscapeText(Loc.GetString("cmu-overwatch-supply-label"))}[/color] " +
                     $"[color={color.ToHex()}][bold]{FormattedMessage.EscapeText(state)}[/bold][/color]";

        if (markup == _last)
            return;

        _last = markup;
        this.SetMarkupPermissive(markup);
    }
}
