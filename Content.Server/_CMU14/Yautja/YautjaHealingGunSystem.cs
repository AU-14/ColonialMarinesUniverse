using Content.Shared._CMU14.Yautja;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;

namespace Content.Server._CMU14.Yautja;

/// <summary>
///     Handles only the discrete CMSS13 healing-gel reload. Treatment itself
///     is deliberately owned by the shared CMU Medicomp surgery flow.
/// </summary>
public sealed partial class YautjaHealingGunSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaHealingGunComponent, AfterInteractUsingEvent>(OnAfterInteractUsing);
    }

    private void OnAfterInteractUsing(Entity<YautjaHealingGunComponent> ent, ref AfterInteractUsingEvent args)
    {
        if (args.Handled || !args.CanReach || !HasComp<YautjaHealingCapsuleComponent>(args.Used))
        {
            return;
        }

        if (ent.Comp.Loaded)
        {
            _popup.PopupClient("The healing gun is already loaded.", ent.Owner, args.User);
            return;
        }

        Del(args.Used);
        ent.Comp.Loaded = true;
        Dirty(ent);
        args.Handled = true;

        if (ent.Comp.ReloadSound is { } reloadSound)
            _audio.PlayPvs(reloadSound, ent.Owner);
    }
}
