using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Fighter;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class FighterAirCombatComponent : Component
{
    [DataField, AutoNetworkedField] public List<int> CoveredSectors = [];
    [DataField, AutoNetworkedField] public int MaximumCoveredSectors = 2;
    [DataField] public TimeSpan CoverageArmTime = TimeSpan.FromSeconds(8);
    [DataField] public TimeSpan InterceptCooldown = TimeSpan.FromSeconds(15);
    [DataField] public TimeSpan MissileFlightTime = TimeSpan.FromSeconds(5);
    [DataField] public TimeSpan RecoveryDuration = TimeSpan.FromMinutes(3);
    // Damage is cumulative for this airframe, including after completed repairs.
    [DataField, AutoNetworkedField] public int HitsTaken;
    [DataField] public int CrashHitLimit;
    [DataField] public TimeSpan CrashDuration = TimeSpan.FromSeconds(8);
    [DataField] public Vector2 CrashStart;
    [DataField] public Vector2 CrashTarget;
    [DataField] public float CrashHeight;
    [DataField, AutoNetworkedField] public float FlareEvasionChance = .65f;
    [DataField, AutoNetworkedField] public bool Incoming;
    [DataField, AutoNetworkedField] public bool IncomingFromGround;
    [DataField, AutoNetworkedField] public bool IncomingPlasma;
    public EntityUid? IncomingAudio;
    [DataField, AutoNetworkedField] public bool FlaresUsed;
    [DataField, AutoNetworkedField] public int IncomingSector = -1;
    [DataField, AutoNetworkedField] public int LastLaunchSector = -1;
    [DataField, AutoNetworkedField] public FighterAirResult Result;
    [DataField, AutoNetworkedField] public Vector2 IncomingDirection;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan IncomingStartedAt;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan CoverageReadyAt;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan InterceptReadyAt;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan IncomingAt;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ResultUntil;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan RecoveryUntil;
}

[Serializable, NetSerializable]
public enum FighterAirResult : byte { None, Evaded, Hit }

[Serializable, NetSerializable]
public sealed class FighterCoverSectorEvent(int sector) : EntityEventArgs
{
    public int Sector = sector;
}

/// <summary>Shared sector geometry for the coverage chart and authoritative interception checks.</summary>
public static class FighterAirCombat
{
    public static Vector2i Grid(Box2 battlefield)
    {
        var cell = Math.Max(1f, Math.Min(128f, Math.Min(battlefield.Width, battlefield.Height) / 3f));
        return new Vector2i(Math.Clamp((int) MathF.Ceiling(battlefield.Width / cell), 3, 26),
            Math.Clamp((int) MathF.Ceiling(battlefield.Height / cell), 3, 26));
    }

    public static int SectorAt(Box2 battlefield, Vector2 position)
    {
        if (!FighterFlight.Finite(position) || !battlefield.Contains(position)) return -1;
        var grid = Grid(battlefield);
        var x = Math.Clamp((int) ((position.X - battlefield.Left) / battlefield.Width * grid.X), 0, grid.X - 1);
        var y = Math.Clamp((int) ((battlefield.Top - position.Y) / battlefield.Height * grid.Y), 0, grid.Y - 1);
        return y * grid.X + x;
    }

    public static Box2 SectorBounds(Box2 battlefield, int sector)
    {
        var grid = Grid(battlefield);
        var size = battlefield.Size / new Vector2(grid.X, grid.Y);
        var topLeft = new Vector2(battlefield.Left, battlefield.Top) + new Vector2(sector % grid.X * size.X, -(sector / grid.X) * size.Y);
        return new Box2(topLeft.X, topLeft.Y - size.Y, topLeft.X + size.X, topLeft.Y);
    }

    public static string Label(Box2 battlefield, int sector) => $"{(char) ('A' + sector % Grid(battlefield).X)}{sector / Grid(battlefield).X + 1}";

    public static bool Opposing(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
        !string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
