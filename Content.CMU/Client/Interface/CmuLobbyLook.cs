using Content.Client.Lobby.UI;
using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;
using Robust.Shared.Maths;

namespace Content.Client.CMU14.Interface;

/// <summary>
///     Dresses the lobby's right column as the molded terminal the chat and guidebook wear, following
///     <c>cmu.chat_housing</c>. With the housing off every control goes back to its plain CRT look.
/// </summary>
public static class CmuLobbyLook
{
    // Panels go through PanelOverride, not style classes: CrtLobbyTheme walks this same tree and a
    // class here would tie with it at equal specificity.

    private const string HousingPath = "/Textures/CMU14/Interface/ChatHousing";

    public static void Apply(LobbyGui lobby)
    {
        // The cvar, never StyleNano.ChatHousing: LobbyGui is built before StylesheetManager corrects
        // that static, so reading it here bakes "no housing" into an override nothing later clears.
        var tone = IoCManager.Resolve<IConfigurationManager>().GetCVar(CCVars.CMUChatHousing);
        var on = CmuHousingPalette.TryGet(tone, out var plastic);
        var cache = IoCManager.Resolve<IResourceCache>();

        lobby.RightSide.PanelOverride = on ? Housing(cache, tone) : null;

        // A screen each, with the seam a sibling: one screen wrapping both zones puts glass behind the seam.
        lobby.LobbyScreen.PanelOverride = on ? Screen(cache, tone, roundedTop: true) : null;
        lobby.ChatScreen.PanelOverride = on ? Screen(cache, tone, roundedTop: false) : null;
        lobby.LobbySeam.Visible = on;
        lobby.LobbySeam.PanelOverride = on ? Seam(cache, tone) : null;

        // The character page's own head rule and leader, matching the round-info page's.
        lobby.CharacterPreview.HeadLeaderPanel.PanelOverride = on || StyleNano.CrtUiEnabled
            ? new CmuDashedRuleStyleBox
            {
                Color = CrtTerminalPalette.Line,
                DashLength = 1,
                GapLength = 3,
                Thickness = 1,
            }
            : null;

        // Printed on the plastic, so it takes the tone's ink rather than the screen's phosphor.
        lobby.ServerName.FontColorOverride = on ? plastic.Ink : null;
        lobby.PlateSerial.Visible = on;
        lobby.PlateSerial.FontColorOverride = on ? plastic.InkDim : null;
        lobby.PlateSerial.FontOverride = on ? StyleNano.GetCrtFont(cache, 8) : null;

        // One block at a time, chosen on the switch. With the housing off both stay stacked.
        lobby.PanelSwitch.Visible = on;
        lobby.PanelSwitch.PanelOverride = on ? Track(plastic) : null;
        lobby.ServerInfoScreen.Visible = !on || lobby.ShowingServerPanel;
        lobby.CharacterPreview.Visible = !on || !lobby.ShowingServerPanel;

        SwitchHalf(lobby.ServerPanelButton, plastic, on, lobby.ShowingServerPanel);
        SwitchHalf(lobby.CharacterPanelButton, plastic, on, !lobby.ShowingServerPanel);

        // Same see-through treatment as the in-round housed chat. Without it the chat draws its own
        // ground inset from the screen edges, leaving a ring of glass around it on all four sides.
        lobby.Chat.OnLobbyHousingScreen = on;
        lobby.Chat.ChatWindowPanel.PanelOverride =
            on ? new StyleBoxFlat { BackgroundColor = Color.Transparent } : null;

        // The collapse arrow's wrapper takes the plastic directly: a TextureButton has no box to restyle.
        lobby.CommandBand.PanelOverride = on ? new StyleBoxFlat { BackgroundColor = Color.Transparent } : null;
        lobby.CollapseCell.PanelOverride = on ? KeyCap(plastic) : null;

        foreach (var key in new Button[]
                 { lobby.AHelpButton, lobby.CallVoteButton, lobby.OptionsButton, lobby.LeaveButton })
        {
            // CrtLobbyTheme withholds its own button class from anything wearing CrtCommandCell, so
            // swapping the two leaves exactly one rule matching in either direction.
            if (on)
            {
                key.RemoveStyleClass(StyleNano.StyleClassCrtCommandCell);
                key.AddStyleClass(StyleNano.StyleClassCmuHousingKey);
            }
            else
            {
                key.RemoveStyleClass(StyleNano.StyleClassCmuHousingKey);
                key.AddStyleClass(StyleNano.StyleClassCrtCommandCell);
            }
        }
    }

    private static StyleBox Screen(IResourceCache cache, string tone, bool roundedTop)
    {
        var name = roundedTop ? "screen_top" : "screen_bottom";
        var box = new StyleBoxTexture
        {
            Texture = cache.GetTexture($"{HousingPath}/{name}_{tone}.png"),
        };

        box.SetPatchMargin(StyleBox.Margin.All, 28);
        box.SetContentMarginOverride(StyleBox.Margin.All, 11);
        return box;
    }

    private static StyleBox Seam(IResourceCache cache, string tone)
    {
        return new StyleBoxTexture
        {
            Texture = cache.GetTexture($"{HousingPath}/seam_{tone}.png"),
            Mode = StyleBoxTexture.StretchMode.Tile,
        };
    }

    private static StyleBox Track(CmuHousingTone tone)
    {
        return new CmuBevelStyleBox
        {
            BackgroundColor = tone.KeyShadow,
            TopColor = tone.KeyShadow,
            BottomColor = tone.KeyHighlight,
            TopThickness = 2,
            BottomThickness = 1,
            ContentMarginLeftOverride = 3,
            ContentMarginRightOverride = 3,
            ContentMarginTopOverride = 3,
            ContentMarginBottomOverride = 3,
        };
    }

    private static void SwitchHalf(Button half, CmuHousingTone tone, bool on, bool active)
    {
        if (!on)
        {
            half.StyleBoxOverride = null;
            half.Label.FontColorOverride = null;
            return;
        }

        half.StyleBoxOverride = active
            ? new CmuBevelStyleBox
            {
                BackgroundColor = tone.KeyFill,
                TopColor = tone.KeyHighlight,
                BottomColor = tone.KeyShadow,
                TopThickness = 1,
                BottomThickness = 3,
                ContentMarginTopOverride = 4,
                ContentMarginBottomOverride = 4,
            }
            : new StyleBoxFlat
            {
                BackgroundColor = Color.Transparent,
                ContentMarginTopOverride = 4,
                ContentMarginBottomOverride = 4,
            };

        half.Label.FontColorOverride = active ? tone.KeyInk : tone.InkDim;
    }

    private static StyleBox KeyCap(CmuHousingTone tone)
    {
        return new CmuBevelStyleBox
        {
            BackgroundColor = tone.KeyFill,
            TopColor = tone.KeyHighlight,
            BottomColor = tone.KeyShadow,
            TopThickness = 1,
            BottomThickness = 3,
        };
    }

    private static StyleBox Housing(IResourceCache cache, string tone)
    {
        var box = new StyleBoxTexture
        {
            Texture = cache.GetTexture($"{HousingPath}/housing_{tone}.png"),
        };
        box.SetPatchMargin(StyleBox.Margin.All, 10);
        box.SetContentMarginOverride(StyleBox.Margin.Horizontal, 12);
        box.SetContentMarginOverride(StyleBox.Margin.Top, 9);
        box.SetContentMarginOverride(StyleBox.Margin.Bottom, 12);
        return box;
    }
}
