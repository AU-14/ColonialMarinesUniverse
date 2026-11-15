using System.Numerics;
using Content.Shared.CMU14.Fighter;

namespace Content.Client.CMU14.Fighter;

internal static class FighterVtolPresentation
{
    public static void Hull(FighterGroundComponent ground, CMUFighterAirframePrototype airframe, TimeSpan now,
        out Vector2 offset, out float scale, out float opacity)
    {
        var active = ground.State is FighterGroundState.TakingOff or FighterGroundState.Landing;
        var p = FighterVtol.Progress(now, ground.StartedAt, ground.EndsAt);
        var lift = active ? CMUFighterTransition.Lift(p, ground.State == FighterGroundState.Landing, airframe) : 0;
        var thrust = active ? CMUFighterTransition.Thrust(p, ground.State == FighterGroundState.Landing, airframe) : 0;
        var age = (float) (now - ground.StartedAt).TotalSeconds;
        var settle = ground.State is FighterGroundState.Grounded or FighterGroundState.Landing && ground.TouchdownAt != TimeSpan.Zero
            ? FighterVtol.Settle((float) (now - ground.TouchdownAt).TotalSeconds) : 0;
        offset = new Vector2(MathF.Sin(age * 47) * .012f * thrust, -lift * 2.5f - settle);
        scale = 1 + lift * .28f + settle * .1f;
        opacity = 1 - FighterVtol.Smooth((lift - .55f) / .45f);
    }

    public static float Power(FighterVtolVisualComponent visual, CMUFighterAirframePrototype airframe, TimeSpan now)
    {
        var p = FighterVtol.Progress(now, visual.StartedAt, visual.EndsAt);
        return visual.Outcome == FighterVtolOutcome.Active ? CMUFighterTransition.Thrust(p, visual.Landing, airframe) : 0;
    }

    public static Color Dust(FighterVtolSurface surface) => surface switch
    {
        FighterVtolSurface.Deck => Color.FromHex("#939C9B"),
        FighterVtolSurface.Water => Color.FromHex("#B8DADF"),
        FighterVtolSurface.Snow => Color.FromHex("#D3DFE2"),
        _ => Color.FromHex("#B09E7E"),
    };

    public static Vector2 Socket(Vector2 pixels) => pixels * (FighterGroundComponent.SpriteScale / 32);

    public static Vector2 Roll(FighterVtolVisualComponent visual, CMUFighterAirframePrototype airframe, TimeSpan now)
    {
        var progress = FighterVtol.Progress(now, visual.StartedAt, visual.EndsAt);
        return new Vector2(0, visual.RollDistance * CMUFighterTransition.Roll(progress, visual.Landing, airframe));
    }
}
