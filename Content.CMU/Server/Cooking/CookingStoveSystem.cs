// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Server.Power.Components;
using Content.Server.Stack;
using Content.Shared.Audio;
using Content.Shared.CMU14.Cooking;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.IgnitionSource;
using Content.Shared.Interaction;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Cooking;

// The stoves. A stove of wood is used so: the pot is put on it (the slot on top), the fuel is put in through the door, a lit thing (a lighter, a match, a torch) is
// put to it to light the fire, and then the fire burns the fuel and heats the pot, until the fuel is out. The electric oven has no fire: it heats while it is powered
// and its door is shut.
public sealed class CookingStoveSystem : EntitySystem
{
    [Dependency] private readonly SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPointLightSystem _light = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly CookingSystem _cooking = default!;

    private static readonly SoundSpecifier OpenSound = new SoundPathSpecifier("/Audio/Effects/closetopen.ogg");
    private static readonly SoundSpecifier CloseSound = new SoundPathSpecifier("/Audio/Effects/closetclose.ogg");

    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CookingStoveComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CookingStoveComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<CookingStoveComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CookingStoveComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<CookingStoveComponent, ExaminedEvent>(OnExamined);
    }

    private void OnMapInit(Entity<CookingStoveComponent> stove, ref MapInitEvent args)
    {
        Apply(stove);
    }

    private void OnActivate(Entity<CookingStoveComponent> stove, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = true;
        Toggle(stove, args.User);
    }

    // A lit thing put to a stove that has fuel lights its fire.
    private void OnInteractUsing(Entity<CookingStoveComponent> stove, ref InteractUsingEvent args)
    {
        if (args.Handled || stove.Comp.Electric)
            return;

        if (!TryComp<IgnitionSourceComponent>(args.Used, out var source) || !source.Ignited)
            return;

        args.Handled = true;

        if (stove.Comp.Lit)
        {
            _popup.PopupEntity(Loc.GetString("au14-stove-already-lit"), stove, args.User);
            return;
        }

        if (!HasFuel(stove))
        {
            _popup.PopupEntity(Loc.GetString("au14-stove-no-fuel"), stove, args.User);
            return;
        }

        SetLit(stove, true);
        _popup.PopupEntity(Loc.GetString("au14-stove-lit-by-user"), stove, args.User);
    }

    private void OnGetVerbs(Entity<CookingStoveComponent> stove, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(stove.Comp.Open ? "au14-stove-close" : "au14-stove-open"),
            Priority = 5,
            Act = () => Toggle(stove, user),
        });
    }

    public void Toggle(Entity<CookingStoveComponent> stove, EntityUid? user)
    {
        stove.Comp.Open = !stove.Comp.Open;
        Dirty(stove);
        Apply(stove);

        _audio.PlayPvs(stove.Comp.Open ? OpenSound : CloseSound, stove);
    }

    // The door decides what can be reached: the fuel is put in through it. The pot is put on top of a stove of wood at any time, and into an oven only while its door is open.
    private void Apply(Entity<CookingStoveComponent> stove)
    {
        _appearance.SetData(stove, StoveVisuals.Open, stove.Comp.Open);

        if (stove.Comp.FuelSlot is { } fuel)
            _itemSlots.SetLock(stove.Owner, fuel, !stove.Comp.Open);

        foreach (var slot in stove.Comp.CookerSlots)
            _itemSlots.SetLock(stove.Owner, slot, stove.Comp.Electric && !stove.Comp.Open);
    }

    private void SetLit(Entity<CookingStoveComponent> stove, bool lit)
    {
        if (stove.Comp.Lit == lit)
            return;

        stove.Comp.Lit = lit;
        Dirty(stove);
        _appearance.SetData(stove, StoveVisuals.Lit, lit);
        _ambient.SetAmbience(stove, lit);
        _light.SetEnabled(stove, lit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CookingStoveComponent>();
        while (query.MoveNext(out var uid, out var stove))
        {
            if (_timing.CurTime < stove.NextTick)
                continue;

            stove.NextTick = _timing.CurTime + Tick;
            UpdateStove((uid, stove));
        }
    }

    private void UpdateStove(Entity<CookingStoveComponent> stove)
    {
        var comp = stove.Comp;

        if (comp.Electric)
        {
            var powered = !comp.Open && TryComp<ApcPowerReceiverComponent>(stove, out var power) && power.Powered;
            SetLit(stove, powered && AnyCooker(stove));

            if (powered)
                HeatAll(stove);

            return;
        }

        // A stove burns once it is lit, and goes out when the fuel is out.
        if (!comp.Lit)
            return;

        if (comp.FuelLeft <= 0 && !TryTakeFuel(stove))
        {
            SetLit(stove, false);
            return;
        }

        comp.FuelLeft -= (float) Tick.TotalSeconds;
        HeatAll(stove);
    }

    private bool HasFuel(Entity<CookingStoveComponent> stove)
    {
        if (stove.Comp.FuelLeft > 0)
            return true;

        return stove.Comp.FuelSlot is { } slot
               && _itemSlots.GetItemOrNull(stove.Owner, slot) is { } fuel
               && TryComp<StackComponent>(fuel, out var stack)
               && stove.Comp.Fuels.ContainsKey(stack.StackTypeId);
    }

    private bool AnyCooker(Entity<CookingStoveComponent> stove)
    {
        foreach (var slot in stove.Comp.CookerSlots)
        {
            if (_itemSlots.GetItemOrNull(stove.Owner, slot) != null)
                return true;
        }

        return false;
    }

    // One piece of the fuel of the slot is burnt; the stack of the fuel is used one by one.
    private bool TryTakeFuel(Entity<CookingStoveComponent> stove)
    {
        if (stove.Comp.FuelSlot is not { } slot || _itemSlots.GetItemOrNull(stove.Owner, slot) is not { } fuel)
            return false;

        if (!TryComp<StackComponent>(fuel, out var stack) || !stove.Comp.Fuels.TryGetValue(stack.StackTypeId, out var seconds))
            return false;

        stove.Comp.FuelLeft += seconds;

        if (stack.Count > 1)
            _stack.SetCount(fuel, stack.Count - 1, stack);
        else
            QueueDel(fuel);

        return true;
    }

    private void HeatAll(Entity<CookingStoveComponent> stove)
    {
        foreach (var slot in stove.Comp.CookerSlots)
        {
            if (_itemSlots.GetItemOrNull(stove.Owner, slot) is { } vessel && TryComp<FoodCookerComponent>(vessel, out var cooker))
                _cooking.Heat((vessel, cooker));
        }
    }

    private void OnExamined(Entity<CookingStoveComponent> stove, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(stove.Comp.Open ? "au14-stove-examine-open" : "au14-stove-examine-closed"));

        if (stove.Comp.Lit)
            args.PushMarkup(Loc.GetString("au14-stove-examine-lit"));
        else if (!stove.Comp.Electric)
            args.PushMarkup(Loc.GetString("au14-stove-examine-cold"));
    }
}
