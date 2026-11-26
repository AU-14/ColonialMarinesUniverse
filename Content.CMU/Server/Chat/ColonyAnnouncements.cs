using Robust.Shared.Player;

namespace Content.Server.CMU14.Chat;

public static class ColonyAnnouncements
{
    public static Filter Recipients() =>
        Filter.Empty().AddWhereAttachedEntity(_ => true);
}
