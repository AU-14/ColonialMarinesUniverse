using Content.Shared._CMU14.Round.Objectives.Type;
using Robust.Shared.GameStates;

namespace Content.Client._CMU14.Round.Objectives;

public sealed partial class ClientObjectiveCaptureSystem : EntitySystem
{
    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("client-capture-obj");
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
        if (!TryComp<AppearanceComponent>(flagUid, out _))
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
