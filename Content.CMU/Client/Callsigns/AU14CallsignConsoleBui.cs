using Content.Shared._AU14.Callsigns;
using Content.Shared._AU14.Radio;
using Content.Shared._CMU14.Threats.Mobs.CLF;
using Content.Shared._RMC14.Marines;
using Robust.Client.Player;
using Robust.Client.UserInterface;

namespace Content.Client._AU14.Callsigns;

public sealed partial class AU14CallsignConsoleBui : BoundUserInterface
{
    [Dependency] private IPlayerManager _player = default!;

    private AU14CallsignConsoleWindow? _window;

    public AU14CallsignConsoleBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<AU14CallsignConsoleWindow>();

        _window.OnRenameElement += (squad, category, word) =>
            SendMessage(new AU14CallsignRenameElementMsg(squad, category, word));

        _window.OnSetSuffix += (member, suffix) =>
            SendMessage(new AU14CallsignSetSuffixMsg(member, suffix));

        _window.OnCreateGroup += word =>
            SendMessage(new AU14CallsignCreateGroupMsg(word));

        _window.OnDeleteGroup += word =>
            SendMessage(new AU14CallsignDeleteGroupMsg(word));

        _window.OnAssignGroup += (member, group) =>
            SendMessage(new AU14CallsignAssignGroupMsg(member, group));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null || state is not AU14CallsignConsoleState consoleState)
            return;

        _window.UpdateState(consoleState, CanEdit(consoleState.Faction));
    }

    // editing needs radio training and matching faction, the server checks this too
    private bool CanEdit(string faction)
    {
        // read-only terminals (pack directories) never show edit controls
        if (EntMan.TryGetComponent(Owner, out AU14CallsignConsoleComponent? console) && console.ReadOnly)
            return false;

        if (_player.LocalEntity is not { } local ||
            !EntMan.HasComponent<ANPRCRadioUserComponent>(local))
        {
            return false;
        }

        var localFaction = EntMan.HasComponent<CLFMemberComponent>(local)
            ? "clf"
            : EntMan.GetComponentOrNull<MarineComponent>(local)?.Faction;

        return localFaction == faction;
    }
}
