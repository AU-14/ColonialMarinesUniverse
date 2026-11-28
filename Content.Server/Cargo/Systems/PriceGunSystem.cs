using Content.Server.Popups;
using Content.Server.Salvage.JobBoard;
using Content.Shared.Cargo.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Timing;
using Content.Shared.Cargo.Systems;
using Content.Server._RMC14.Requisitions; // CMU14
using Robust.Shared.Audio.Systems;

namespace Content.Server.Cargo.Systems;

public sealed partial class PriceGunSystem : SharedPriceGunSystem
{
    [Dependency] private RequisitionsSystem _requisitions = default!; // CMU14
    [Dependency] private UseDelaySystem _useDelay = default!;
    [Dependency] private PricingSystem _pricingSystem = default!;
    [Dependency] private PopupSystem _popupSystem = default!;
    [Dependency] private CargoSystem _bountySystem = default!;
    [Dependency] private SalvageJobBoardSystem _salvageJobBoard = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    protected override bool GetPriceOrBounty(Entity<PriceGunComponent> entity, EntityUid target, EntityUid user)
    {
        if (!TryComp(entity.Owner, out UseDelayComponent? useDelay) || _useDelay.IsDelayed((entity.Owner, useDelay)))
            return false;

        // CMU14 Begin: use the same cargo eligibility, contents, and paperwork valuation as the elevator.
        if (entity.Comp.OnlyASRSSellable)
        {
            var message = _requisitions.TryAppraiseAsrsCargo(target, out var value)
                ? Loc.GetString("cmu-asrs-appraisal-value", ("target", Identity.Entity(target, EntityManager)), ("value", $"{value:0.####}"))
                : Loc.GetString("cmu-asrs-appraisal-not-sellable");
            _popupSystem.PopupEntity(message, user, user);
        } // CMU14 End
        else if (_bountySystem.IsBountyComplete(target, out _))
        {
            _popupSystem.PopupEntity(Loc.GetString("price-gun-bounty-complete"), user, user);
        }
        else if (_salvageJobBoard.FulfillsSalvageJob(target, null, out _))
        {
            _popupSystem.PopupEntity(Loc.GetString("price-gun-salvjob-complete"), user, user);
        }
        else // Otherwise appraise the price
        {
            var price = _pricingSystem.GetPrice(target);
            _popupSystem.PopupEntity(Loc.GetString("price-gun-pricing-result",
                    ("object", Identity.Entity(target, EntityManager)),
                    ("price", $"{price:F2}")),
                user,
                user);
        }

        _audio.PlayPvs(entity.Comp.AppraisalSound, entity.Owner);
        _useDelay.TryResetDelay((entity.Owner, useDelay));
        return true;
    }
}
