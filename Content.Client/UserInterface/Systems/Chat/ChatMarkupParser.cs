using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Chat;

internal static class ChatMarkupParser
{
    public static string? AddMarkup(FormattedMessage message, string markup)
    {
        if (message.TryAddMarkup(markup, out var error))
            return null;

        message.AddMarkupPermissive(markup);
        return error;
    }
}
