// ReSharper disable CheckNamespace

using Content.Shared._RMC14.ShakeStun;
using Content.Shared._RMC14.Xenonids.Construction.Nest;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Random;

namespace Content.Shared._RMC14.Xenonids.Parasite;

// CMU14: how long a parasite holds on to a face, and shaking it loose.
public abstract partial class SharedXenoParasiteSystem
{
    private void CMUInitialize()
    {
        SubscribeLocalEvent<InfectableComponent, InteractHandEvent>(CMUOnInfectableInteractHand,
            before: [typeof(StunShakeableSystem), typeof(InteractionPopupSystem)]);
    }

    /// <summary>
    ///     A parasite holds on for a random two to five minutes. The server picks the time; until its state
    ///     arrives the client assumes the longest, so it never predicts the parasite letting go early.
    ///     Nested hosts use the shorter infection delay instead.
    /// </summary>
    private void CMUSetHugTime(Entity<XenoParasiteComponent> parasite)
    {
        var now = _timing.CurTime;
        var hold = _net.IsServer
            ? TimeSpan.FromSeconds(_random.NextDouble(parasite.Comp.CMUMinHugTime.TotalSeconds, parasite.Comp.CMUMaxHugTime.TotalSeconds))
            : parasite.Comp.CMUMaxHugTime;

        if (parasite.Comp.InfectedVictim is { } victim &&
            TryComp<XenoNestedComponent>(victim, out var nested) &&
            !nested.Detached)
        {
            hold = hold < parasite.Comp.FallOffDelay ? hold : parasite.Comp.FallOffDelay;
        }

        parasite.Comp.CMUAttachedAt = now;
        parasite.Comp.FallOffAt = now + hold;
    }

    private void CMUUpdateNestedHugTime(Entity<XenoParasiteComponent> parasite)
    {
        if (!_net.IsServer ||
            parasite.Comp.FellOff ||
            parasite.Comp.FallOffAt is not { } fallOffAt ||
            parasite.Comp.InfectedVictim is not { } victim ||
            !TryComp<XenoNestedComponent>(victim, out var nested) ||
            nested.Detached)
        {
            return;
        }

        var nestedFallOffAt = _timing.CurTime + parasite.Comp.FallOffDelay;
        if (fallOffAt <= nestedFallOffAt)
            return;

        parasite.Comp.FallOffAt = nestedFallOffAt;
        Dirty(parasite);
    }

    /// <summary>
    ///     Nobody can shake a parasite off for its first two minutes: it tightens around the host's neck and they
    ///     think better of it. After that, each shake or pat from someone else has a chance of knocking it loose,
    ///     and the shake or pat itself goes on as normal.
    /// </summary>
    private void CMUOnInfectableInteractHand(Entity<InfectableComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || args.User == args.Target || !ent.Comp.BeingInfected)
            return;

        if (!_inventory.TryGetSlotEntity(ent, "mask", out var mask) ||
            !TryComp(mask, out XenoParasiteComponent? parasite) ||
            parasite.InfectedVictim != ent.Owner ||
            parasite.FellOff)
        {
            return;
        }

        // The client doesn't know how long the parasite has held on, so it leaves the shake to the server.
        if (!_net.IsServer)
        {
            args.Handled = true;
            return;
        }

        if (parasite.CMUAttachedAt is not { } attachedAt)
            return;

        var now = _timing.CurTime;
        if (now < attachedAt + parasite.CMUMinHugTime)
        {
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("cmu-xeno-parasite-constricts", ("parasite", mask.Value), ("victim", ent.Owner)),
                ent, args.User, PopupType.MediumCaution);
            return;
        }

        if (!_random.Prob(parasite.CMUShakeOffChance))
            return;

        parasite.FallOffAt = now;
        Dirty(mask.Value, parasite);
        _popup.PopupEntity(Loc.GetString("cmu-xeno-parasite-shaken-loose", ("parasite", mask.Value), ("victim", ent.Owner)),
            ent, PopupType.MediumCaution);
    }
}
