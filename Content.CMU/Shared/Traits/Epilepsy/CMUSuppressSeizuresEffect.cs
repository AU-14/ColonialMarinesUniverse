using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Traits.Epilepsy;

/// <summary>
///     Stops a seizure that's underway and keeps someone with epilepsy from seizing for a while. Each dose resets the
///     clock to the full duration.
/// </summary>
[UsedImplicitly]
public sealed partial class CMUSuppressSeizuresEffect : EntityEffectBase<CMUSuppressSeizuresEffect>
{
    /// <summary>Seconds of protection from the latest dose.</summary>
    [DataField]
    public float Duration = 1500f;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("cmu-medical-suppress-seizures-guidebook", ("minutes", (int) (Duration / 60)));
}

public sealed partial class CMUSuppressSeizuresEntityEffectSystem : EntityEffectSystem<EpilepsyComponent, CMUSuppressSeizuresEffect>
{
    [Dependency] private EpilepsySystem _epilepsy = default!;
    [Dependency] private IGameTiming _timing = default!;

    protected override void Effect(Entity<EpilepsyComponent> entity, ref EntityEffectEvent<CMUSuppressSeizuresEffect> args)
    {
        _epilepsy.TryEndSeizure(entity);

        var until = _timing.CurTime + TimeSpan.FromSeconds(args.Effect.Duration);
        if (until <= entity.Comp.SuppressedUntil)
            return;

        entity.Comp.SuppressedUntil = until;
        Dirty(entity);
    }
}
