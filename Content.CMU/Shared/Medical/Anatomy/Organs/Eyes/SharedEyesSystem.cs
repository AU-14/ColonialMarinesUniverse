using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.Body.Events;
using Content.Shared.Eye.Blinding.Systems;
using Robust.Shared.GameObjects;

namespace Content.Shared._CMU14.Medical.Anatomy.Organs.Eyes;

public abstract partial class SharedEyesSystem : EntitySystem
{
    [Dependency] protected BlindableSystem Blindable = default!;
    [Dependency] protected CMUMedicalBodyIndexSystem MedicalIndex = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EyesComponent, OrganStageChangedEvent>(OnStageChanged);
        SubscribeLocalEvent<EyesComponent, OrganRemovedFromBodyEvent>(OnEyesRemovedFromBody);
        SubscribeLocalEvent<EyesComponent, OrganAddedToBodyEvent>(OnEyesAddedToBody);
        SubscribeLocalEvent<CMUOrganBlindnessComponent, ComponentStartup>(OnOrganBlindnessStartup);
        SubscribeLocalEvent<CMUOrganBlindnessComponent, ComponentShutdown>(OnOrganBlindnessShutdown);
        SubscribeLocalEvent<CMUOrganBlindnessComponent, CanSeeAttemptEvent>(OnOrganBlindnessCanSee);
    }

    private void OnStageChanged(Entity<EyesComponent> ent, ref OrganStageChangedEvent args)
    {
        var body = args.Body;
        var bestStage = ComputeBestEyeStage(body);
        UpdateVisionStatus(body, bestStage);
    }

    private void OnEyesRemovedFromBody(Entity<EyesComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        if (TerminatingOrDeleted(args.OldBody))
            return;

        UpdateVisionStatus(args.OldBody, ComputeBestEyeStage(args.OldBody, excludedOrgan: ent.Owner));
    }

    private void OnEyesAddedToBody(Entity<EyesComponent> ent, ref OrganAddedToBodyEvent args)
    {
        UpdateVisionStatus(args.Body, ComputeBestEyeStage(args.Body, includedOrgan: ent.Owner));
    }

    private void OnOrganBlindnessStartup(Entity<CMUOrganBlindnessComponent> ent, ref ComponentStartup args)
    {
        Blindable.UpdateIsBlind(ent.Owner);
    }

    private void OnOrganBlindnessShutdown(Entity<CMUOrganBlindnessComponent> ent, ref ComponentShutdown args)
    {
        Blindable.UpdateIsBlind(ent.Owner);
    }

    private void OnOrganBlindnessCanSee(Entity<CMUOrganBlindnessComponent> ent, ref CanSeeAttemptEvent args)
    {
        if (ent.Comp.LifeStage <= ComponentLifeStage.Running)
            args.Cancel();
    }

    /// <summary>
    ///     Best (lowest enum value) stage across all <see cref="EyesComponent"/>
    ///     organs in the body. A marine with one healthy eye → Healthy aggregate.
    /// </summary>
    protected OrganDamageStage ComputeBestEyeStage(
        EntityUid body,
        EntityUid? excludedOrgan = null,
        EntityUid? includedOrgan = null)
    {
        var best = OrganDamageStage.Dead;
        var any = false;

        if (includedOrgan is { } included &&
            HasComp<EyesComponent>(included) &&
            TryComp<OrganHealthComponent>(included, out var includedHealth))
        {
            best = includedHealth.Stage;
            any = true;
        }

        foreach (var (organId, _) in MedicalIndex.GetOrgans(body))
        {
            if (organId == excludedOrgan || organId == includedOrgan)
                continue;
            if (!HasComp<EyesComponent>(organId))
                continue;
            if (!TryComp<OrganHealthComponent>(organId, out var oh))
                continue;
            if (!any || (byte)oh.Stage < (byte)best)
                best = oh.Stage;
            any = true;
        }

        return any ? best : OrganDamageStage.Dead;
    }

    protected virtual void UpdateVisionStatus(EntityUid body, OrganDamageStage stage)
    {
    }
}
