using System;
using System.Collections.Generic;
using Content.Shared._CMU14.Yautja;

namespace Content.Server._CMU14.Yautja;

/// <summary>
/// Holds the last completed state for the clan administration EUI.
/// Database work must never be performed from <c>GetNewState</c>, which is called
/// synchronously by the EUI manager on the server tick.
/// </summary>
public sealed class YautjaClanAdminStateStore
{
    private readonly object _sync = new();
    private (int ClanId, YautjaClanAdminMutationKind Kind, string StatusMessage)? _pendingMutation;
    private bool _acknowledgementAwaitingDelivery;
    private YautjaClanAdminEuiState _state = new(
        [],
        "",
        "",
        "",
        0,
        null,
        YautjaClanAdminMutationKind.None);

    public bool CanStartMutation
    {
        get
        {
            lock (_sync)
            {
                return _pendingMutation == null && !_acknowledgementAwaitingDelivery;
            }
        }
    }

    public bool NeedsMutationRecovery
    {
        get
        {
            lock (_sync)
            {
                return _pendingMutation != null;
            }
        }
    }

    public YautjaClanAdminEuiState Get()
    {
        lock (_sync)
        {
            return _state;
        }
    }

    public YautjaClanAdminEuiState GetForDelivery()
    {
        lock (_sync)
        {
            _acknowledgementAwaitingDelivery = false;
            return _state;
        }
    }

    public void Set(YautjaClanAdminEuiState state)
    {
        lock (_sync)
        {
            _state = state;
        }
    }

    public void StageMutation(int clanId, YautjaClanAdminMutationKind kind, string statusMessage)
    {
        lock (_sync)
        {
            if (kind == YautjaClanAdminMutationKind.None)
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (_pendingMutation != null || _acknowledgementAwaitingDelivery)
                throw new InvalidOperationException("A previous clan mutation is still awaiting state delivery.");

            _pendingMutation = (clanId, kind, statusMessage);
        }
    }

    public YautjaClanAdminEuiState PublishFreshSnapshot(
        List<YautjaClanAdminClanState> clans,
        string inspectedPlayer,
        string inspectedSummary,
        string statusMessage,
        List<YautjaClanAdminMemberState>? clanlessPlayers = null)
    {
        lock (_sync)
        {
            var version = _state.ClanMutationVersion;
            var lastMutatedClanId = _state.LastMutatedClanId;
            var lastMutationKind = _state.LastMutationKind;
            var pending = _pendingMutation;

            if (pending is { } mutation)
            {
                version++;
                lastMutatedClanId = mutation.ClanId;
                lastMutationKind = mutation.Kind;
                statusMessage = mutation.StatusMessage;
            }

            var state = new YautjaClanAdminEuiState(
                clans,
                inspectedPlayer,
                inspectedSummary,
                statusMessage,
                version,
                lastMutatedClanId,
                lastMutationKind,
                clanlessPlayers);
            _state = state;

            if (pending != null)
            {
                _acknowledgementAwaitingDelivery = true;
                _pendingMutation = null;
            }

            return state;
        }
    }

    public YautjaClanAdminEuiState PublishRefreshFailure(string statusMessage)
    {
        lock (_sync)
        {
            var state = new YautjaClanAdminEuiState(
                _state.Clans,
                _state.InspectedPlayer,
                _state.InspectedSummary,
                statusMessage,
                _state.ClanMutationVersion,
                _state.LastMutatedClanId,
                _state.LastMutationKind,
                _state.ClanlessPlayers);
            _state = state;
            return state;
        }
    }
}
