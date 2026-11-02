namespace Content.Shared.Database;

[Flags]
public enum ChatType
{
    None = 0,
    Dead = 1,
    Looc = 1 << 1,
    Ooc = 1 << 2,
    // cmu edit start: voice chat bans
    Voice = 1 << 3,
    // cmu edit end
}
