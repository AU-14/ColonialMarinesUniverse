using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;

namespace Content.Server.CMU14.VoiceChat;

[AdminCommand(AdminFlags.Moderator)]
public sealed partial class CMUVoiceMuteCommand : LocalizedEntityCommands
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private CMUVoiceChatSystem _voiceChat = default!;

    public override string Command => "voicemute";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!_player.TryGetSessionByUsername(args[0], out var session))
        {
            shell.WriteError(Loc.GetString("shell-target-player-does-not-exist"));
            return;
        }

        var muted = _voiceChat.ToggleAdminMute(session.UserId);
        shell.WriteLine(Loc.GetString(muted ? "cmu-voicemute-muted" : "cmu-voicemute-unmuted", ("player", session.Name)));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? CompletionResult.FromHintOptions(CompletionHelper.SessionNames(players: _player), Loc.GetString("cmu-voicemute-hint"))
            : CompletionResult.Empty;
    }
}
