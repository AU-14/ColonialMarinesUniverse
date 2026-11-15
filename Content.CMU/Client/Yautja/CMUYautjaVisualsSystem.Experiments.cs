using Content.Shared.CMU14.Yautja;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Yautja;

public sealed partial class CMUYautjaVisualsSystem
{
    private void UpdateExperimentLights()
    {
        var query = EntityQueryEnumerator<CMUYautjaEffectComponent, PointLightComponent>();
        while (query.MoveNext(out var uid, out var effect, out var light))
        {
            if (effect.Kind != CMUYautjaEffect.Experiment || effect.Experiment is not { } id)
                continue;
            var recipe = _prototypes.Index(id);
            var p = CMUYautjaPresentation.Progress(_timing.CurTime, effect.StartedAt, effect.EndsAt);
            var charge = CMUYautjaPresentation.Segment(p, 0, CMUYautjaExperimentPrototype.ReleaseAt);
            var fade = CMUYautjaPresentation.Segment(p, 0, .08f) *
                (1 - CMUYautjaPresentation.Segment(p, CMUYautjaExperimentPrototype.CollapseAt, 1));
            var release = p < CMUYautjaExperimentPrototype.ReleaseAt ? 0 :
                Math.Max(0, 1 - (p - CMUYautjaExperimentPrototype.ReleaseAt) / .1f);
            var ripple = .9f + .1f * MathF.Sin(p * 80);
            _lights.SetColor(uid, recipe.Color, light);
            _lights.SetEnergy(uid, recipe.LightEnergy * fade * (.25f + charge * .35f + release * .6f) * ripple, light);
            _lights.SetRadius(uid, 1.5f + charge * .9f + release * .5f, light);
        }
    }
}
