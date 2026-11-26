using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.BuildOrders;

/// <summary>
///     Added to the platoon commander. They pick something from a build menu and mark where it goes, and everyone on
///     their side sees a see-through blueprint there until it is built.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUBuildOrderAbilityComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId MenuAction = "ActionCMUBuildMenu";

    [DataField, AutoNetworkedField]
    public EntityUid? MenuActionEntity;

    [DataField, AutoNetworkedField]
    public EntProtoId PlaceAction = "ActionCMUBuildPlace";

    [DataField, AutoNetworkedField]
    public EntityUid? PlaceActionEntity;

    /// <summary>What the commander can order built.</summary>
    [DataField]
    public List<EntProtoId> Options = new();

    /// <summary>What the next marked site will be.</summary>
    [DataField, AutoNetworkedField]
    public EntProtoId? Selected;

    /// <summary>How many blueprints can be up at once; the oldest goes when this is passed.</summary>
    [DataField]
    public int MaxOrders = 12;

    /// <summary>How far from the commander a site can be marked.</summary>
    [DataField]
    public float Range = 30f;

    /// <summary>A blueprint nobody builds goes away on its own after this.</summary>
    [DataField]
    public TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    [DataField, AutoNetworkedField]
    public EntProtoId MarkAction = "ActionCMUMarkTarget";

    [DataField, AutoNetworkedField]
    public EntityUid? MarkActionEntity;

    /// <summary>How long someone stays highlighted for their side.</summary>
    [DataField]
    public TimeSpan MarkDuration = TimeSpan.FromSeconds(5);

    /// <summary>How far away someone can be highlighted.</summary>
    [DataField]
    public float MarkRange = 30f;

    /// <summary>How far from the commander someone still gets the PUSH arrow and the call-out text.</summary>
    [DataField]
    public float PushRange = 30f;

    /// <summary>How long the arrow from a PUSH order stays on everyone's screen.</summary>
    [DataField]
    public TimeSpan PushDuration = TimeSpan.FromSeconds(10);
}

/// <summary>Someone the commander has highlighted for a few seconds: their side sees a ring around them.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUMarkedComponent : Component
{
    [DataField, AutoNetworkedField]
    public string? Faction;

    public TimeSpan ExpireAt;
}

public enum CMUPushDirection : byte
{
    North,
    East,
    South,
    West,
}

/// <summary>Given to everyone on the commander's side when they order a push: an arrow showing which way to go.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUPushArrowComponent : Component
{
    [DataField, AutoNetworkedField]
    public CMUPushDirection Direction;

    public TimeSpan ExpireAt;
}

public sealed partial class CMUMarkActionEvent : EntityTargetActionEvent;

/// <summary>A blueprint on the map: what is to be built here, and which side can see it.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CMUBuildOrderMarkerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId? Order;

    [DataField, AutoNetworkedField]
    public string? Faction;

    [DataField, AutoNetworkedField]
    public EntityUid? Owner;

    public TimeSpan ExpireAt;
}

public sealed partial class CMUBuildMenuActionEvent : InstantActionEvent;

public sealed partial class CMUBuildPlaceActionEvent : WorldTargetActionEvent;

/// <summary>Sent to the commander to open the build menu.</summary>
[Serializable, NetSerializable]
public sealed class CMUBuildMenuOpenEvent(List<string> options, string? selected) : EntityEventArgs
{
    public readonly List<string> Options = options;
    public readonly string? Selected = selected;
}

/// <summary>The commander picked what to build next.</summary>
[Serializable, NetSerializable]
public sealed class CMUBuildSelectEvent(string proto) : EntityEventArgs
{
    public readonly string Proto = proto;
}

/// <summary>The commander placed a ghost from the game's construction menu; their side should see it too.</summary>
[Serializable, NetSerializable]
public sealed class CMUBuildGhostPlacedEvent(string proto, NetCoordinates coordinates, Angle rotation) : EntityEventArgs
{
    public readonly string Proto = proto;
    public readonly NetCoordinates Coordinates = coordinates;
    public readonly Angle Rotation = rotation;
}

/// <summary>The commander took all their blueprints down.</summary>
[Serializable, NetSerializable]
public sealed class CMUBuildClearEvent : EntityEventArgs;
