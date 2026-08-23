using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Administration;
using Content.Shared.Eui;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaPredatorAdminEditorEui : BaseEui
{
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private IEntitySystemManager _systems = default!;

    private YautjaPredatorRoundSystem _yautja = default!;
    private string _statusMessage = Loc.GetString("cmu-yautja-admin-editor-ready");

    public YautjaPredatorAdminEditorEui()
    {
        IoCManager.InjectDependencies(this);
    }

    public override void Opened()
    {
        base.Opened();

        _yautja = _systems.GetEntitySystem<YautjaPredatorRoundSystem>();
        _admin.OnPermsChanged += OnAdminPermsChanged;

        if (!_admin.HasAdminFlag(Player, AdminFlags.Host))
        {
            Close();
            return;
        }

        StateDirty();
    }

    public override void Closed()
    {
        base.Closed();
        _admin.OnPermsChanged -= OnAdminPermsChanged;
    }

    public override EuiStateBase GetNewState()
    {
        var initialized = _yautja.TryGetActiveHunterSlots(out var activeHunterSlots);
        var hunterSlots = _yautja.ConfiguredHunterSlots;
        if (hunterSlots <= 0)
            hunterSlots = activeHunterSlots > 0 ? activeHunterSlots : 2;

        return new YautjaPredatorAdminEditorEuiState(
            _yautja.CurrentRoundId,
            _yautja.RoundActive,
            initialized,
            activeHunterSlots,
            hunterSlots,
            _yautja.RandomEnabled,
            _yautja.RandomMinimumRounds,
            _yautja.RandomMaximumRounds,
            _yautja.RandomRoundsRemaining,
            _statusMessage);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        if (!_admin.HasAdminFlag(Player, AdminFlags.Host))
        {
            Close();
            return;
        }

        base.HandleMessage(msg);

        switch (msg)
        {
            case YautjaPredatorAdminEditorInitializeMessage:
                _yautja.TryInitializePredatorRound(out _statusMessage);
                StateDirty();
                break;
            case YautjaPredatorAdminEditorSetHunterSlotsMessage slots:
                _yautja.TrySetHunterSlots(slots.Slots, out _statusMessage);
                StateDirty();
                break;
            case YautjaPredatorAdminEditorSetRandomMessage random:
                _yautja.TryConfigureRandom(
                    random.Enabled,
                    random.MinimumRounds,
                    random.MaximumRounds,
                    out _statusMessage);
                StateDirty();
                break;
            case YautjaPredatorAdminEditorRefreshMessage:
                StateDirty();
                break;
        }
    }

    private void OnAdminPermsChanged(AdminPermsChangedEventArgs args)
    {
        if (args.Player == Player && !_admin.HasAdminFlag(Player, AdminFlags.Host))
            Close();
    }
}
