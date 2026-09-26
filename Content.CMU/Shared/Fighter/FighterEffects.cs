using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Fighter;

/// <summary>Short, authoritative presentation history shared by both seats. Never drives gameplay.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class FighterEffectsComponent : Component
{
    [DataField, AutoNetworkedField] public List<FighterEffectCue> Cues = [];
    [DataField, AutoNetworkedField] public uint Sequence;
    // Cue offsets stay relative to this paused epoch, including across a paused cockpit map.
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan Epoch;
}

[Serializable, NetSerializable]
public enum FighterEffectKind : byte
{
    Gau, Rocket, Missile, Interceptor, Flares, Hit, Evaded,
    Launch, Pass, Return, Holding, Repaired, Overheat, Laser, LaserLock, Crash,
}

[Serializable, NetSerializable]
public sealed record FighterEffectCue(uint Sequence, FighterEffectKind Kind, TimeSpan Offset, int Index, Vector2 Direction, float Duration);

/// <summary>A visual descent on the ground map, independent of the real CAS payload's nullspace simulation.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class FighterStrikeVisualComponent : Component
{
    [DataField, AutoNetworkedField] public FighterWeaponKind Kind;
    [DataField, AutoNetworkedField] public Vector2 Direction;
    [DataField, AutoNetworkedField] public int Volleys;
    [DataField, AutoNetworkedField] public float VolleyInterval;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ImpactAt;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ExpiresAt;
    [DataField, AutoNetworkedField] public List<FighterGroundImpact> Impacts = [];
}

/// <summary>One record per real CAS volley, relative to the strike's paused time and position.</summary>
[Serializable, NetSerializable]
public sealed record FighterGroundImpact(Vector2 Offset, TimeSpan At, bool Water);

[RegisterComponent]
public sealed partial class FighterPayloadVisualComponent : Component
{
    public EntityUid Visual;
    public TimeSpan NextImpactSound;
}

public static class FighterEffects
{
    public const int MaximumCues = 24;
    public const float HistorySeconds = 8;
    public const int MaximumGroundImpacts = 48;
    public const float GroundLifetime = 10;

    public static void AddGroundImpact(FighterStrikeVisualComponent strike, Vector2 offset, TimeSpan now, bool water)
    {
        strike.Impacts.RemoveAll(hit => (now - strike.ImpactAt - hit.At).TotalSeconds >= GroundLifetime);
        while (strike.Impacts.Count >= MaximumGroundImpacts) strike.Impacts.RemoveAt(0);
        strike.Impacts.Add(new(offset, now - strike.ImpactAt, water));
        strike.ExpiresAt = now + TimeSpan.FromSeconds(GroundLifetime);
    }

    public static FighterEffectCue Add(FighterEffectsComponent effects, FighterEffectKind kind, TimeSpan now,
        int index = -1, Vector2 direction = default, float duration = 1)
    {
        if (effects.Sequence == 0) effects.Epoch = now;
        effects.Cues.RemoveAll(cue => Age(effects, cue, now) >= HistorySeconds);
        while (effects.Cues.Count >= MaximumCues) effects.Cues.RemoveAt(0);
        var cue = new FighterEffectCue(++effects.Sequence, kind, now - effects.Epoch, index,
            direction.LengthSquared() > .001f && FighterFlight.Finite(direction) ? Vector2.Normalize(direction) : Vector2.UnitY,
            Math.Clamp(duration, .05f, HistorySeconds));
        effects.Cues.Add(cue);
        return cue;
    }

    public static float Age(FighterEffectsComponent effects, FighterEffectCue cue, TimeSpan now) =>
        (float) (now - effects.Epoch - cue.Offset).TotalSeconds;

    public static bool Active(FighterEffectsComponent effects, FighterEffectCue cue, TimeSpan now) =>
        Age(effects, cue, now) is >= 0 and < HistorySeconds && Age(effects, cue, now) < cue.Duration;

    public static float DescentSeconds(FighterWeaponKind kind) => kind switch
    {
        FighterWeaponKind.Gau => .15f,
        FighterWeaponKind.Rockets => .65f,
        _ => 1.2f,
    };

    /// <summary>Returns the current visible tracer in a volley without replaying an old impact.</summary>
    public static bool TryDescent(FighterStrikeVisualComponent strike, TimeSpan now, out float progress, out int volley)
    {
        var travel = DescentSeconds(strike.Kind);
        var elapsed = (float) (now - strike.ImpactAt).TotalSeconds + travel;
        progress = 0;
        volley = 0;
        if (now >= strike.ExpiresAt || elapsed < 0 || strike.Volleys <= 0) return false;
        volley = Math.Min(strike.Volleys - 1, (int) (elapsed / Math.Max(.001f, strike.VolleyInterval)));
        progress = (elapsed - volley * strike.VolleyInterval) / travel;
        return progress is >= 0 and <= 1;
    }
}
