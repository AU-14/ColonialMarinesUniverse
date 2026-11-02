// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Server.CMU14.Fluids;
using Content.Shared.CMU14.Fluids;
using Content.Shared.CMU14.Smelting;
using Content.Shared.CMU14.Steam;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Steam;

// The steam smelter. It makes only the recipes written for it (ore into ingots, in a mold), uses steam in proportion to the setting of its steam regulator, and with
// every operation leaves some slag in its tank; the slag is let out through its pipe at the pace of the slag regulator, and
// when it has nowhere to go the smelter stops.
public sealed class SteamSmelterSystem : EntitySystem
{
    [Dependency] private readonly FluidSystem _fluids = default!;
    [Dependency] private readonly SteamGasSystem _steam = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private static readonly SoundSpecifier CompleteSound = new SoundPathSpecifier("/Audio/Machines/ding.ogg");

    // How often the progress of an operation is sent to the windows that look at it.
    private const float DirtyInterval = 0.2f;

    // What the smelter is going to make, found again every tick from what is in it.
    private sealed class Job
    {
        public string Id = string.Empty;
        public float Duration;
        public ProtoId<StackPrototype> Result;
        public int ResultAmount;
        public readonly List<(EntityUid Item, int Amount)> Consumed = new();
        public float Slag;
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SteamSmelterComponent, SteamSmelterRegulatorMessage>(OnRegulator);
        SubscribeLocalEvent<SteamSmelterComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<SteamSmelterComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<SteamSmelterComponent> smelter, ref ComponentStartup args)
    {
        ApplyRegulators(smelter);
    }

    private void OnRegulator(Entity<SteamSmelterComponent> smelter, ref SteamSmelterRegulatorMessage args)
    {
        var value = float.IsFinite(args.Value) ? args.Value : 1f;
        if (args.Slag)
            smelter.Comp.SlagRegulator = Math.Clamp(value, 0f, 1f);
        else
            smelter.Comp.SteamRegulator = Math.Clamp(value, smelter.Comp.MinSteamRegulator, 1f);

        ApplyRegulators(smelter);
        Dirty(smelter);
    }

    // The slag regulator is the scale of the slag port.
    private void ApplyRegulators(Entity<SteamSmelterComponent> smelter)
    {
        if (!TryComp<FluidPortsComponent>(smelter, out var ports))
            return;

        foreach (var port in ports.Ports)
        {
            if (port.Kind == FluidKinds.Slag && port.Output)
                port.Scale = smelter.Comp.SlagRegulator;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SteamSmelterComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            UpdateSmelter((uid, comp), frameTime);
        }
    }

    private void UpdateSmelter(Entity<SteamSmelterComponent> smelter, float frameTime)
    {
        var comp = smelter.Comp;
        var job = FindJob(smelter);

        // Nothing to make, nowhere to put it, or no room left for the slag: what was begun is forgotten.
        if (job == null || !CanTakeOutput(smelter, job) ||
            _fluids.Room(Tank(smelter), FluidKinds.Slag, comp.SlagLimit) < job.Slag)
        {
            Stop(smelter, job == null || comp.ActiveRecipe != job.Id);
            return;
        }

        if (comp.ActiveRecipe != job.Id)
        {
            comp.ActiveRecipe = job.Id;
            comp.Progress = 0f;
        }

        // It works at the pace of the steam it is given.
        var steam = comp.SteamPerSecond * comp.SteamRegulator * frameTime;
        if (!_steam.TryUse(smelter.Owner, steam))
        {
            SetActive(smelter, false);
            return;
        }

        comp.Progress += frameTime * comp.SteamRegulator / Math.Max(job.Duration, 0.1f);
        SetActive(smelter, true);

        if (comp.Progress >= 1f)
        {
            Complete(smelter, job);
            return;
        }

        comp.NextDirty -= frameTime;
        if (comp.NextDirty <= 0f)
        {
            comp.NextDirty = DirtyInterval;
            Dirty(smelter);
        }
    }

    private FluidTankComponent Tank(EntityUid uid) => Comp<FluidTankComponent>(uid);

    private void SetActive(Entity<SteamSmelterComponent> smelter, bool active)
    {
        if (smelter.Comp.Active == active)
            return;

        smelter.Comp.Active = active;
        Dirty(smelter);
    }

    // Stops: the work is left where it is when the cause is only a lack of steam, and forgotten when the contents changed.
    private void Stop(Entity<SteamSmelterComponent> smelter, bool forget)
    {
        var comp = smelter.Comp;
        if (!comp.Active && comp.Progress == 0f && comp.ActiveRecipe == null)
            return;

        comp.Active = false;
        if (forget)
        {
            comp.Progress = 0f;
            comp.ActiveRecipe = null;
        }

        Dirty(smelter);
    }

