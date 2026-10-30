using Content.Server.Chemistry.Components;
using Content.Shared._RMC14.Xenonids.Despoiler;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Xenos.Despoiler;

/// <summary>
///     A fire extinguisher (or any water spray) washes away the lingering acid a despoiler leaves on the ground.
///     The vapor and the acid never collide, so this checks what each spray cloud is passing over.
/// </summary>
public sealed class CMULingeringAcidExtinguishSystem : EntitySystem
{
    private static readonly ProtoId<ReagentPrototype> Water = "Water";
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(0.1);
    private const float Range = 0.5f;

    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly HashSet<Entity<XenoDespoilerLingeringAcidComponent>> _acid = new();
    private TimeSpan _nextCheck;

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + CheckInterval;

        var query = EntityQueryEnumerator<VaporComponent, SolutionComponent, TransformComponent>();
        while (query.MoveNext(out _, out var vapor, out var solution, out var xform))
        {
            if (!vapor.Active || !solution.Solution.ContainsPrototype(Water))
                continue;

            _acid.Clear();
            _lookup.GetEntitiesInRange(xform.Coordinates, Range, _acid);
            foreach (var acid in _acid)
            {
                QueueDel(acid);
            }
        }
    }
}
