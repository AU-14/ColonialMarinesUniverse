using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Fighter;

[Serializable, NetSerializable]
public enum FighterVtolOutcome : byte { Active, Departed, Touchdown, Aborted }

[Serializable, NetSerializable]
public enum FighterVtolSurface : byte { Dust, Deck, Water, Snow }

/// <summary>A fixed ground emission site, independent of the moving airframe.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class FighterVtolVisualComponent : Component
{
    public const float ResidueSeconds = 3;
    [DataField, AutoNetworkedField] public bool Landing;
    [DataField, AutoNetworkedField] public FighterVtolOutcome Outcome;
    [DataField, AutoNetworkedField] public FighterVtolSurface Surface;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan StartedAt;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan EndsAt;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan FinishedAt;
}

/// <summary>Shared motion curves keep the exterior, ground effects and flight camera in phase.</summary>
public static class FighterVtol
{
    public static float Progress(TimeSpan now, TimeSpan start, TimeSpan end) =>
        (float) Math.Clamp((now - start).TotalSeconds / Math.Max(.01, (end - start).TotalSeconds), 0, 1);

    public static float Smooth(float value)
    {
        var t = Math.Clamp(value, 0, 1);
        return t * t * (3 - 2 * t);
    }

    // Spool on the pad, briefly hover, then accelerate upwards. Landing brakes into a gentle settle.
    public static float Lift(float progress, bool landing) => landing
        ? MathF.Pow(1 - Smooth(progress), 1.35f)
        : .18f * Smooth((progress - .2f) / .3f) + .82f * Smooth((progress - .48f) / .52f);

    public static float Thrust(float progress, bool landing) => landing
        ? (.28f + .72f * Smooth(progress / .65f)) * (1 - .3f * Smooth((progress - .85f) / .15f))
        : Smooth(progress / .23f) * (1 - .5f * Smooth((progress - .65f) / .35f));

    public static float Settle(float seconds) => seconds is >= 0 and < .8f
        ? -MathF.Sin(seconds / .8f * MathF.Tau) * MathF.Exp(-seconds * 6) * .1f : 0;
}
