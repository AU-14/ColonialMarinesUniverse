using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Numerics;
using Content.Shared.CMU14.Fighter;
using Content.Shared.CMU14.ZLevels;
using Content.Client.Viewport;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.ContentPack;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;

namespace Content.Client.CMU14.Fighter;

public sealed partial class FighterClientSystem
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IResourceManager _resources = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    private bool _trial;
    private int _trialStep;
    private TimeSpan? _trialStart;
    private TimeSpan? _trialPassStart;
    private bool _trialImpactSeen;
    private bool _trialImpactLost;
    private bool _trialPanelsShown;
    private bool _trialPanelsCaptured;
    private int _trialCapturePending;

    private void OnTrialRequested(bool requested)
    {
        if (requested)
            StartTrial();
        else _trial = false;
    }

    public void StartTrial()
    {
        _trial = true;
        _trialStep = 0;
        _trialStart = null;
        _trialPassStart = null;
        _trialImpactSeen = false;
        _trialImpactLost = false;
        _trialPanelsShown = false;
        _trialPanelsCaptured = false;
        _airTrialIncoming = null;
        _airTrialFlares = false;
        _effectsSecondPass = false;
        _effectsCaptured.Clear();
        _strikeCaptured.Clear();
        _impactCaptured.Clear();
        _manpadCaptureStep = -1;
        ResetPerformanceTrial();
    }

    private void UpdateTrial(FighterAircraftComponent aircraft, FighterSeatComponent seat)
    {
        CaptureWeaponPreview(aircraft, seat);
        if (!_trial)
            return;
        if (aircraft.GroundEntity != null)
        {
            UpdateGroundTrial(aircraft, seat);
            return;
        }
        if (_configuration.GetCVar(FighterTrialCVars.Manpad))
        {
            UpdateManpadTrial(aircraft, seat);
            return;
        }
        if (_configuration.GetCVar(FighterTrialCVars.Performance))
        {
            UpdatePerformanceTrial(aircraft, seat);
            return;
        }
        if (_configuration.GetCVar(FighterTrialCVars.Effects)) CaptureEffectsTrial(aircraft, seat);
        if (_configuration.GetCVar(FighterTrialCVars.AirCombat))
        {
            UpdateAirCombatTrial(aircraft, seat);
            return;
        }
        if (_configuration.GetCVar(FighterTrialCVars.Laser))
        {
            UpdateLaserTrial(aircraft, seat);
            return;
        }
        if (_configuration.GetCVar(FighterTrialCVars.Weapons))
        {
            UpdateWeaponTrial(aircraft, seat);
            return;
        }
        _trialStart ??= _timing.RealTime;
        var elapsed = (_timing.RealTime - _trialStart.Value).TotalSeconds;
        if (_trialStep == 0 && elapsed >= 8)
        {
            CaptureTrial(seat.Pilot ? "pilot-holding" : "observer-holding");
            if (seat.Pilot)
            {
                var half = Math.Min(160, aircraft.Battlefield.Width * .4f);
                RaiseNetworkEvent(new FighterPlanEvent(aircraft.Home + new Vector2(-half, 30), aircraft.Home + new Vector2(half, -30)));
                RaiseNetworkEvent(new FighterSettingsEvent(350, 16));
            }
            _trialStep = 1;
        }
        if (_trialStep == 1 && elapsed >= 8.5 && !_trialPanelsShown)
        {
            _trialPanelsShown = true;
            _display?.ShowRoute(seat.Pilot);
            _display?.ShowOptics(!seat.Pilot);
        }
        if (_trialStep == 1 && elapsed >= 9.5 && !_trialPanelsCaptured)
        {
            _trialPanelsCaptured = true;
            CaptureTrial(seat.Pilot ? "pilot-route" : "observer-sight");
        }
        if (_trialStep == 1 && elapsed >= 10)
        {
            _display?.ShowRoute(false);
            _display?.ShowOptics(false);
            if (seat.Pilot) RaiseNetworkEvent(new FighterCommandEvent(FighterCommand.Launch));
            _trialStep = 2;
        }
        if (aircraft.Phase == FighterPhase.Pass)
        {
            _trialPassStart ??= _timing.RealTime;
            if (aircraft.TrainingImpact != null)
                _trialImpactSeen = true;
            else if (_trialImpactSeen)
                _trialImpactLost = true;
        }
        if (_trialPassStart is not { } start) return;
        var passTime = (_timing.RealTime - start).TotalSeconds;
        // Mark and release while the short pass is still low and over the battlefield.
        var deadlines = new[] { 1.0, 1.3, 1.6, 2, 3, 6, 7, 11, 12 };
        var index = _trialStep - 2;
        if (index >= 0 && index < deadlines.Length && passTime >= deadlines[index])
        {
            _trialStep++;
            switch (index)
            {
                case 0: CaptureTrial(seat.Pilot ? "pilot-low" : "observer-low"); break;
                case 1 when !seat.Pilot:
                    _display?.ShowOptics(true);
                    RaiseNetworkEvent(new FighterCommandEvent(FighterCommand.Mark));
                    break;
                case 2 when !seat.Pilot: RaiseNetworkEvent(new FighterCommandEvent(FighterCommand.TrainingRelease)); break;
                case 3: CaptureTrial(seat.Pilot ? "pilot-mark" : "observer-mark"); break;
                case 4 when seat.Pilot: RaiseNetworkEvent(new FighterSettingsEvent(700, 16)); break;
                case 5: CaptureTrial(seat.Pilot ? "pilot-cloud" : "observer-cloud"); break;
                case 6 when seat.Pilot: RaiseNetworkEvent(new FighterSettingsEvent(1200, 16)); break;
                case 7: CaptureTrial(seat.Pilot ? "pilot-high" : "observer-high"); break;
                case 8 when seat.Pilot: RaiseNetworkEvent(new FighterSettingsEvent(350, 16)); break;
            }
        }
        if (aircraft.Phase == FighterPhase.Return && _trialStep == 11)
        {
            CaptureTrial(seat.Pilot ? "pilot-return" : "observer-return");
            _trialStep++;
        }
        if (_trialStep < 12 || aircraft.Phase != FighterPhase.Holding) return;
        _trial = false;
        _configuration.SetCVar(FighterTrialCVars.Enabled, false);
        _display?.ShowOptics(false);
        _input = FighterInput.None;
        Log.Info($"Fighter route trial completed; impact observed={_trialImpactSeen}, impact retained={!_trialImpactLost}; controls returned to the player.");
    }

    private void CaptureTrial(string name)
    {
        // PNG encoding used to run in the render callback and hitch the live test,
        // especially when both 4K clients captured a release at the same time.
        if (Interlocked.Increment(ref _trialCapturePending) > 2)
        {
            Interlocked.Decrement(ref _trialCapturePending);
            return;
        }
        if (TryGetSeat(out var seat) && seat.Comp.Aircraft is { } uid && TryComp(uid, out FighterAircraftComponent? flight))
        {
            Log.Info($"Fighter trial snapshot {name}: phase={flight.Phase}, height={flight.Height:F0}, mark={flight.Mark}, impact={flight.TrainingImpact}, pass={flight.PassNumber}.");
            if (_configuration.GetCVar(CMUZLevelsCVars.ClientDiagnosticsEnabled))
            {
                foreach (var map in new[] { flight.TerrainMap, flight.ViewMap })
                {
                    if (TryComp(map, out MapLightComponent? light))
                        Log.Info($"Fighter terrain lighting {name}: map={ToPrettyString(map)}, ambient={light.AmbientLightColor}.");
                }
            }
        }
        if (_configuration.GetCVar(CMUZLevelsCVars.ClientDiagnosticsEnabled))
        {
            var stats = ScalingViewport.LastZRenderDebugStats;
            Log.Info($"Fighter terrain render {name}: result={stats.SkipReason}, map={stats.BaseMapId}, lowerMap={stats.HasLowerMap}, depth={stats.LowestDepth}, lowerPasses={stats.LowerPassesRendered}, openings={stats.OpeningsAfterLos}.");
        }
        _clyde.Screenshot(ScreenshotType.Final, image =>
        {
            try
            {
                var directory = new ResPath("/Screenshots");
                _resources.UserData.CreateDir(directory);
                var file = _resources.UserData.Open(directory / ("fighter-" + name + ".png"), FileMode.Create);
                _ = Task.Run(() =>
                {
                    try
                    {
                        using (image)
                        using (file) image.SaveAsPng(file);
                        Log.Info($"Fighter trial screenshot saved: fighter-{name}.png");
                    }
                    catch (Exception e) { Log.Warning($"Fighter trial capture failed: {e.Message}"); }
                    finally { Interlocked.Decrement(ref _trialCapturePending); }
                });
            }
            catch (Exception e)
            {
                image.Dispose();
                Interlocked.Decrement(ref _trialCapturePending);
                Log.Warning($"Fighter trial capture failed: {e.Message}");
            }
        });
    }
}

