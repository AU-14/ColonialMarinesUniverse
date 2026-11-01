using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Client.Verbs.UI;

public sealed partial class VerbMenuElement
{
    private CMUVerbPresentation _cmuPresentation;
    internal VerbCategory? CMUCategory;
    internal string? CMUCategoryStyle;

    internal void CMURememberVerb(Verb verb)
    {
        _cmuPresentation = new CMUVerbPresentation(verb);
    }

    internal bool CMUMatches(Verb verb)
    {
        return ReferenceEquals(Verb, verb) && _cmuPresentation == new CMUVerbPresentation(verb);
    }

    private readonly record struct CMUVerbPresentation(
        string Text, string? Message, bool Disabled, string Style, SpriteSpecifier? Icon,
        NetEntity? IconEntity, bool Confirmation)
    {
        public CMUVerbPresentation(Verb verb) : this(verb.Text, verb.Message, verb.Disabled,
            verb.TextStyleClass, verb.Icon, verb.IconEntity, verb.ConfirmationPopup) { }
    }
}
