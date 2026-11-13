using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Yautja.BadBlood;

public sealed partial class BadBloodSurvivalSystem : EntitySystem
{
    [Dependency] private SatiationSystem _satiation = default!;

    // keyed by the threshold names the human hunger and thirst prototypes define. above Okay is well fed
    private static readonly Dictionary<SatiationValue, BadBloodSatiationState> HungerStates = new()
    {
        ["Starving"] = BadBloodSatiationState.Starving,
        ["Peckish"] = BadBloodSatiationState.Peckish,
        ["Okay"] = BadBloodSatiationState.Fed,
    };

    private static readonly Dictionary<SatiationValue, BadBloodSatiationState> ThirstStates = new()
    {
        ["Parched"] = BadBloodSatiationState.Starving,
        ["Thirsty"] = BadBloodSatiationState.Peckish,
        ["Okay"] = BadBloodSatiationState.Fed,
    };

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodSurvivalComponent, YautjaBracerRegenModifyEvent>(OnBracerRegenModify);
    }

    private void OnBracerRegenModify(Entity<BadBloodSurvivalComponent> ent, ref YautjaBracerRegenModifyEvent args)
    {
        args.Multiplier *= GetBracerRegenMultiplier(ent);
    }

    public BadBloodSatiationState GetHungerState(EntityUid uid)
    {
        return GetState(uid, SatiationSystem.Hunger, HungerStates);
    }

    public BadBloodSatiationState GetThirstState(EntityUid uid)
    {
        return GetState(uid, SatiationSystem.Thirst, ThirstStates);
    }

    // starving or parched. always false for anything that isn't a Bad Blood
    public bool IsStarving(EntityUid uid)
    {
        return HasComp<BadBloodSurvivalComponent>(uid) &&
               (GetHungerState(uid) == BadBloodSatiationState.Starving ||
                GetThirstState(uid) == BadBloodSatiationState.Starving);
    }

    // the worse of hunger and thirst decides. only a full stomach gives the bonus, water alone does not
    public float GetBracerRegenMultiplier(Entity<BadBloodSurvivalComponent> ent)
    {
        var hunger = GetHungerState(ent);
        var thirst = GetThirstState(ent);

        if (hunger == BadBloodSatiationState.Starving || thirst == BadBloodSatiationState.Starving)
            return ent.Comp.StarvingRegenMultiplier;

        if (hunger == BadBloodSatiationState.Peckish || thirst == BadBloodSatiationState.Peckish)
            return ent.Comp.PeckishRegenMultiplier;

        return hunger == BadBloodSatiationState.WellFed ? ent.Comp.WellFedRegenMultiplier : 1f;
    }

    public float GetLimbRegrowDelayMultiplier(EntityUid uid)
    {
        return TryComp(uid, out BadBloodSurvivalComponent? survival) && IsStarving(uid)
            ? survival.StarvingLimbRegrowMultiplier
            : 1f;
    }

    private BadBloodSatiationState GetState(
        EntityUid uid,
        ProtoId<SatiationTypePrototype> type,
        Dictionary<SatiationValue, BadBloodSatiationState> states)
    {
        if (!TryComp(uid, out SatiationComponent? satiation) || !satiation.Has(type))
            return BadBloodSatiationState.Fed;

        return _satiation.TryGetValueByThreshold((uid, satiation), type, states, out var state, out _, out _)
            ? state
            : BadBloodSatiationState.WellFed;
    }
}

public enum BadBloodSatiationState : byte
{
    Starving,
    Peckish,
    Fed,
    WellFed,
}
