using System.Collections.Generic;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Anatomy.Bones.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Medical.HUD;
using Content.Shared._RMC14.Medical.HUD.Components;
using Content.Shared._RMC14.Medical.HUD.Systems;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Medical.Diagnostics.Holocards;

/// <summary>Projects current medical conditions without taking ownership of a user's annotation.</summary>
public sealed partial class AutoHolocardSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private HolocardSystem _holocard = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;

    private static readonly ProtoId<OrganCategoryPrototype> Brain = "Brain";
    private const CMUMedicalChangeFlags IndicatorChanges =
        CMUMedicalChangeFlags.Anatomy | CMUMedicalChangeFlags.Fractures |
        CMUMedicalChangeFlags.Organs | CMUMedicalChangeFlags.Wounds | CMUMedicalChangeFlags.Topology;

    private bool _medicalEnabled;
    private bool _diagnosticsEnabled;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FractureComponent, ComponentStartup>(OnFractureSpawn);
        SubscribeLocalEvent<InternalBleedingComponent, ComponentStartup>(OnInternalBleedSpawn);
        SubscribeLocalEvent<FractureSeverityChangedEvent>(OnFractureChanged);
        SubscribeLocalEvent<InternalBleedingChangedEvent>(OnInternalBleedingChanged);
        SubscribeLocalEvent<VictimInfectionChangedEvent>(OnInfectionChanged);
        SubscribeLocalEvent<CMUMedicalChangedEvent>(OnMedicalChanged);
        SubscribeLocalEvent<OrganStageChangedEvent>(OnOrganStageChanged);
        _cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => SetEnabled(ref _medicalEnabled, v), true);
        _cfg.OnValueChanged(CMUMedicalCCVars.DiagnosticsEnabled, v => SetEnabled(ref _diagnosticsEnabled, v), true);
    }

    private void SetEnabled(ref bool field, bool value)
    {
        if (field == value)
            return;
        field = value;
        // Configuration must release stale automatic labels on paused bodies too.
        var patients = new List<EntityUid>();
        var query = EntityManager.AllEntityQueryEnumerator<HolocardStateComponent>();
        while (query.MoveNext(out var uid, out _))
            patients.Add(uid);
        foreach (var patient in patients)
            Reconcile(patient);
    }

    private void OnFractureSpawn(Entity<FractureComponent> ent, ref ComponentStartup args)
    {
        if (TryGetBodyForPart(ent.Owner) is { } body)
            Reconcile(body);
    }

    private void OnInternalBleedSpawn(Entity<InternalBleedingComponent> ent, ref ComponentStartup args)
    {
        if (TryGetBodyForPart(ent.Owner) is { } body)
            Reconcile(body);
    }

    private void OnFractureChanged(ref FractureSeverityChangedEvent args) => Reconcile(args.Body);
    private void OnInternalBleedingChanged(ref InternalBleedingChangedEvent args) => Reconcile(args.Body);
    private void OnInfectionChanged(ref VictimInfectionChangedEvent args) => Reconcile(args.Victim);
    private void OnOrganStageChanged(ref OrganStageChangedEvent args) => Reconcile(args.Body);

    private void OnMedicalChanged(ref CMUMedicalChangedEvent args)
    {
        if ((args.Changes & IndicatorChanges) != 0)
            Reconcile(args.Body);
    }

    private void Reconcile(EntityUid body)
    {
        if (TerminatingOrDeleted(body) || EntityManager.IsQueuedForDeletion(body) ||
            !HasComp<CMUHumanMedicalComponent>(body) || !TryComp<HolocardStateComponent>(body, out var holocard))
            return;

        if (holocard.BrainRemovalAssessment)
        {
            // Temporary extraction does not create an irreversible diagnosis. A
            // compatible reattached brain resolves this source, including donors.
            foreach (var organ in _medicalIndex.GetOrgans(body))
            {
                if (organ.Comp.Category == Brain && IsAttachedOrgan(organ.Owner, body))
                {
                    _holocard.SetBrainRemovalAssessment((body, holocard), false);
                    break;
                }
            }
            if (TerminatingOrDeleted(body) || EntityManager.IsQueuedForDeletion(body) ||
                !TryComp<HolocardStateComponent>(body, out var current) || !ReferenceEquals(current, holocard))
                return;
        }
        _holocard.SetAutomaticStatus((body, holocard),
            _medicalEnabled && _diagnosticsEnabled ? GetAutomaticStatus(body) : HolocardStatus.None);
    }

    private HolocardStatus GetAutomaticStatus(EntityUid body)
    {
        if (TryComp<VictimInfectedComponent>(body, out var infection) && infection.LifeStage <= ComponentLifeStage.Running)
            return HolocardStatus.Xeno;

        foreach (var (organUid, _) in _medicalIndex.GetOrgans(body))
        {
            if (IsAttachedOrgan(organUid, body) && TryComp<OrganHealthComponent>(organUid, out var organ) &&
                organ.Stage.IsAtLeast(OrganDamageStage.Failing))
                return HolocardStatus.OrganFailure;
        }
        foreach (var (partUid, part) in _medicalIndex.GetBodyParts(body))
        {
            if (part.Body != body || TerminatingOrDeleted(partUid) || EntityManager.IsQueuedForDeletion(partUid))
                continue;
            if (TryComp<FractureComponent>(partUid, out var fracture) &&
                fracture.LifeStage <= ComponentLifeStage.Running && fracture.Severity != FractureSeverity.None ||
                TryComp<InternalBleedingComponent>(partUid, out var bleeding) &&
                bleeding.LifeStage <= ComponentLifeStage.Running && bleeding.BloodlossPerSecond > 0)
                return HolocardStatus.Trauma;
        }
        return HolocardStatus.None;
    }

    private bool IsAttachedOrgan(EntityUid organ, EntityUid body)
        => !TerminatingOrDeleted(organ) && !EntityManager.IsQueuedForDeletion(organ) &&
           TryComp<OrganComponent>(organ, out var component) && component.Body == body &&
           TryComp<ChildOrganComponent>(organ, out var child) && child.Parent is { } parent &&
           !TerminatingOrDeleted(parent) && !EntityManager.IsQueuedForDeletion(parent) &&
           TryComp<BodyPartComponent>(parent, out var part) && part.Body == body;

    private EntityUid? TryGetBodyForPart(EntityUid part)
        => TryComp<BodyPartComponent>(part, out var component) ? component.Body : null;
}
