using System.Linq;
using Content.Client._CMU14.Interface;
using Content.Client.Resources;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Maths;

namespace Content.Client._CMU14.UserInterface.Guidebook;

/// <summary>
///     Dresses the guidebook as a reference terminal when the chat housing is on: the window body becomes
///     the molded housing, the page sits on one screen, and the contents are inked on the housing itself.
///     With the housing off every control goes back to its plain CRT look.
/// </summary>
public static class CmuGuidebookLook
{
    private const string HousingPath = "/Textures/_CMU14/Interface/ChatHousing";

    /// <summary>Colour for text drawn straight onto the housing, or null when there is no housing.</summary>
    public static Color? Ink =>
        CmuHousingPalette.TryGet(StyleNano.ChatHousing, out var tone) ? tone.Ink : null;

    public static void Apply(Control window)
    {
        var on = CmuHousingPalette.TryGet(StyleNano.ChatHousing, out var tone);
        var cache = IoCManager.Resolve<IResourceCache>();

        if (Body(window) is { } body)
        {
            body.PanelOverride = on ? Housing(cache) : null;

            // AngleRect multiplies the panel by a dark grey, which would tint the housing texture almost black.
            body.ModulateSelfOverride = on ? Color.White : null;
        }

        if (Find(window, "Housing") is PanelContainer housing)
            housing.PanelOverride = on ? new StyleBoxFlat { BackgroundColor = Color.Transparent } : null;

        if (Find(window, "Screen") is PanelContainer screen)
            screen.PanelOverride = on ? Screen(cache) : null;

        if (Find(window, "SearchSlot") is PanelContainer slot)
            slot.PanelOverride = on ? Slot(tone) : null;

        if (Find(window, "Groove") is PanelContainer groove)
        {
            groove.Visible = on;
            groove.PanelOverride = on ? Groove(tone) : null;
        }

        foreach (var name in new[] { "BackButton", "ForwardButton", "HomeButton" })
        {
            if (Find(window, name) is not Button key)
                continue;

            if (on)
            {
                key.RemoveStyleClass(StyleNano.StyleClassCrtButton);
                key.AddStyleClass(StyleNano.StyleClassCmuHousingKey);
            }
            else
            {
                key.RemoveStyleClass(StyleNano.StyleClassCmuHousingKey);
            }
        }

        foreach (var name in new[] { "Breadcrumb", "ContentsLabel" })
        {
            if (Find(window, name) is Label label)
                label.FontColorOverride = on ? tone.InkDim : null;
        }
    }

    private static PanelContainer? Body(Control window)
    {
        return window.Children.OfType<PanelContainer>().FirstOrDefault();
    }

    private static StyleBox Housing(IResourceCache cache)
    {
        var box = new StyleBoxTexture
        {
            Texture = cache.GetTexture($"{HousingPath}/housing_{StyleNano.ChatHousing}.png"),
        };
        box.SetPatchMargin(StyleBox.Margin.All, 10);
        box.SetContentMarginOverride(StyleBox.Margin.All, 10);
        return box;
    }

    private static StyleBox Screen(IResourceCache cache)
    {
        // The corner radius is baked into the texture; the padding keeps text off the curve.
        var box = new StyleBoxTexture
        {
            Texture = cache.GetTexture($"{HousingPath}/screen_{StyleNano.ChatHousing}.png"),
        };
        box.SetPatchMargin(StyleBox.Margin.All, 28);
        box.SetContentMarginOverride(StyleBox.Margin.All, 11);
        return box;
    }

    private static StyleBox Slot(CmuHousingTone tone)
    {
        return new CmuBevelStyleBox
        {
            BackgroundColor = CrtTerminalPalette.Void,
            TopColor = tone.KeyShadow,
            BottomColor = tone.KeyHighlight,
            TopThickness = 2,
            BottomThickness = 1,
            ContentMarginLeftOverride = 4,
            ContentMarginRightOverride = 4,
        };
    }

    private static StyleBox Groove(CmuHousingTone tone)
    {
        return new CmuBevelStyleBox
        {
            BackgroundColor = tone.KeyShadow,
            TopColor = tone.KeyShadow,
            BottomColor = tone.KeyHighlight,
            TopThickness = 1,
            BottomThickness = 1,
        };
    }

    private static Control? Find(Control parent, string name)
    {
        foreach (var child in parent.Children)
        {
            if (child.Name == name)
                return child;

            if (Find(child, name) is { } found)
                return found;
        }

        return null;
    }
}
