using Content.Shared.CMU14.Hygiene;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared._RMC14.Chemistry;
using System;

namespace Content.Server.CMU14.Hygiene;

public sealed partial class CMUSinkWashSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;

    [Dependency] private SharedPopupSystem _popup = default!;

    [Dependency] private CMUResidueSystem _residue = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RMCSinkWaterComponent, CMUSinkWashRequestEvent>(OnWashRequested);
        SubscribeLocalEvent<RMCSinkWaterComponent, CMUSinkWashDoAfterEvent>(OnFinished);
    }

    private void OnWashRequested(EntityUid uid, RMCSinkWaterComponent component, CMUSinkWashRequestEvent args)
    {
        var action = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(7),
            new CMUSinkWashDoAfterEvent(), uid, uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BlockDuplicate = true,
        };
        if (_doAfter.TryStartDoAfter(action))
            _popup.PopupEntity(Loc.GetString("cmu-sink-wash-start"), args.User, args.User);
    }

    private void OnFinished(Entity<RMCSinkWaterComponent> ent, ref CMUSinkWashDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;
        args.Handled = true;
        _residue.Clean(args.User);
        _popup.PopupEntity(Loc.GetString("cmu-sink-wash-done"), args.User, args.User);
    }
}
