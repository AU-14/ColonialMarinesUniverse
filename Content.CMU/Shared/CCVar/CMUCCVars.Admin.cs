// ReSharper disable CheckNamespace

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// When an admin claims or acknowledges an AHelp ticket, tell the player that admin's name.
    /// Stealthed admins are never named. Off: the player only hears that an admin is handling the ticket.
    /// </summary>
    public static readonly CVarDef<bool> CMUAhelpTicketShowClaimer =
        CVarDef.Create("cmu.ahelp_ticket_show_claimer", true, CVar.SERVERONLY | CVar.ARCHIVE);
}
