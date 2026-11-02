using Robust.Shared.Console;

namespace Content.Client.CMU14.VoiceChat;

public sealed partial class CMUVoiceChatCommand : LocalizedCommands
{
    [Dependency] private CMUVoiceChatManager _voiceChat = default!;

    public override string Command => "voicechat";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!_voiceChat.RequestLink())
        {
            shell.WriteError(Loc.GetString("cmu-voicechat-unavailable"));
            return;
        }

        shell.WriteLine(Loc.GetString("cmu-voicechat-opening"));
    }
}
