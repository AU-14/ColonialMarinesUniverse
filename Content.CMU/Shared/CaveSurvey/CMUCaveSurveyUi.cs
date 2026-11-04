using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Content.Shared.Actions;
using Content.Shared.Ghost.Components;

namespace Content.Shared.CMU14.CaveSurvey;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUCaveSurveyAccessComponent : Component;

[RegisterComponent]
public sealed partial class CMUCaveSurveyMapComponent : Component;

[Serializable, NetSerializable]
public enum CMUCaveSurveyUiKey : byte { Key }

[ByRefEvent]
public struct CMUOpenCaveSurveyEvent
{
    public bool Handled;
}

[Serializable, NetSerializable]
public sealed class CMUCaveSurveyViewMessage : BoundUserInterfaceMessage
{
    public Vector2i Focus;
    public int MinDepth = -8;
    public bool Center = true;
    public bool NextWorld;
    public int RequestId;
}

[Serializable, NetSerializable]
public sealed class CMUCaveSurveySurfaceMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CMUCaveSurveySectionMessage(int requestId, NetEntity world, string name, int blocks,
    Vector2i min, Vector2i max) : BoundUserInterfaceMessage
{
    public int RequestId = requestId;
    public NetEntity World = world;
    public string Name = name;
    public int Blocks = blocks;
    public Vector2i Min = min;
    public Vector2i Max = max;
}

public sealed partial class CMUOpenCaveSurveyActionEvent : InstantActionEvent;

/// <summary>The shared tactical action hook also suppresses the old predicted UI on the client.</summary>
public sealed partial class CMUCaveSurveyAccessSystem : EntitySystem
{
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUCaveSurveyAccessComponent, CMUOpenCaveSurveyEvent>(OnOpen);
        SubscribeLocalEvent<CMUCaveSurveyAccessComponent, CMUOpenCaveSurveyActionEvent>(OnAction);
    }

    private void OnAction(Entity<CMUCaveSurveyAccessComponent> ent, ref CMUOpenCaveSurveyActionEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;
        Toggle(ent);
    }

    private void OnOpen(Entity<CMUCaveSurveyAccessComponent> ent, ref CMUOpenCaveSurveyEvent args)
    {
        // Observers have a separate mining action; keep their colony tactical map available.
        if (HasComp<GhostComponent>(ent)) return;
        args.Handled = true;
        Toggle(ent);
    }

    private void Toggle(EntityUid actor)
    {
        if (_ui.IsUiOpen(actor, CMUCaveSurveyUiKey.Key, actor))
            _ui.CloseUi(actor, CMUCaveSurveyUiKey.Key, actor);
        else
            _ui.TryOpenUi(actor, CMUCaveSurveyUiKey.Key, actor);
    }
}
