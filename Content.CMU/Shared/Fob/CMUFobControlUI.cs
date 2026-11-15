using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Fob;

[Serializable, NetSerializable]
public enum CMUFobControlUiKey : byte { Key }

[Serializable, NetSerializable]
public enum CMUFobVisuals : byte { Available, Broken, Stowed }

[Serializable, NetSerializable]
public sealed class CMUFobControlMessage(bool activate) : BoundUserInterfaceMessage
{
    public readonly bool Activate = activate;
}

[Serializable, NetSerializable]
public sealed class CMUFobControlState(
    CMUFobReserveState state,
    int remaining,
    int restart,
    int minimum,
    int startup,
    int jobTime,
    int reward,
    string first,
    string second,
    CMUFobGunStatus[] guns,
    bool canActivate,
    bool canDeactivate,
    bool noRelayPair = false) : BoundUserInterfaceState
{
    public readonly CMUFobReserveState State = state;
    public readonly int Remaining = remaining;
    public readonly int Restart = restart;
    public readonly int Minimum = minimum;
    public readonly int Startup = startup;
    public readonly int JobTime = jobTime;
    public readonly int Reward = reward;
    public readonly string First = first;
    public readonly string Second = second;
    public readonly CMUFobGunStatus[] Guns = guns;
    public readonly bool CanActivate = canActivate;
    public readonly bool CanDeactivate = canDeactivate;
    public readonly bool NoRelayPair = noRelayPair;
}
