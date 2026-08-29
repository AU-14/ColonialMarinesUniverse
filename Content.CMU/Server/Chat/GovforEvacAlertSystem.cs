using Content.Server.CMU14.Comms;
using Content.Shared._RMC14.Evacuation;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Chat;

/// <summary>
/// Broadcasts a colony alert when Govfor starts or cancels an evacuation from their command tablet,
/// so insurgents and colonists know how much time they have before Govfor departs and the round ends.
/// </summary>
public sealed class GovforEvacAlertSystem : EntitySystem
{
    [Dependency] private readonly ColonyCommsConsoleSystem _colonyComms = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly TimeSpan BroadcastCooldown = TimeSpan.FromMinutes(1);

    private TimeSpan _nextBroadcastAt;

    public override void Initialize()
    {
        SubscribeLocalEvent<EvacuationEnabledEvent>(OnEvacuationEnabled);
        SubscribeLocalEvent<EvacuationDisabledEvent>(OnEvacuationDisabled);
    }

    private void OnEvacuationEnabled(ref EvacuationEnabledEvent ev)
    {
        if (!TryComp<EvacuationProgressComponent>(ev.Map, out var progress) ||
            progress.DropShipCrashed ||
            !IsGovfor(progress.VictimFaction))
        {
            return;
        }

        var minutes = progress.SelfDestructAt is { } destructAt
            ? (int) Math.Ceiling((destructAt - _timing.CurTime).TotalMinutes)
            : 0;
        Broadcast(ev.Map, Loc.GetString("govfor-evac-started", ("minutes", minutes)));
    }

    private void OnEvacuationDisabled(ref EvacuationDisabledEvent ev)
    {
        if (!TryComp<EvacuationProgressComponent>(ev.Map, out var progress) ||
            !IsGovfor(progress.VictimFaction))
        {
            return;
        }

        Broadcast(ev.Map, Loc.GetString("govfor-evac-cancelled"));
    }

    private void Broadcast(EntityUid source, string message)
    {
        if (_timing.CurTime < _nextBroadcastAt)
            return;

        _nextBroadcastAt = _timing.CurTime + BroadcastCooldown;
        _colonyComms.BroadcastColonyAlert(source, message);
    }

    private static bool IsGovfor(string? faction) =>
        string.Equals(faction, "govfor", StringComparison.OrdinalIgnoreCase);
}
