// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Server.Atmos.EntitySystems;
using Content.Shared._RMC14.Campfire;
using Content.Shared.Atmos.Components;
using Content.Shared.CMU14.Cooking;
using Content.Shared.CMU14.Forge;
using Content.Shared.DoAfter;
using Content.Shared.IgnitionSource;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Forge;

// The flint striker lights what can be lit with a spark: a campfire or a stove that has fuel, and a thing that burns (but not a living thing). The striker is an ignition
// source for the moment of a caught spark, so the fires take it as they take any lit thing and keep their own rules (the fuel is wanted, a lit fire is not lit again).
public sealed class FlintStrikerSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedIgnitionSourceSystem _ignition = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly ToolWearSystem _wear = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly SoundSpecifier SparkSound = new SoundPathSpecifier("/Audio/Effects/sparks4.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FlintStrikerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<FlintStrikerComponent, FlintStrikeDoAfterEvent>(OnStruck);
    }

    // What a spark can light.
    public bool CanLight(EntityUid target)
    {
        if (HasComp<CampfireComponent>(target))
            return true;

        if (TryComp<CookingStoveComponent>(target, out var stove) && !stove.Electric)
            return true;

        return HasComp<FlammableComponent>(target) && !HasComp<MobStateComponent>(target);
    }

    private void OnAfterInteract(Entity<FlintStrikerComponent> striker, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !CanLight(target))
            return;

        args.Handled = true;
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, striker.Comp.Seconds, new FlintStrikeDoAfterEvent(), striker, target: target, used: striker)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });
    }

    private void OnStruck(Entity<FlintStrikerComponent> striker, ref FlintStrikeDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;
        _audio.PlayPvs(SparkSound, striker);

        if (_random.Prob(striker.Comp.Chance))
            Light(striker, target, args.User);
        else
            _popup.PopupEntity(Loc.GetString("au14-striker-miss"), striker, args.User);

        // The stones wear with every strike, caught or not.
        _wear.Use(striker.Owner, args.User);
    }

    // A caught spark: whether it lit what it was held to.
    public bool Light(Entity<FlintStrikerComponent> striker, EntityUid target, EntityUid user)
    {
        if (HasComp<CampfireComponent>(target) || TryComp<CookingStoveComponent>(target, out var stove) && !stove.Electric)
        {
            // Held to the fire as a lit thing for the moment. Only these two get it, so that no other use of an item on a thing (putting it in) is taken for it.
            EnsureComp<IgnitionSourceComponent>(striker);
            _ignition.SetIgnited(striker.Owner, true);
            var use = new InteractUsingEvent(user, striker, target, Transform(target).Coordinates);
            RaiseLocalEvent(target, use);
            _ignition.SetIgnited(striker.Owner, false);

            // The fire answers a lit thing also with "it has no fuel", so it is the fire that tells whether it was lit.
            return TryComp<CampfireComponent>(target, out var campfire) ? campfire.Lit : TryComp<CookingStoveComponent>(target, out var lit) && lit.Lit;
        }

        if (TryComp<FlammableComponent>(target, out var flammable) && !HasComp<MobStateComponent>(target))
        {
            _flammable.AdjustFireStacks(target, striker.Comp.FireStacks, flammable);
            _flammable.Ignite(target, striker, flammable, user);
            if (flammable.OnFire)
                _popup.PopupEntity(Loc.GetString("au14-striker-set-fire", ("target", target)), target, user);

            return flammable.OnFire;
        }

        return false;
    }
}
