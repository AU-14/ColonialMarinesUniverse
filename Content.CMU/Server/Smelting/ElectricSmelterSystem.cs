// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Linq;
using Content.Server.Power.EntitySystems;
using Content.Shared.CMU14.Smelting;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Smelting;

// The electric smelter: melts what is in its slots by recipe at the temperature the player sets, using power.
public sealed class ElectricSmelterSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPowerStateSystem _powerState = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly SoundSpecifier CompleteSound = new SoundPathSpecifier("/Audio/Machines/ding.ogg");

    private const float TemperatureTolerance = 0.5f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ElectricSmelterComponent, ElectricSmelterSetTemperatureMessage>(OnSetTemperature);
        SubscribeLocalEvent<ElectricSmelterComponent, ExaminedEvent>(OnExamined);
    }

    private void OnSetTemperature(Entity<ElectricSmelterComponent> ent, ref ElectricSmelterSetTemperatureMessage args)
    {
        ent.Comp.TargetTemperature = Math.Clamp(args.Temperature, ent.Comp.MinTemperature, ent.Comp.MaxTemperature);
        Dirty(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ElectricSmelterComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var smelter = new Entity<ElectricSmelterComponent>(uid, comp);
            var powered = this.IsPowered(uid, EntityManager);

            if (!powered)
            {
                if (comp.BatchEndsAt != null)
                    PauseBatch(smelter);

                _powerState.TrySetWorkingState(uid, false);
                continue;
            }

            StepTemperature(smelter, frameTime);

            var tally = GetLoadedTally(smelter);

            if (comp.BatchEndsAt != null && comp.RecipeId != FindReadyRecipe(smelter, tally)?.ID)
            {
                CancelBatch(smelter);
            }

            if (comp.BatchEndsAt == null)
                TryStartBatch(smelter, tally);

            if (comp.BatchEndsAt is { } endsAt && now >= endsAt)
                CompleteBatch(smelter);

            var heating = MathF.Abs(comp.CurrentTemperature - comp.TargetTemperature) > TemperatureTolerance;
            _powerState.TrySetWorkingState(uid, heating || comp.BatchEndsAt != null);
        }
    }

    private void StepTemperature(Entity<ElectricSmelterComponent> smelter, float frameTime)
    {
        var comp = smelter.Comp;
        var diff = comp.TargetTemperature - comp.CurrentTemperature;
        if (MathF.Abs(diff) <= TemperatureTolerance)
            return;

        var step = comp.HeatRatePerSecond * frameTime;
        comp.CurrentTemperature += MathF.Sign(diff) * MathF.Min(step, MathF.Abs(diff));
        Dirty(smelter);
    }

    private void TryStartBatch(Entity<ElectricSmelterComponent> smelter, Dictionary<ProtoId<StackPrototype>, int> tally)
    {
        if (FindReadyRecipe(smelter, tally) is not { } recipe)
            return;

        if (_itemSlots.GetItemOrNull(smelter.Owner, ElectricSmelterComponent.OutputSlotId) != null)
            return;

        var duration = smelter.Comp.BatchRemaining ?? recipe.Duration;
        smelter.Comp.BatchRemaining = null;
        smelter.Comp.BatchDuration = recipe.Duration;
        smelter.Comp.BatchEndsAt = _timing.CurTime + duration;
        smelter.Comp.RecipeId = recipe.ID;
        smelter.Comp.Active = true;
        Dirty(smelter);
    }

    private void PauseBatch(Entity<ElectricSmelterComponent> smelter)
    {
        var comp = smelter.Comp;
        if (comp.BatchEndsAt is not { } endsAt)
            return;

        var left = endsAt - _timing.CurTime;
        comp.BatchRemaining = left > TimeSpan.Zero ? left : TimeSpan.Zero;
        comp.BatchEndsAt = null;
        Dirty(smelter);
    }

    private void CancelBatch(Entity<ElectricSmelterComponent> smelter)
    {
        var comp = smelter.Comp;
        comp.BatchEndsAt = null;
        comp.BatchDuration = null;
        comp.BatchRemaining = null;
        comp.RecipeId = null;
        comp.Active = false;
        Dirty(smelter);
    }

    private void CompleteBatch(Entity<ElectricSmelterComponent> smelter)
    {
        var comp = smelter.Comp;
        comp.BatchEndsAt = null;
        comp.BatchDuration = null;
        comp.Active = false;

        if (comp.RecipeId is not { } recipeId || !_proto.TryIndex<AlloyRecipePrototype>(recipeId, out var recipe))
        {
            comp.RecipeId = null;
            Dirty(smelter);
            return;
        }

        comp.RecipeId = null;

        foreach (var slotId in ElectricSmelterComponent.MaterialSlotIds)
        {
            if (_itemSlots.GetItemOrNull(smelter.Owner, slotId) is { } item)
                PredictedQueueDel(item);
        }

        var coords = Transform(smelter).Coordinates;

        if (_proto.TryIndex(recipe.Result, out var resultProto) && !string.IsNullOrEmpty(resultProto.Spawn.Id))
        {
            var result = Spawn(resultProto.Spawn, coords);
            if (TryComp<StackComponent>(result, out var resultStack))
                _stack.SetCount(result, recipe.ResultAmount, resultStack);

            _itemSlots.TryInsert(smelter.Owner, ElectricSmelterComponent.OutputSlotId, result, null);
        }

        _audio.PlayPvs(CompleteSound, smelter);
        Dirty(smelter);
    }

    private Dictionary<ProtoId<StackPrototype>, int> GetLoadedTally(Entity<ElectricSmelterComponent> smelter)
    {
        var tally = new Dictionary<ProtoId<StackPrototype>, int>();

        foreach (var slotId in ElectricSmelterComponent.MaterialSlotIds)
        {
            if (_itemSlots.GetItemOrNull(smelter.Owner, slotId) is not { } item || !TryComp<StackComponent>(item, out var stack))
                continue;

            tally[stack.StackTypeId] = tally.GetValueOrDefault(stack.StackTypeId) + stack.Count;
        }

        return tally;
    }

    private AlloyRecipePrototype? FindReadyRecipe(Entity<ElectricSmelterComponent> smelter, Dictionary<ProtoId<StackPrototype>, int> tally)
    {
        if (tally.Count == 0)
            return null;

        foreach (var recipe in _proto.EnumeratePrototypes<AlloyRecipePrototype>())
        {
            if (recipe.Materials.Count != tally.Count)
                continue;

            if (!recipe.Materials.All(kv => tally.GetValueOrDefault(kv.Key) == kv.Value))
                continue;

            if (smelter.Comp.CurrentTemperature < recipe.MinTemperature)
                continue;

            return recipe;
        }

        return null;
    }

    private void OnExamined(Entity<ElectricSmelterComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var comp = ent.Comp;
        using var block = args.PushGroup(nameof(ElectricSmelterComponent));

        args.PushMarkup(Loc.GetString("au14-smelter-examine-temperature",
            ("current", (int) comp.CurrentTemperature),
            ("target", (int) comp.TargetTemperature)));

        if (comp.BatchEndsAt is { } endsAt && comp.BatchDuration is { } duration && duration > TimeSpan.Zero)
        {
            var left = endsAt - _timing.CurTime;
            var percent = (int) Math.Clamp((1 - left.TotalSeconds / duration.TotalSeconds) * 100, 0, 100);
            args.PushMarkup(Loc.GetString("au14-smelter-examine-progress", ("percent", percent)));
        }
        else if (!this.IsPowered(ent.Owner, EntityManager))
        {
            args.PushMarkup(Loc.GetString("au14-smelter-examine-unpowered"));
        }
    }
}
