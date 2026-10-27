using Content.Client.CMU14.Lobby;
using Robust.Client.UserInterface;

namespace Content.Client.Lobby.UI;

public sealed partial class LobbyGui
{
    private CmuCharacterSetupLayout? _characterSetupLayout;
    private Control? _characterSetupParent;
    private Control? _characterSetupChatParent;
    private float _lobbyChatMinWidth;

    private void SetCharacterSetupLayout(bool editing)
    {
        if (_characterSetupLayout == null)
        {
            if (!editing)
                return;

            _characterSetupParent = CharacterSetupState.Parent!;
            _characterSetupChatParent = ChatCrtScreen.Parent!;
            _characterSetupLayout = new CmuCharacterSetupLayout(CharacterSetupState, ChatCrtScreen)
            {
                HorizontalExpand = true,
                VerticalExpand = true,
                Visible = false,
            };
            MainContainer.AddChild(_characterSetupLayout);
            _characterSetupLayout.SetPositionFirst();
        }

        if (_characterSetupLayout.Visible == editing)
            return;

        if (editing)
        {
            // Reuse the live chat, including its messages, filters, splits and input text.
            _lobbyChatMinWidth = Chat.MinWidth;
            Chat.MinWidth = 0;
            CharacterSetupState.HorizontalExpand = true;
            CharacterSetupState.Orphan();
            ChatCrtScreen.Orphan();
            _characterSetupLayout.AddChild(CharacterSetupState);
            _characterSetupLayout.AddChild(ChatCrtScreen);
        }
        else
        {
            CharacterSetupState.Orphan();
            ChatCrtScreen.Orphan();
            _characterSetupParent!.AddChild(CharacterSetupState);
            _characterSetupChatParent!.AddChild(ChatCrtScreen);
            Chat.MinWidth = _lobbyChatMinWidth;
        }

        LeftSide.Parent!.Visible = !editing;
        _characterSetupLayout.Visible = editing;
        ExpandPanel.Visible = false;
    }
}
