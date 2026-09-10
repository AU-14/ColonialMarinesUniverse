using System;
using System.Collections.Generic;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Sentry.Laptop;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedSentryLaptopSystem))]
public sealed partial class SentryLaptopComponent : Component
{
    [DataField]
    public bool IsOpen;

    [DataField]
    public bool IsPowered;

    [DataField]
    public float Range = 20f;

    [DataField]
    public HashSet<EntityUid> LinkedSentries = new();

    [DataField]
    public int MaxLinkedSentries = 99;

    [DataField]
    public Dictionary<EntityUid, string> SentryCustomNames = new();

    [DataField]
    public List<EntityUid> Watchers = new();

    [DataField]
    public EntityUid? CurrentCamera;

}

[Serializable, NetSerializable]
public enum SentryLaptopVisuals : byte
{
    State
}

[Serializable, NetSerializable]
public enum SentryLaptopVisualLayers : byte
{
    Base
}

[Serializable, NetSerializable]
public enum SentryLaptopState : byte
{
    Closed,
    Open,
    Active
}

[Serializable, NetSerializable]
public enum SentryLaptopUiKey : byte
{
    Key
}
