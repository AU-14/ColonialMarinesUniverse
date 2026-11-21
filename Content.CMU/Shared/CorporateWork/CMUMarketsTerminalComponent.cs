namespace Content.Shared.CMU14.CorporateWork;

/// <summary>
///     A way into the commodity markets. The corporate terminal trades the corporate budget and
///     reads the news as it breaks; the civilian version trades the user's own bank account, with
///     a smaller holdings cap, and only sees the news shortly before the move.
/// </summary>
[RegisterComponent]
public sealed partial class CMUMarketsTerminalComponent : Component
{
    [DataField]
    public bool Corporate = true;
}
