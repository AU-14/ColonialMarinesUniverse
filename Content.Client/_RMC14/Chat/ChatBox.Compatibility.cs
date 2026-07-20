using Content.Client._RMC14.Chat;

namespace Content.Client.UserInterface.Systems.Chat.Widgets;

public partial class ChatBox
{
    public readonly Queue<RepeatedMessage> RepeatQueue = new();
}
