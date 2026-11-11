namespace Content.Shared.CMU14.Fighter;

/// <summary>Short ground runs and lift curves shared by the server, hull and effects.</summary>
public static class CMUFighterTransition
{
    public static float Lift(float progress, bool landing, CMUFighterAirframePrototype airframe) => landing
        ? 1 - FighterVtol.Smooth(progress / airframe.TouchdownPoint)
        : FighterVtol.Smooth((progress - airframe.LiftPoint) / (1 - airframe.LiftPoint));

    public static float Roll(float progress, bool landing, CMUFighterAirframePrototype airframe)
    {
        if (landing)
        {
            var p = Math.Clamp((progress - airframe.TouchdownPoint) / (1 - airframe.TouchdownPoint), 0, 1);
            return 1 - (1 - p) * (1 - p);
        }

        var run = Math.Clamp((progress - .12f) / (airframe.LiftPoint + .15f - .12f), 0, 1);
        return run * run;
    }

    public static float Thrust(float progress, bool landing, CMUFighterAirframePrototype airframe) => landing
        ? .28f * (1 - FighterVtol.Smooth((progress - airframe.TouchdownPoint) / (1 - airframe.TouchdownPoint)))
        : FighterVtol.Smooth(progress / .2f) * (1 - .2f * FighterVtol.Smooth((progress - .8f) / .2f));
}
