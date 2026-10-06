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
    // Best case: immediate flares at maximum speed. Slow flight and late flares reduce this.
    [DataField, AutoNetworkedField] public float FlareEvasionChance = .45f;
    [DataField, AutoNetworkedField] public float SlowFlareEvasionMultiplier = .5f;
    [DataField, AutoNetworkedField] public float LateFlareEvasionMultiplier = .25f;
    [DataField, AutoNetworkedField] public float DeployedFlareEvasionChance;
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
    /// <summary>Capture effectiveness when flares deploy, so later speed changes cannot improve that attempt.</summary>
    public static float FlareEvasion(FighterAircraftComponent aircraft, FighterAirCombatComponent combat, TimeSpan now)
    {
        var duration = (combat.IncomingAt - combat.IncomingStartedAt).TotalSeconds;
        if (duration <= 0 || now >= combat.IncomingAt)
            return 0;

        var remaining = (float) Math.Clamp((combat.IncomingAt - now).TotalSeconds / duration, 0, 1);
        var speed = Math.Clamp((aircraft.Speed - aircraft.MinimumSpeed) /
                               Math.Max(1f, aircraft.MaximumSpeed - aircraft.MinimumSpeed), 0, 1);
        var late = Math.Clamp(combat.LateFlareEvasionMultiplier, 0, 1);
        var slow = Math.Clamp(combat.SlowFlareEvasionMultiplier, 0, 1);
        return Math.Clamp(combat.FlareEvasionChance, 0, 1) *
               (late + (1 - late) * remaining) * (slow + (1 - slow) * speed);
    }

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
