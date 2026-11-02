using System.Diagnostics.CodeAnalysis;
using Content.Client.Guidebook.Richtext;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.Fob;

/// <summary>Localized public pilot instructions, shared by Marine and xeno equipment help.</summary>
[UsedImplicitly]
public sealed class CMUFobGuideText : RichTextLabel, IDocumentTag
{
    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        if (!args.TryGetValue("Key", out var key)) return false;
        SetMessage(Loc.GetString(key));
        Margin = new Thickness(0, 0, 0, 16);
        control = this;
        return true;
    }
}