    // The smelter makes only what the recipes of the steam smelter say: a mold of the shape in the mold slot, and enough of the
    // material in the material slots. Nothing else in the slots is used.
    private Job? FindJob(Entity<SteamSmelterComponent> smelter)
    {
        if (_itemSlots.GetItemOrNull(smelter.Owner, SteamSmelterComponent.MoldSlotId) is not { } mold ||
            !TryComp<PressMoldComponent>(mold, out var shape))
        {
            return null;
        }

        var items = new List<(EntityUid Item, StackComponent Stack)>();
        foreach (var slotId in SteamSmelterComponent.MaterialSlotIds)
        {
            if (_itemSlots.GetItemOrNull(smelter.Owner, slotId) is { } item && TryComp<StackComponent>(item, out var stack))
                items.Add((item, stack));
        }

        if (items.Count == 0)
            return null;

        foreach (var recipe in _proto.EnumeratePrototypes<SteamSmeltRecipePrototype>())
        {
            if (recipe.Shape != shape.Shape)
                continue;

            var use = new List<(EntityUid, int)>();
            var taken = new HashSet<EntityUid>();
            var matched = true;

            foreach (var (stackId, amount) in recipe.Inputs)
            {
                var found = false;
                foreach (var (item, stack) in items)
                {
                    if (taken.Contains(item) || stack.StackTypeId != stackId || stack.Count < amount)
                        continue;

                    taken.Add(item);
                    use.Add((item, amount));
                    found = true;
                    break;
                }

                if (!found)
                {
                    matched = false;
                    break;
                }
            }

            if (!matched)
                continue;

            var job = new Job
            {
                Id = recipe.ID,
                Duration = recipe.Duration,
                Result = recipe.Result,
                ResultAmount = recipe.ResultAmount,
                Slag = smelter.Comp.ProducesSlag ? recipe.Slag : 0f,
            };
            job.Consumed.AddRange(use);
            return job;
        }

        return null;
    }

    // The output slot is empty, or holds the same pieces and has room for more of them.
    private bool CanTakeOutput(Entity<SteamSmelterComponent> smelter, Job job)
    {
        if (_itemSlots.GetItemOrNull(smelter.Owner, SteamSmelterComponent.OutputSlotId) is not { } held)
            return true;

        return TryComp<StackComponent>(held, out var stack) && stack.StackTypeId == job.Result &&
               _stack.GetAvailableSpace(stack) >= job.ResultAmount;
    }

    private void Complete(Entity<SteamSmelterComponent> smelter, Job job)
    {
        var comp = smelter.Comp;
        comp.Progress = 0f;
        comp.ActiveRecipe = null;
        comp.Active = false;

        foreach (var (item, amount) in job.Consumed)
        {
            if (!TryComp<StackComponent>(item, out var stack))
                continue;

            if (stack.Count <= amount)
                PredictedQueueDel(item);
            else
                _stack.SetCount((item, stack), stack.Count - amount);
        }

        _fluids.Add(smelter.Owner, FluidKinds.Slag, job.Slag, comp.SlagLimit);

        if (_proto.TryIndex(job.Result, out var resultProto) && !string.IsNullOrEmpty(resultProto.Spawn.Id))
        {
            var made = Spawn(resultProto.Spawn, Transform(smelter).Coordinates);
            if (TryComp<StackComponent>(made, out var madeStack))
                _stack.SetCount((made, madeStack), job.ResultAmount);

            if (_itemSlots.GetItemOrNull(smelter.Owner, SteamSmelterComponent.OutputSlotId) is { } held &&
                TryComp<StackComponent>(held, out var heldStack))
            {
                _stack.TryMergeStacks(made, (held, heldStack), out _);
                if (!TerminatingOrDeleted(made))
                    Del(made);
            }
            else
            {
                _itemSlots.TryInsert(smelter.Owner, SteamSmelterComponent.OutputSlotId, made, null);
            }
        }

        _audio.PlayPvs(CompleteSound, smelter);
        Dirty(smelter);
    }

    private void OnExamined(Entity<SteamSmelterComponent> smelter, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using var block = args.PushGroup(nameof(SteamSmelterComponent));
        args.PushMarkup(Loc.GetString(smelter.Comp.Active ? "au14-steam-smelter-examine-working" : "au14-steam-smelter-examine-idle",
            ("percent", (int) (smelter.Comp.Progress * 100))));
    }
}
