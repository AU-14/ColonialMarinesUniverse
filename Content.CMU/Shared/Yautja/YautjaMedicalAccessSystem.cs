using Content.Shared.CMU14.Yautja;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;

namespace Content.Shared.CMU14.Yautja;

/// <summary>
/// CMU14: CM-SS13 only lets tech users work the yautja healing crystals.
/// code/game/objects/items/reagent_containers/autoinjectors.dm:463-467:
/// <code>
/// /obj/item/reagent_container/hypospray/autoinjector/yautja/attack(mob/M, mob/user)
///     if(HAS_TRAIT(user, TRAIT_YAUTJA_TECH))
///         ..()
///     else
///         to_chat(user, SPAN_DANGER("You have no idea where to inject [src]."))
/// </code>
/// Thralls hold that trait (yaut_hudprocs.dm:526), so they keep their orange dathwei crystals.
/// This lives in the shared assembly so the client predicts the refusal too; a server-only check let the
/// injection start (and drain the crystal) for a frame before the server cancelled it.
/// </summary>
public sealed class YautjaMedicalAccessSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<YautjaMedicalItemComponent, UseInHandEvent>(
            OnInjectSelf, before: [typeof(InjectorSystem)]);
        SubscribeLocalEvent<YautjaMedicalItemComponent, AfterInteractEvent>(
            OnInjectTarget, before: [typeof(InjectorSystem)]);
    }

    private void OnInjectSelf(Entity<YautjaMedicalItemComponent> ent, ref UseInHandEvent args)
    {
        if (ShouldDeny(ent, args.User))
            args.Handled = true;
    }

    private void OnInjectTarget(Entity<YautjaMedicalItemComponent> ent, ref AfterInteractEvent args)
    {
        if (ShouldDeny(ent, args.User))
            args.Handled = true;
    }

    private bool ShouldDeny(Entity<YautjaMedicalItemComponent> ent, EntityUid user)
    {
        if (!HasComp<HyposprayComponent>(ent.Owner) || CanUseYautjaTech(user))
            return false;

        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-thwei-no-idea", ("item", Name(ent.Owner))),
            user,
            user,
            PopupType.SmallCaution);

        return true;
    }

    /// <summary>
    /// CM-SS13's <c>HAS_TRAIT(user, TRAIT_YAUTJA_TECH)</c>: Yautja, blooded thralls and any other
    /// tech-authorized user may inject; nobody else can.
    /// </summary>
    private bool CanUseYautjaTech(EntityUid user)
    {
        return HasComp<YautjaComponent>(user) ||
               HasComp<YautjaTechAuthorizedComponent>(user) ||
               (TryComp(user, out YautjaThrallComponent? thrall) && thrall.Blooded && thrall.TechAuthorized);
    }
}
