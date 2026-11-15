// SPDX-License-Identifier: AGPL-3.0-only
namespace Content.Shared.CMU14.Government;

// The card of a licence that the mayor has given (see ColonyLicensePrototype): whose it is, what for, the number of it, until when, and whether the mayor has taken it back. The law officers look at it
// (what it says when it is examined), and write the fines themselves. Whether it has run out is told by the clock, so that the card needs no tick of its own.
[RegisterComponent]
public sealed partial class CityLicenseCardComponent : Component
{
    public string Serial = string.Empty;
    public string TypeName = string.Empty;
    public string Holder = string.Empty;
    public TimeSpan ExpiresAt;
    public bool Revoked;

    // A deed of a plot has no end of its own: it holds as long as the plot is the holder's.
    public bool Permanent;
}
