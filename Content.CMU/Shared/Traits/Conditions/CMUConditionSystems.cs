using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Nutrition;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;

namespace Content.Shared.CMU14.Traits.Conditions;

/// <summary>
///     Someone wheelchair-bound only moves normally while sitting in a chair. Out of it they lie on the floor and crawl.
/// </summary>
public sealed partial class CMUWheelchairBoundSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUWheelchairBoundComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CMUWheelchairBoundComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<CMUWheelchairBoundComponent, StandAttemptEvent>(OnStandAttempt);
        SubscribeLocalEvent<CMUWheelchairBoundComponent, BuckledEvent>(OnBuckled);
        SubscribeLocalEvent<CMUWheelchairBoundComponent, UnbuckledEvent>(OnUnbuckled);
    }

    private bool InChair(EntityUid uid)
    {
        return TryComp(uid, out BuckleComponent? buckle) && buckle.Buckled;
    }

    private void OnStartup(Entity<CMUWheelchairBoundComponent> ent, ref ComponentStartup args)
    {
        if (!InChair(ent))
            _standing.Down(ent);

        _speed.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnRefreshSpeed(Entity<CMUWheelchairBoundComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!InChair(ent))
            args.ModifySpeed(ent.Comp.CrawlSpeed, ent.Comp.CrawlSpeed);
    }

    private void OnStandAttempt(Entity<CMUWheelchairBoundComponent> ent, ref StandAttemptEvent args)
    {
        if (!InChair(ent))
            args.Cancel();
    }

    private void OnBuckled(Entity<CMUWheelchairBoundComponent> ent, ref BuckledEvent args)
    {
        _speed.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnUnbuckled(Entity<CMUWheelchairBoundComponent> ent, ref UnbuckledEvent args)
    {
        _standing.Down(ent);
        _speed.RefreshMovementSpeedModifiers(ent.Owner);
    }
}

/// <summary>Hearty appetite and high thirst speed up how fast hunger and thirst run down.</summary>
public sealed partial class CMUSatiationTraitSystem : EntitySystem
{
    private const string Hunger = "Hunger";
    private const string Thirst = "Thirst";

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUHeartyAppetiteComponent, CMUSatiationRateEvent>(OnHungerRate);
        SubscribeLocalEvent<CMUHighThirstComponent, CMUSatiationRateEvent>(OnThirstRate);
    }

    private void OnHungerRate(Entity<CMUHeartyAppetiteComponent> ent, ref CMUSatiationRateEvent args)
    {
        if (args.Type == Hunger && args.Rate < 0)
            args.Rate *= ent.Comp.Multiplier;
    }

    private void OnThirstRate(Entity<CMUHighThirstComponent> ent, ref CMUSatiationRateEvent args)
    {
        if (args.Type == Thirst && args.Rate < 0)
            args.Rate *= ent.Comp.Multiplier;
    }
}

/// <summary>Slows someone down while their heart condition is acting up. The server drives the episodes.</summary>
public sealed partial class CMUHeartConditionSpeedSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<CMUHeartConditionComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnRefreshSpeed(Entity<CMUHeartConditionComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.InEpisode)
            args.ModifySpeed(ent.Comp.EpisodeSpeedModifier, ent.Comp.EpisodeSpeedModifier);
    }
}
