using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Content.Shared.Actions;

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
    public Vector2i Origin;
    public int MinDepth = -8;
    public bool Center = true;
    public bool NextWorld;
    public int RequestId;
}

[Serializable, NetSerializable]
public sealed class CMUCaveSurveySurfaceMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CMUCaveSurveySectionMessage(int requestId, NetEntity world, string name, int blocks) : BoundUserInterfaceMessage
{
    public int RequestId = requestId;
    public NetEntity World = world;
    public string Name = name;
    public int Blocks = blocks;
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
        var open = new CMUOpenCaveSurveyEvent();
        OnOpen(ent, ref open);
    }

    private void OnOpen(Entity<CMUCaveSurveyAccessComponent> ent, ref CMUOpenCaveSurveyEvent args)
    {
        args.Handled = true;
        if (_ui.IsUiOpen(ent.Owner, CMUCaveSurveyUiKey.Key, ent.Owner))
            _ui.CloseUi(ent.Owner, CMUCaveSurveyUiKey.Key, ent.Owner);
        else
            _ui.TryOpenUi(ent.Owner, CMUCaveSurveyUiKey.Key, ent.Owner);
    }
}
