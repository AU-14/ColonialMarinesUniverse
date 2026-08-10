using Content.Server.Light.EntitySystems;
using Content.Shared._CMU14.Xenomorphs.Pathogen.BlightWave;
using Content.Shared.Coordinates;
using Robust.Server.GameObjects;

namespace Content.Server._CMU14.Xenomorphs.Pathogen.BlightWave;

public sealed partial class BlightWaveSystem : SharedBlightWaveSystem
{
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;

    private readonly HashSet<Entity<PointLightComponent>> _lights = new();

    protected override void OnAction(Entity<CMUXenoBlightWaveComponent> xeno, ref CMUXenoBlightWaveActionEvent args)
    {
        base.OnAction(xeno, ref args);

        if (!args.Handled)
            return;

        var coords = _transform.GetMapCoordinates(xeno);

        _lights.Clear();
        _lookup.GetEntitiesInRange(coords, xeno.Comp.Range * 2f, _lights);

        var restoreAt = _timing.CurTime + xeno.Comp.LightOffDuration;

        foreach (var light in _lights)
        {
            if (!light.Comp.Enabled || light.Comp.Radius <= 0f)
                continue;

            var restore = EnsureComp<CMUXenoBlightWaveLightRestoreComponent>(light);
            restore.OriginalRadius = light.Comp.Radius;
            restore.RestoreAt = restoreAt;
            _pointLight.SetRadius(light.Owner, 0f, light.Comp);
        }

        if (xeno.Comp.Effect is { } effect)
            SpawnAttachedTo(effect, xeno.Owner.ToCoordinates());
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUXenoBlightWaveLightRestoreComponent, PointLightComponent>();
        while (query.MoveNext(out var uid, out var restore, out var light))
        {
            if (time < restore.RestoreAt)
                continue;

            _pointLight.SetRadius(uid, restore.OriginalRadius, light);
            RemComp<CMUXenoBlightWaveLightRestoreComponent>(uid);
        }
    }
}