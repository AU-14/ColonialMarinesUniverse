namespace Content.Shared._CMU14.Chat;

public static class CMURunechatStyles
{
    public const string Pain = "runechatPain";
    public const string Scream = "runechatScream";

    public static bool IsInterrupting(string? style)
    {
        return style is Pain or Scream;
    }
}
