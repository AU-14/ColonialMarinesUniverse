// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Server.Popups;
using Content.Shared.CMU14.Trade;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Server.CMU14.Trade;

// The packer takes cardboard and whatever is put in it, and when it is switched on it seals the things in a parcel. A parcel is torn open by hand, which gives the things back.
public sealed class TradePackerSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedStackSystem _stacks = default!;

    private static readonly SoundSpecifier PackSound = new SoundPathSpecifier("/Audio/Effects/box_deploy.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TradePackerComponent, ComponentInit>(OnPackerInit);
        SubscribeLocalEvent<TradePackerComponent, InteractUsingEvent>(OnPackerUsing);
        SubscribeLocalEvent<TradePackerComponent, ActivateInWorldEvent>(OnPackerActivate);
        SubscribeLocalEvent<TradePackerComponent, TradePackDoAfterEvent>(OnPacked);
        SubscribeLocalEvent<TradePackerComponent, GetVerbsEvent<AlternativeVerb>>(OnPackerVerbs);
        SubscribeLocalEvent<TradePackerComponent, ExaminedEvent>(OnPackerExamined);

        SubscribeLocalEvent<TradeParcelComponent, ComponentInit>(OnParcelInit);
        SubscribeLocalEvent<TradeParcelComponent, GetVerbsEvent<AlternativeVerb>>(OnParcelVerbs);
        SubscribeLocalEvent<TradeParcelComponent, TradeUnpackDoAfterEvent>(OnUnpacked);
        SubscribeLocalEvent<TradeParcelComponent, ExaminedEvent>(OnParcelExamined);
    }

    private void OnPackerInit(Entity<TradePackerComponent> packer, ref ComponentInit args)
    {
        _container.EnsureContainer<Container>(packer, TradePackerComponent.ContainerId);
    }

    private void OnParcelInit(Entity<TradeParcelComponent> parcel, ref ComponentInit args)
    {
        _container.EnsureContainer<Container>(parcel, TradeParcelComponent.ContainerId);
    }

    private BaseContainer Items(Entity<TradePackerComponent> packer)
    {
        return _container.EnsureContainer<Container>(packer, TradePackerComponent.ContainerId);
    }

    // How many sheets of cardboard the things in the packer take.
    public static int SheetsFor(TradePackerComponent packer, int items)
    {
        return Math.Max(1, (items + packer.ItemsPerSheet - 1) / packer.ItemsPerSheet);
    }

    private void OnPackerUsing(Entity<TradePackerComponent> packer, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var used = args.Used;

        if (TryComp<StackComponent>(used, out var stack) && stack.StackTypeId == packer.Comp.CardboardStack)
        {
            args.Handled = true;

            var room = packer.Comp.MaxCardboard - packer.Comp.Cardboard;
            if (room <= 0)
            {
                _popup.PopupEntity(Loc.GetString("rumc-trade-packer-cardboard-full"), packer, args.User, PopupType.SmallCaution);
                return;
            }

            var taken = Math.Min(room, stack.Count);
            packer.Comp.Cardboard += taken;
            _stacks.SetCount((used, stack), stack.Count - taken);
            _popup.PopupEntity(Loc.GetString("rumc-trade-packer-cardboard-loaded", ("count", packer.Comp.Cardboard)), packer, args.User);
            return;
        }

        // Anything else that can be carried goes into the packer to be packed.
        if (HasComp<MobStateComponent>(used) || HasComp<TradeParcelComponent>(used) || HasComp<TradePackerComponent>(used))
            return;

        args.Handled = true;

        var items = Items(packer);
        if (items.ContainedEntities.Count >= packer.Comp.MaxItems)
        {
            _popup.PopupEntity(Loc.GetString("rumc-trade-packer-full"), packer, args.User, PopupType.SmallCaution);
            return;
        }

        if (!_hands.TryDrop(args.User, used, checkActionBlocker: false, doDropInteraction: false) || !_container.Insert(used, items))
        {
            _popup.PopupEntity(Loc.GetString("rumc-trade-packer-cannot-take", ("item", used)), packer, args.User, PopupType.SmallCaution);
            return;
        }

        _popup.PopupEntity(Loc.GetString("rumc-trade-packer-item-loaded", ("item", used), ("count", items.ContainedEntities.Count)), packer, args.User);
    }

    private void OnPackerActivate(Entity<TradePackerComponent> packer, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = true;

        var count = Items(packer).ContainedEntities.Count;
        if (count == 0)
        {
            _popup.PopupEntity(Loc.GetString("rumc-trade-packer-empty"), packer, args.User, PopupType.SmallCaution);
            return;
        }

        var sheets = SheetsFor(packer, count);
        if (packer.Comp.Cardboard < sheets)
        {
            _popup.PopupEntity(Loc.GetString("rumc-trade-packer-no-cardboard", ("need", sheets), ("have", packer.Comp.Cardboard)), packer, args.User, PopupType.SmallCaution);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, packer.Comp.PackTime, new TradePackDoAfterEvent(), packer, target: packer)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("rumc-trade-packer-started"), packer, args.User);
    }

    private void OnPacked(Entity<TradePackerComponent> packer, ref TradePackDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var items = Items(packer);
        var count = items.ContainedEntities.Count;
        var sheets = SheetsFor(packer, count);
        if (count == 0 || packer.Comp.Cardboard < sheets)
            return;

        var parcel = Spawn(packer.Comp.ParcelPrototype, Transform(packer).Coordinates);
        var inside = _container.EnsureContainer<Container>(parcel, TradeParcelComponent.ContainerId);

        foreach (var thing in items.ContainedEntities.ToArray())
            _container.Insert(thing, inside);

        packer.Comp.Cardboard -= sheets;
        _audio.PlayPvs(PackSound, packer);
        _popup.PopupEntity(Loc.GetString("rumc-trade-packer-done"), packer, args.User);
    }

    private void OnPackerVerbs(Entity<TradePackerComponent> packer, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || Items(packer).ContainedEntities.Count == 0)
            return;

        var target = packer;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("rumc-trade-packer-take-out"),
            Act = () => _container.EmptyContainer(Items(target), destination: Transform(target).Coordinates),
            Priority = 5,
        });
    }

    private void OnPackerExamined(Entity<TradePackerComponent> packer, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var count = Items(packer).ContainedEntities.Count;
        args.PushMarkup(Loc.GetString("rumc-trade-packer-examine",
            ("cardboard", packer.Comp.Cardboard),
            ("items", count),
            ("need", SheetsFor(packer, count))));
    }

    private void OnParcelVerbs(Entity<TradeParcelComponent> parcel, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var target = parcel;
        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("rumc-trade-parcel-open"),
            Act = () =>
            {
                var doAfter = new DoAfterArgs(EntityManager, user, target.Comp.UnpackTime, new TradeUnpackDoAfterEvent(), target, target: target)
                {
                    BreakOnMove = true,
                    BreakOnDamage = true,
                    NeedHand = true,
                };

                _doAfter.TryStartDoAfter(doAfter);
            },
            Priority = 5,
        });
    }

    private void OnUnpacked(Entity<TradeParcelComponent> parcel, ref TradeUnpackDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (_container.TryGetContainer(parcel, TradeParcelComponent.ContainerId, out var inside))
            _container.EmptyContainer(inside, destination: Transform(parcel).Coordinates);

        _popup.PopupEntity(Loc.GetString("rumc-trade-parcel-opened"), parcel, args.User);
        QueueDel(parcel);
    }

    private void OnParcelExamined(Entity<TradeParcelComponent> parcel, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var count = _container.TryGetContainer(parcel, TradeParcelComponent.ContainerId, out var inside) ? inside.ContainedEntities.Count : 0;
        args.PushMarkup(Loc.GetString("rumc-trade-parcel-examine", ("count", count)));
    }
}
