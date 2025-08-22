using Content.Shared.AU14.Objectives.Capture;
using Content.Shared._RMC14.Flag;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.IoC;
using Robust.Shared.Log;

namespace Content.Client.AU14.Objectives.Capture;

public sealed class ClientCaptureObjectiveSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly AppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly ILogManager _logManager = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logManager.GetSawmill("client-capture-obj");
        SubscribeLocalEvent<CaptureObjectiveComponent, ComponentStartup>(OnCaptureObjectiveStartup);
        SubscribeLocalEvent<CaptureObjectiveComponent, ComponentHandleState>(OnCaptureObjectiveState);
    }

    private void OnCaptureObjectiveStartup(EntityUid uid, CaptureObjectiveComponent comp, ref ComponentStartup args)
    {
        UpdateFlagSpriteState(uid, comp);
    }

    private void OnCaptureObjectiveState(EntityUid uid, CaptureObjectiveComponent comp, ref ComponentHandleState args)
    {
        UpdateFlagSpriteState(uid, comp);
    }

    private void UpdateFlagSpriteState(EntityUid flagUid, CaptureObjectiveComponent comp)
    {
        if (!_entManager.TryGetComponent(flagUid, out AppearanceComponent? _))
            return;
        var faction = comp.CurrentController.ToLowerInvariant();
        string? spriteState = null;

        if (faction == "govfor")
        {
            spriteState = comp.GovforFlagState;
        }
        else if (faction == "opfor")
        {
            spriteState = comp.OpforFlagState;
        }
        else if (faction == "clf")
        {
            spriteState = "clfflag";
        }
        if (string.IsNullOrEmpty(spriteState))
            spriteState = "uaflag";
        _sawmill.Debug($"[CLIENT CAPTURE OBJ] Set sprite state for {flagUid} to {spriteState} (controller: {faction})");
    }
}
