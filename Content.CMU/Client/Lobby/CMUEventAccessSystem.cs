using System.Linq;
using Content.Client.Players.PlayTimeTracking;
using Content.Shared.CMU14.Lobby;
using Content.Shared.CMU14.Round.Roles;
using Content.Shared.Preferences;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Client;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Lobby;

public sealed partial class CMUEventAccessSystem : EntitySystem
{
    [Dependency] private IBaseClient _client = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private JobRequirementsManager _requirements = default!;

    private HashSet<ProtoId<JobPrototype>> _locked = new();
    private CMUEventCodeWindow? _window;
    public bool Enabled { get; private set; }
    public event Action? Updated;

    public bool IsLocked(ProtoId<JobPrototype> job) => _locked.Contains(job);

    public bool CanAccessSide(RoundJobSide side) => !Enabled ||
        CMUEventJobCatalog.GetJobs(_prototypes).Any(job => job.RoundSide == side && !IsLocked(job.ID));

    public ForceOnForceSide EffectiveSide(ForceOnForceSide preferred)
    {
        var govfor = CanAccessSide(RoundJobSide.Govfor);
        var opfor = CanAccessSide(RoundJobSide.Opfor);
        return govfor == opfor ? preferred : govfor ? ForceOnForceSide.Govfor : ForceOnForceSide.Opfor;
    }

    public void RequestCode(string code) => RaiseNetworkEvent(new CMUEventCodeRequestEvent(code));

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CMUEventAccessStateEvent>(OnState);
        SubscribeNetworkEvent<CMUEventCodeResultEvent>(OnResult);
        SubscribeNetworkEvent<TickerJoinGameEvent>(_ => _window?.Close());
        _client.RunLevelChanged += OnRunLevelChanged;
    }

    public override void Shutdown()
    {
        _client.RunLevelChanged -= OnRunLevelChanged;
        _window?.Close();
        base.Shutdown();
    }

    public void OpenCodeWindow()
    {
        if (!Enabled)
            return;
        if (_window != null)
        {
            _window.MoveToFront();
            return;
        }

        var window = new CMUEventCodeWindow();
        _window = window;
        window.SubmitButton.OnPressed += _ => Submit(window);
        window.CodeInput.OnTextEntered += _ => Submit(window);
        window.OnClose += () => _window = null;
        window.OpenCentered();
        window.CodeInput.GrabKeyboardFocus();
    }

    private void Submit(CMUEventCodeWindow window)
    {
        var code = window.CodeInput.Text;
        window.CodeInput.Text = string.Empty;
        if (string.IsNullOrWhiteSpace(code) || code.Length > CMUEventCodeRequestEvent.MaxCodeLength)
        {
            window.StatusLabel.SetMessage(FormattedMessage.FromUnformatted(Loc.GetString("cmu-event-code-invalid")));
            return;
        }

        window.StatusLabel.SetMessage(FormattedMessage.FromUnformatted(Loc.GetString("cmu-event-code-checking")));
        RequestCode(code);
    }

    private void OnState(CMUEventAccessStateEvent ev)
    {
        Enabled = ev.Enabled;
        _locked = new HashSet<ProtoId<JobPrototype>>(ev.LockedJobs);
        if (!Enabled)
            _window?.Close();
        _requirements.RefreshEventAccess();
        Updated?.Invoke();
    }

    private void OnResult(CMUEventCodeResultEvent ev)
    {
        _window?.StatusLabel.SetMessage(FormattedMessage.FromUnformatted(ev.Message));
    }

    private void OnRunLevelChanged(object? sender, RunLevelChangedEventArgs args)
    {
        if (args.NewLevel != ClientRunLevel.Initialize)
            return;

        _locked.Clear();
        Enabled = false;
        _window?.Close();
        _requirements.RefreshEventAccess();
        Updated?.Invoke();
    }
}