/// <summary>Opt-in local visual check, useful with +fighter_trial on a test client.</summary>
public sealed class FighterTrialCommand : IConsoleCommand
{
    public string Command => "fighter_trial";
    public string Description => "Run a short fighter visual trial and save cockpit screenshots.";
    public string Help => "fighter_trial: waits for a fighter seat, flies a short pass and returns the test aircraft to its starting point.";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        IoCManager.Resolve<IConfigurationManager>().SetCVar(FighterTrialCVars.Enabled, true);
        shell.WriteLine("Fighter visual trial queued; it starts when a cockpit view is available.");
    }
}

[CVarDefs]
public sealed class FighterTrialCVars
{
    public static readonly CVarDef<bool> Enabled = CVarDef.Create("fighter.visual_trial", false, CVar.CLIENTONLY);
    public static readonly CVarDef<bool> Weapons = CVarDef.Create("fighter.weapon_trial", false, CVar.CLIENTONLY);
    public static readonly CVarDef<bool> Laser = CVarDef.Create("fighter.laser_trial", false, CVar.CLIENTONLY);
    public static readonly CVarDef<bool> AirCombat = CVarDef.Create("fighter.air_combat_trial", false, CVar.CLIENTONLY);
    public static readonly CVarDef<bool> Effects = CVarDef.Create("fighter.effects_trial", false, CVar.CLIENTONLY);
    public static readonly CVarDef<bool> Manpad = CVarDef.Create("fighter.manpad_trial", false, CVar.CLIENTONLY);
    public static readonly CVarDef<bool> Performance = CVarDef.Create("fighter.performance_trial", false, CVar.CLIENTONLY);
}
