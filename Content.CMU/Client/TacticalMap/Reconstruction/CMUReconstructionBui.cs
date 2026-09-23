using System.Threading;
using System.Linq;
using Content.Shared._RMC14.TacticalMap;
using Content.Client._RMC14.TacticalMap;
using Content.Client._RMC14.UserInterface;
using Content.Shared.CCVar;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Client.CMU14.TacticalMap.Reconstruction;

[UsedImplicitly]
public class CMUReconstructionBui(EntityUid owner, Enum uiKey) : RMCPopOutBui<TacticalMapWindow>(owner, uiKey)
{
    protected override TacticalMapWindow? Window { get; set; }
    protected bool UsingReconstruction { get; private set; }
    protected virtual void OpenClassicWindow() { }
    [Dependency] private IConfigurationManager _cfg = default!;
    private CMUReconstructionWindow? _window;
    private CancellationTokenSource? _surveyRetry;
    private EntityUid? _actor;
    private bool _remembered;
    private int _requestId;
    private CMUReconMapChoice _choice;
    private bool _explicitChoice;
    private bool _classic;
    private TacticalMapLine[] _classicLines = [];
    private Dictionary<Vector2i, string> _classicLabels = new();

    protected bool KeepClassicDraft(TacticalMapControl canvas, TacticalMapControl map)
    {
        static bool LabelsEqual(Dictionary<Vector2i, string> a, Dictionary<Vector2i, string> b) =>
            a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out var value) && p.Value == value);
        var unchanged = SameLines(canvas.Lines, _classicLines) && LabelsEqual(canvas.TacticalLabels, _classicLabels);
        var acknowledged = SameLines(canvas.Lines, map.Lines) && LabelsEqual(canvas.TacticalLabels, map.TacticalLabels);
        return !unchanged && !acknowledged;
    }

    private static bool SameLines(IReadOnlyList<TacticalMapLine> a, IReadOnlyList<TacticalMapLine> b)
    {
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
        {
            var left = a[i]; var right = b[i];
            if (left.Start != right.Start || left.End != right.End || left.Color != right.Color ||
                left.Thickness != right.Thickness || left.Depth != right.Depth) return false;
            if (left.WorldPoints == null || right.WorldPoints == null)
            {
                if (left.WorldPoints != right.WorldPoints) return false;
            }
            else if (!left.WorldPoints.SequenceEqual(right.WorldPoints)) return false;
        }
        return true;
    }

    protected void RememberClassicCanvas(TacticalMapControl canvas, TacticalMapControl map)
    {
        canvas.UpdateTacticalLabels(map.TacticalLabels);
        _classicLines = canvas.Lines.ToArray();
        _classicLabels = new Dictionary<Vector2i, string>(canvas.TacticalLabels);
    }

    protected override void Open()
    {
        base.Open();
        _remembered = false;
        _choice = CMUReconMapChoice.Automatic;
        _explicitChoice = false;
        _classic = _cfg.GetCVar(CCVars.CMUTacMapClassic);
        UsingReconstruction = !_classic;
        // Existing actions and computers keep their normal BUI key and classic implementation.
        if (_classic && UiKey is not CMUReconstructionUiKey)
            return;
        _actor = PlayerManager.LocalEntity;
        if (!_classic)
        {
            _window = this.CreateWindow<CMUReconstructionWindow>();
            _window.CenterOnOpening = _cfg.GetCVar(CCVars.CMUTacMapCenterOnOpen);
            if (_actor is { } actor && EntMan.System<CMUReconstructionCacheSystem>().TryTake(Owner, actor, out var scene, out var camera,
                    _cfg.GetCVar(CCVars.CMUTacMapPlanetOnShip)))
                _window.RestoreCached(scene, camera);
            _window.OnRoute += SendMessage;
            _window.OnSend += SendMessage;
            _window.OnCancelOrder += SendMessage;
            _window.OnClear += () => SendMessage(new CMUReconClearOrdersMessage());
            _window.OnClose += StopSurveyRetry;
            _window.OnClose += Remember;
            _window.OnMapSelected += SelectMap;
            _requestId = EntMan.System<CMUReconstructionCacheSystem>().NextRequestId();
            _window.BeginViewRequest(_requestId, keepScene: true);
        }
        StartSurveyRetry();
    }

    private void StartSurveyRetry()
    {
        StopSurveyRetry();
        _surveyRetry = new CancellationTokenSource();
        // A predicted reopen can send before the server subscribes us, or lose the reply while
        // the old window closes. Keep requesting metadata until this window receives a baseline.
        Timer.SpawnRepeating(TimeSpan.FromSeconds(2), RequestSurvey, _surveyRetry.Token);
        RequestSurvey();
    }

    private void RequestSurvey()
    {
        if (!IsOpened) return;
        if (_classic) SendMessage(new CMUReconClassicMessage());
        else if (_window is { Disposed: false })
            SendMessage(new CMUReconViewMessage(Vector2i.Zero)
            {
                RequestId = _requestId, MapChoice = _choice,
                PreferPlanetOnShip = _cfg.GetCVar(CCVars.CMUTacMapPlanetOnShip),
                AtlasId = _window.SurveyView.Scene?.AtlasId ?? 0,
                Revisions = _window.SurveyView.Scene?.Revisions ?? [],
                SurfaceCount = _window.SurveyView.Scene?.Surfaces.Length ?? 0,
            });
    }

    private void SelectMap(CMUReconMapChoice choice)
    {
        _choice = choice;
        _explicitChoice = true;
        _requestId = EntMan.System<CMUReconstructionCacheSystem>().NextRequestId();
        _window?.BeginViewRequest(_requestId);
        StartSurveyRetry();
    }

    private void StopSurveyRetry()
    {
        _surveyRetry?.Cancel();
        _surveyRetry?.Dispose();
        _surveyRetry = null;
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);
        if (message is CMUReconSnapshotMessage snapshot)
        {
            if (snapshot.RequestId != _requestId) return;
            if (_explicitChoice && snapshot.AboardShip)
            {
                _cfg.SetCVar(CCVars.CMUTacMapPlanetOnShip, snapshot.MapChoice == CMUReconMapChoice.Planet);
                _cfg.SaveToFile();
            }
            _explicitChoice = false;
        }
        if (message is CMUReconFeedbackMessage { LocalizationKey: "cmu-recon-no-map" } feedback && feedback.RequestId != _requestId)
            return;
        _window?.Receive(message);
        if (message is CMUReconSnapshotMessage or CMUReconFeedbackMessage { LocalizationKey: "cmu-recon-no-map" })
            StopSurveyRetry();
        if (message is CMUReconFeedbackMessage { LocalizationKey: "cmu-recon-no-map" } &&
            UiKey is not CMUReconstructionUiKey && UsingReconstruction)
        {
            _window?.Dispose();
            _window = null;
            UsingReconstruction = false;
            OpenClassicWindow();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Remember();
            StopSurveyRetry();
        }
        base.Dispose(disposing);
    }

    private void Remember()
    {
        if (_remembered || _actor is not { } actor || PlayerManager.LocalEntity != actor || _window?.SurveyView.Scene is not { } scene)
            return;
        _remembered = true;
        EntMan.System<CMUReconstructionCacheSystem>().Remember(Owner, actor, scene, _window.SurveyView.CaptureCamera());
    }
}
