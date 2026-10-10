using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Treatment.FirstAid;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.Administration.Logs;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Database;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;

namespace Content.Server.CMU14.Yautja;

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
