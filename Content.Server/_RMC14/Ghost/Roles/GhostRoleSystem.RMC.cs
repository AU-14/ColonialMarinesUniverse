using Content.Server._RMC14.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Ghost.Roles.Raffles;

namespace Content.Server.Ghost.Roles;

public sealed partial class GhostRoleSystem
{
    private void ApplyRMCRaffleSettings(
        Entity<GhostRoleRaffleComponent> raffle,
        GhostRoleRaffleSettings settings)
    {
        var ev = new GhostRoleRaffleEvent(
            raffle.Comp.Countdown,
            settings.RoundTimeRequirement);
        RaiseLocalEvent(raffle, ref ev);

        if (ev.Handled)
            raffle.Comp.Countdown = ev.CountDown;
    }
}
