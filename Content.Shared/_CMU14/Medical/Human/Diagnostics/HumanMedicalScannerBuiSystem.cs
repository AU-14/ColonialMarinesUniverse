using System.Collections.Generic;
using System.Linq;
using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Organs;
using Content.Shared._CMU14.Medical.Machines;
using Content.Shared._RMC14.Medical.Scanner;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using BodyPartSymmetry = Content.Shared.Body.Part.BodyPartSymmetry;
using BodyPartType = Content.Shared.Body.Part.BodyPartType;

namespace Content.Shared._CMU14.Medical.Human.Diagnostics;

public sealed class HumanMedicalScannerBuiSystem : EntitySystem
{
    public delegate string HumanMedicalScannerLocalizer(string key, params (string, object)[] args);

    private static readonly BodyRegion[] HeadRegions = { BodyRegion.Head };
    private static readonly BodyRegion[] TorsoRegions = { BodyRegion.Chest, BodyRegion.Groin };
    private static readonly BodyRegion[] LeftArmRegions = { BodyRegion.LeftArm };
    private static readonly BodyRegion[] RightArmRegions = { BodyRegion.RightArm };
    private static readonly BodyRegion[] LeftHandRegions = { BodyRegion.LeftHand };
    private static readonly BodyRegion[] RightHandRegions = { BodyRegion.RightHand };
    private static readonly BodyRegion[] LeftLegRegions = { BodyRegion.LeftLeg };
    private static readonly BodyRegion[] RightLegRegions = { BodyRegion.RightLeg };
    private static readonly BodyRegion[] LeftFootRegions = { BodyRegion.LeftFoot };
    private static readonly BodyRegion[] RightFootRegions = { BodyRegion.RightFoot };
    private static readonly OrganSlot[] BrainOrgans = { OrganSlot.Brain };
    private static readonly OrganSlot[] HeartOrgans = { OrganSlot.Heart };
    private static readonly OrganSlot[] LungOrgans = { OrganSlot.LeftLung, OrganSlot.RightLung };
    private static readonly OrganSlot[] LiverOrgans = { OrganSlot.Liver };
    private static readonly OrganSlot[] KidneyOrgans = { OrganSlot.Kidneys };
    private static readonly OrganSlot[] StomachOrgans = { OrganSlot.Stomach };
    private static readonly OrganSlot[] EyeOrgans = { OrganSlot.Eyes };
    private static readonly OrganSlot[] EarOrgans = { OrganSlot.Ears };
    private static readonly FixedPoint2 ScannerRegionMaxDamage = FixedPoint2.New(100);

    public static HumanMedicalScannerSummaryState BuildHudSummaryState(
        HumanMedicalComponent medical,
        HumanMedicalSummaryComponent? summaryComponent = null)
    {
        if (summaryComponent is not null)
            summaryComponent.Summary = medical.Summary;

        return new HumanMedicalScannerSummaryState(medical.Summary);
    }

    public static HumanMedicalScannerLedgerResponseMessage BuildLedgerResponse(
        HumanMedicalComponent medical,
        HumanMedicalScannerFullLedgerRequestMessage request)
    {
        if (request.KnownRevision == medical.Revision)
        {
            return new HumanMedicalScannerLedgerResponseMessage(
                HumanMedicalScannerResponseKind.NoChange,
                medical.Revision,
                fullLedger: null);
        }

        return new HumanMedicalScannerLedgerResponseMessage(
            HumanMedicalScannerResponseKind.FullLedger,
            medical.Revision,
            BuildFullLedgerDetail(medical));
    }

    public static HumanMedicalLedgerDetail BuildFullLedgerDetail(HumanMedicalComponent medical)
    {
        var summary = medical.DirtyFlags.HasFlag(MedicalDirtyFlags.Summary)
            ? MedicalSummaryBuilder.Build(medical)
            : medical.Summary;

        return new HumanMedicalLedgerDetail(
            medical.Revision,
            summary,
            (RegionState[]) medical.Regions.Clone(),
            medical.Injuries.ToArray(),
            (OrganState[]) medical.Organs.Clone(),
            medical.BleedSources.ToArray(),
            medical.ForeignObjects.ToArray(),
            medical.DetachedLimbs.ToArray());
    }

    public static void FillHealthScannerState(
        HumanMedicalComponent medical,
        HealthScannerBuiState state,
        bool includeFullLedger)
    {
        var detail = includeFullLedger
            ? BuildFullLedgerDetail(medical)
            : null;

        state.CMUHumanMedicalLedger = detail;
        state.CMUHumanMedicalSummary = detail?.Summary ?? medical.Summary;
        FillDamageProfile(medical, state);
        state.CMUParts = BuildBodyPartReadouts(medical);

        if (includeFullLedger)
        {
            state.CMUOrgans = BuildOrganReadouts(medical);
            state.CMUFractures = BuildFractureReadouts(medical);
            state.CMUInternalBleeds = BuildInternalBleedReadouts(medical);
        }
        else
        {
            state.CMUOrgans = null;
            state.CMUFractures = null;
            state.CMUInternalBleeds = null;
        }

        RefreshAdviceFromLedgerState(state);
    }

    private static void FillDamageProfile(
        HumanMedicalComponent medical,
        HealthScannerBuiState state)
    {
        var brute = FixedPoint2.Zero;
        var burn = FixedPoint2.Zero;
        var untreatedBrute = false;
        var untreatedBurn = false;

        foreach (var region in medical.Regions)
        {
            if (region.Region == BodyRegion.None)
                continue;

            brute += region.BruteDamage;
            burn += region.BurnDamage;
        }

        foreach (var injury in medical.Injuries)
        {
            if (IsClosedInjury(injury) || IsTreatedInjury(injury))
                continue;

            if (injury.Kind == InjuryKind.Burn)
                untreatedBurn = true;
            else if (injury.Kind is InjuryKind.Cut or InjuryKind.Puncture or InjuryKind.Bruise or InjuryKind.Stump)
                untreatedBrute = true;
        }

        state.Damage.Brute = brute;
        state.Damage.Burn = burn;
        state.Damage.Total = brute + burn + state.Damage.Toxin + state.Damage.Airloss + state.Damage.Genetic;
        state.Damage.UntreatedBruteWounds = untreatedBrute;
        state.Damage.UntreatedBurnWounds = untreatedBurn;
        state.CMUExternalBleeding = HasActiveExternalBleeding(medical);
    }

    private static List<CMUBodyPartReadout> BuildBodyPartReadouts(HumanMedicalComponent medical)
    {
        var readouts = new List<CMUBodyPartReadout>(10);
        AddBodyPartReadout(readouts, medical, BodyPartType.Head, BodyPartSymmetry.None, HeadRegions);
        AddBodyPartReadout(readouts, medical, BodyPartType.Torso, BodyPartSymmetry.None, TorsoRegions);
        AddBodyPartReadout(readouts, medical, BodyPartType.Arm, BodyPartSymmetry.Left, LeftArmRegions);
        AddBodyPartReadout(readouts, medical, BodyPartType.Hand, BodyPartSymmetry.Left, LeftHandRegions);
        AddBodyPartReadout(readouts, medical, BodyPartType.Arm, BodyPartSymmetry.Right, RightArmRegions);
        AddBodyPartReadout(readouts, medical, BodyPartType.Hand, BodyPartSymmetry.Right, RightHandRegions);
        AddBodyPartReadout(readouts, medical, BodyPartType.Leg, BodyPartSymmetry.Left, LeftLegRegions);
        AddBodyPartReadout(readouts, medical, BodyPartType.Foot, BodyPartSymmetry.Left, LeftFootRegions);
        AddBodyPartReadout(readouts, medical, BodyPartType.Leg, BodyPartSymmetry.Right, RightLegRegions);
        AddBodyPartReadout(readouts, medical, BodyPartType.Foot, BodyPartSymmetry.Right, RightFootRegions);
        return readouts;
    }

    private static void AddBodyPartReadout(
        List<CMUBodyPartReadout> readouts,
        HumanMedicalComponent medical,
        BodyPartType type,
        BodyPartSymmetry symmetry,
        BodyRegion[] regions)
    {
        var max = ScannerRegionMaxDamage * regions.Length;
        var damage = FixedPoint2.Zero;
        var present = false;
        var removed = false;
        var prosthetic = false;
        var broken = false;
        var splinted = false;
        var cast = false;
        var tourniquet = false;
        var eschar = false;
        var shrapnelFragments = 0;
        var shrapnelSeverity = 0f;

        for (var i = 0; i < regions.Length; i++)
        {
            if (!TryGetRegion(medical, regions[i], out var region))
                continue;

            if (IsPresentForScanner(region.Presence))
                present = true;
            else if (IsRemovedForScanner(region.Presence))
                removed = true;

            prosthetic |= region.Presence == LimbPresence.Prosthetic;

            damage += region.BruteDamage + region.BurnDamage;
            broken |= region.Skeletal.Broken;
            splinted |= region.Skeletal.Splinted;
            cast |= region.Skeletal.Knitting;
            tourniquet |= region.Tourniquet.Applied;
        }

        AccumulateForeignObjects(medical, regions, out shrapnelFragments, out shrapnelSeverity);

        if (!present && !removed)
            return;

        var current = FixedPoint2.Max(FixedPoint2.Zero, max - damage);
        var woundSize = TryGetWorstVisibleInjury(medical, regions, out var wound)
            ? MapWoundSize(wound.Stage)
            : (WoundSize?) null;
        var mechanism = woundSize is not null
            ? MapWoundMechanism(wound.Kind)
            : (WoundMechanism?) null;

        foreach (var injury in medical.Injuries)
        {
            if (injury.Kind == InjuryKind.Burn &&
                injury.Flags.HasFlag(InjuryFlags.Necrotic) &&
                ContainsRegion(regions, injury.Region))
            {
                eschar = true;
                break;
            }
        }

        if (!removed &&
            damage <= FixedPoint2.Zero &&
            woundSize is null &&
            !broken &&
            !splinted &&
            !cast &&
            !tourniquet &&
            !eschar &&
            !prosthetic &&
            shrapnelFragments <= 0 &&
            !HasRelevantBleedSource(medical, regions))
        {
            return;
        }

        readouts.Add(new CMUBodyPartReadout(
            type,
            symmetry,
            current,
            max,
            woundSize,
            mechanism,
            ShrapnelFragments: shrapnelFragments,
            ShrapnelSeverity: shrapnelSeverity,
            Eschar: eschar,
            Splinted: splinted,
            Cast: cast,
            Tourniquet: tourniquet,
            Removed: removed && !present,
            Prosthetic: prosthetic && present));
    }

    private static bool IsPresentForScanner(LimbPresence presence)
    {
        return presence is LimbPresence.Present or LimbPresence.Prosthetic;
    }

    private static bool IsRemovedForScanner(LimbPresence presence)
    {
        return presence is LimbPresence.Missing or LimbPresence.Detached;
    }

    private static List<CMUFractureReadout> BuildFractureReadouts(HumanMedicalComponent medical)
    {
        var readouts = new List<CMUFractureReadout>(10);
        AddFractureReadout(readouts, medical, BodyPartType.Head, BodyPartSymmetry.None, HeadRegions);
        AddFractureReadout(readouts, medical, BodyPartType.Torso, BodyPartSymmetry.None, TorsoRegions);
        AddFractureReadout(readouts, medical, BodyPartType.Arm, BodyPartSymmetry.Left, LeftArmRegions);
        AddFractureReadout(readouts, medical, BodyPartType.Hand, BodyPartSymmetry.Left, LeftHandRegions);
        AddFractureReadout(readouts, medical, BodyPartType.Arm, BodyPartSymmetry.Right, RightArmRegions);
        AddFractureReadout(readouts, medical, BodyPartType.Hand, BodyPartSymmetry.Right, RightHandRegions);
        AddFractureReadout(readouts, medical, BodyPartType.Leg, BodyPartSymmetry.Left, LeftLegRegions);
        AddFractureReadout(readouts, medical, BodyPartType.Foot, BodyPartSymmetry.Left, LeftFootRegions);
        AddFractureReadout(readouts, medical, BodyPartType.Leg, BodyPartSymmetry.Right, RightLegRegions);
        AddFractureReadout(readouts, medical, BodyPartType.Foot, BodyPartSymmetry.Right, RightFootRegions);
        return readouts;
    }

    private static void AddFractureReadout(
        List<CMUFractureReadout> readouts,
        HumanMedicalComponent medical,
        BodyPartType type,
        BodyPartSymmetry symmetry,
        BodyRegion[] regions)
    {
        var broken = false;
        var suppressed = true;
        var severity = FractureSeverity.None;

        for (var i = 0; i < regions.Length; i++)
        {
            if (!TryGetRegion(medical, regions[i], out var region) || !region.Skeletal.Broken)
                continue;

            broken = true;
            suppressed &= region.Skeletal.Splinted;
            if (region.Skeletal.Severity > severity)
                severity = region.Skeletal.Severity;
        }

        if (!broken)
            return;

        readouts.Add(new CMUFractureReadout(
            type,
            symmetry,
            severity == FractureSeverity.None ? FractureSeverity.Simple : severity,
            ExactSeverity: true,
            Suppressed: suppressed));
    }

    private static List<CMUInternalBleedReadout> BuildInternalBleedReadouts(HumanMedicalComponent medical)
    {
        var readouts = new List<CMUInternalBleedReadout>(10);
        AddInternalBleedReadout(readouts, medical, BodyPartType.Head, BodyPartSymmetry.None, HeadRegions);
        AddInternalBleedReadout(readouts, medical, BodyPartType.Torso, BodyPartSymmetry.None, TorsoRegions);
        AddInternalBleedReadout(readouts, medical, BodyPartType.Arm, BodyPartSymmetry.Left, LeftArmRegions);
        AddInternalBleedReadout(readouts, medical, BodyPartType.Hand, BodyPartSymmetry.Left, LeftHandRegions);
        AddInternalBleedReadout(readouts, medical, BodyPartType.Arm, BodyPartSymmetry.Right, RightArmRegions);
        AddInternalBleedReadout(readouts, medical, BodyPartType.Hand, BodyPartSymmetry.Right, RightHandRegions);
        AddInternalBleedReadout(readouts, medical, BodyPartType.Leg, BodyPartSymmetry.Left, LeftLegRegions);
        AddInternalBleedReadout(readouts, medical, BodyPartType.Foot, BodyPartSymmetry.Left, LeftFootRegions);
        AddInternalBleedReadout(readouts, medical, BodyPartType.Leg, BodyPartSymmetry.Right, RightLegRegions);
        AddInternalBleedReadout(readouts, medical, BodyPartType.Foot, BodyPartSymmetry.Right, RightFootRegions);
        return readouts;
    }

    private static void AddInternalBleedReadout(
        List<CMUInternalBleedReadout> readouts,
        HumanMedicalComponent medical,
        BodyPartType type,
        BodyPartSymmetry symmetry,
        BodyRegion[] regions)
    {
        var rate = 0f;

        foreach (var bleed in medical.BleedSources)
        {
            if (bleed.Kind != BleedKind.Internal ||
                !ContainsRegion(regions, bleed.Region) ||
                (!bleed.Active && !IsBleedSuppressedButNeedsSurgery(bleed)))
            {
                continue;
            }

            rate += bleed.Rate.Float();
        }

        if (rate <= 0f)
            return;

        readouts.Add(new CMUInternalBleedReadout(
            type,
            symmetry,
            ExactLocationKnown: true,
            rate));
    }

    private static List<CMUOrganReadout> BuildOrganReadouts(HumanMedicalComponent medical)
    {
        var organs = new List<CMUOrganReadout>(8);
        AddOrganReadout(organs, medical, "brain", BrainOrgans);
        AddOrganReadout(organs, medical, "heart", HeartOrgans);
        AddOrganReadout(organs, medical, "lungs", LungOrgans);
        AddOrganReadout(organs, medical, "liver", LiverOrgans);
        AddOrganReadout(organs, medical, "kidneys", KidneyOrgans);
        AddOrganReadout(organs, medical, "stomach", StomachOrgans);
        AddOrganReadout(organs, medical, "eyes", EyeOrgans);
        AddOrganReadout(organs, medical, "ears", EarOrgans);
        return organs;
    }

    private static void AddOrganReadout(
        List<CMUOrganReadout> organs,
        HumanMedicalComponent medical,
        string organName,
        OrganSlot[] slots)
    {
        var count = 0;
        var missing = 0;
        var damage = FixedPoint2.Zero;
        var worst = OrganDamageStage.Healthy;

        for (var i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            var index = (int) slot;
            if (index <= 0 || index >= medical.Organs.Length)
                continue;

            var organ = medical.Organs[index];
            if (organ.Slot == OrganSlot.None)
                continue;

            count++;
            if (organ.Missing)
            {
                missing++;
                worst = OrganDamageStage.Dead;
                damage += ScannerRegionMaxDamage;
                continue;
            }

            damage += organ.Damage;
            var stage = MapOrganStage(organ.Status);
            if (stage > worst)
                worst = stage;
        }

        if (count == 0 ||
            missing == 0 &&
            damage <= FixedPoint2.Zero &&
            worst == OrganDamageStage.Healthy)
        {
            return;
        }

        var max = ScannerRegionMaxDamage * count;
        var current = FixedPoint2.Max(FixedPoint2.Zero, max - damage);
        organs.Add(new CMUOrganReadout(
            organName,
            worst,
            current,
            max,
            Removed: missing == count));
    }

    private static void RefreshAdviceFromLedgerState(HealthScannerBuiState state)
    {
        var advice = state.Advice;
        var damage = state.Damage;
        var isDead = state.MobState == MobState.Dead;
        var isCritical = state.MobState == MobState.Critical;
        var chemicals = state.Chemicals;

        advice.NeedsEpinephrine = false;
        advice.ShowRepeatedDefibWarning = false;
        advice.ShowDefib = false;
        advice.ShowCpr = false;
        advice.ShowBruteWounds = damage.UntreatedBruteWounds;
        advice.ShowBurnWounds = damage.UntreatedBurnWounds;
        advice.ShowBloodPack = false;
        advice.ShowFood = false;
        advice.ShowCprCrit = false;
        advice.ShowDexalin = false;
        advice.ShowBicaridine = false;
        advice.ShowKelotane = false;
        advice.ShowDylovene = false;

        if (isDead)
        {
            if (state.HasDeadThreshold)
            {
                if (state.DeadThreshold + 30 < damage.Total &&
                    chemicals != null &&
                    !chemicals.ContainsReagent("CMEpinephrine", null))
                {
                    advice.NeedsEpinephrine = true;
                }
                else
                {
                    advice.ShowRepeatedDefibWarning = state.DeadThreshold - 20 <= damage.Total &&
                        !damage.UntreatedBruteWounds &&
                        !damage.UntreatedBurnWounds;
                    advice.ShowDefib = !advice.ShowRepeatedDefibWarning && state.DeadThreshold > damage.Total;
                }
            }

            advice.ShowCpr = true;
        }

        if (state.MaxBlood > FixedPoint2.Zero && state.Blood < state.MaxBlood)
        {
            var bloodPercent = state.Blood / state.MaxBlood;
            advice.ShowBloodPack = bloodPercent < FixedPoint2.New(0.85f);
            advice.ShowFood = bloodPercent < FixedPoint2.New(0.9f) &&
                chemicals != null &&
                !chemicals.ContainsReagent("Nutriment", null);
        }

        if (damage.Airloss > FixedPoint2.Zero && !isDead)
        {
            advice.ShowCprCrit = damage.Airloss > FixedPoint2.New(10) && isCritical;
            advice.ShowDexalin = damage.Airloss > FixedPoint2.New(30) &&
                chemicals != null &&
                !chemicals.ContainsReagent("CMDexalin", null);
        }

        advice.ShowBicaridine = damage.Brute > FixedPoint2.New(30) &&
            chemicals != null &&
            !chemicals.ContainsReagent("CMBicaridine", null) &&
            !isDead;
        advice.ShowKelotane = damage.Burn > FixedPoint2.New(30) &&
            chemicals != null &&
            !chemicals.ContainsReagent("CMKelotane", null) &&
            !isDead;
        advice.ShowDylovene = damage.Toxin > FixedPoint2.New(10) &&
            chemicals != null &&
            !chemicals.ContainsReagent("CMDylovene", null) &&
            !chemicals.ContainsReagent("Inaprovaline", null) &&
            !isDead;
    }

    private static bool TryGetWorstVisibleInjury(
        HumanMedicalComponent medical,
        BodyRegion[] regions,
        out InjuryRecord wound)
    {
        wound = default;
        var found = false;

        foreach (var injury in medical.Injuries)
        {
            if (IsClosedInjury(injury) ||
                injury.Kind == InjuryKind.InternalBleed ||
                !ContainsRegion(regions, injury.Region))
            {
                continue;
            }

            if (!found || injury.Stage > wound.Stage)
                wound = injury;
            found = true;
        }

        return found;
    }

    private static bool TryGetRegion(
        HumanMedicalComponent medical,
        BodyRegion region,
        out RegionState state)
    {
        var index = (int) region;
        if (index > 0 && index < medical.Regions.Length)
        {
            state = medical.Regions[index];
            return state.Region == region;
        }

        state = default;
        return false;
    }

    private static bool ContainsRegion(BodyRegion[] regions, BodyRegion region)
    {
        for (var i = 0; i < regions.Length; i++)
        {
            if (regions[i] == region)
                return true;
        }

        return false;
    }

    private static void AccumulateForeignObjects(
        HumanMedicalComponent medical,
        BodyRegion[] regions,
        out int fragments,
        out float severity)
    {
        fragments = 0;
        severity = 0f;
        foreach (var foreignObject in medical.ForeignObjects)
        {
            if (foreignObject.Kind != ForeignObjectKind.Shrapnel ||
                foreignObject.Fragments <= 0 ||
                !ContainsRegion(regions, foreignObject.Region))
            {
                continue;
            }

            fragments += foreignObject.Fragments;
            severity = MathF.Max(severity, foreignObject.Severity);
        }
    }

    private static bool HasActiveExternalBleeding(HumanMedicalComponent medical)
    {
        foreach (var bleed in medical.BleedSources)
        {
            if (bleed.Active && bleed.Kind != BleedKind.Internal)
                return true;
        }

        return false;
    }

    private static bool HasRelevantBleedSource(HumanMedicalComponent medical, BodyRegion[] regions)
    {
        foreach (var bleed in medical.BleedSources)
        {
            if (!ContainsRegion(regions, bleed.Region))
                continue;

            if (bleed.Active || IsBleedSuppressedButNeedsSurgery(bleed))
                return true;
        }

        return false;
    }

    private static WoundSize MapWoundSize(InjuryStage stage)
    {
        return stage switch
        {
            InjuryStage.Tiny or InjuryStage.Small or InjuryStage.Moderate or InjuryStage.Large => WoundSize.Small,
            InjuryStage.Deep or InjuryStage.Flesh => WoundSize.Deep,
            InjuryStage.Gaping or InjuryStage.GapingBig or InjuryStage.Severe or InjuryStage.Carbonised => WoundSize.Gaping,
            InjuryStage.Massive or InjuryStage.Huge or InjuryStage.Monumental or InjuryStage.Stump => WoundSize.Massive,
            _ => WoundSize.Deep,
        };
    }

    private static WoundMechanism MapWoundMechanism(InjuryKind kind)
    {
        return kind switch
        {
            InjuryKind.Bruise => WoundMechanism.Crush,
            InjuryKind.Burn => WoundMechanism.Burn,
            InjuryKind.SurgicalIncision => WoundMechanism.Surgical,
            InjuryKind.Stump => WoundMechanism.Slash,
            InjuryKind.Puncture => WoundMechanism.Bullet,
            InjuryKind.Cut => WoundMechanism.Slash,
            _ => WoundMechanism.Generic,
        };
    }

    private static OrganDamageStage MapOrganStage(OrganDamageStatus status)
    {
        return status switch
        {
            OrganDamageStatus.LittleBruised => OrganDamageStage.Bruised,
            OrganDamageStatus.Bruised => OrganDamageStage.Damaged,
            OrganDamageStatus.Broken => OrganDamageStage.Failing,
            _ => OrganDamageStage.Healthy,
        };
    }

    private static bool IsClosedInjury(InjuryRecord injury)
    {
        return injury.Flags.HasFlag(InjuryFlags.Closed) ||
            injury.Flags.HasFlag(InjuryFlags.Sutured);
    }

    private static bool IsTreatedInjury(InjuryRecord injury)
    {
        return injury.Flags.HasFlag(InjuryFlags.Bandaged) ||
            injury.Flags.HasFlag(InjuryFlags.Salved) ||
            injury.Flags.HasFlag(InjuryFlags.Clamped) ||
            injury.Flags.HasFlag(InjuryFlags.Sutured) ||
            injury.Flags.HasFlag(InjuryFlags.Closed);
    }

    public static void AppendBodyScannerLines(
        HumanMedicalComponent medical,
        List<CMUBodyScannerScanLine> lines,
        HumanMedicalScannerLocalizer? localizer = null)
    {
        localizer ??= global::Robust.Shared.Localization.Loc.GetString;

        var detail = BuildFullLedgerDetail(medical);
        var summary = detail.Summary;

        lines.Add(new CMUBodyScannerScanLine(
            CMUBodyScannerScanCategory.Vitals,
            localizer(
                "cmu-body-scanner-line-human-summary",
                ("status", localizer(StatusLocKey(summary.HudStatus))),
                ("revision", summary.Revision),
                ("alerts", summary.Alerts))));

        AppendRegionLines(detail, lines, localizer);
        AppendOrganLines(detail, lines, localizer);
    }

    private static void AppendRegionLines(
        HumanMedicalLedgerDetail detail,
        List<CMUBodyScannerScanLine> lines,
        HumanMedicalScannerLocalizer localizer)
    {
        foreach (var region in detail.Regions)
        {
            if (region.Region == BodyRegion.None)
                continue;

            var details = new List<string>();
            if (region.Presence != LimbPresence.Present)
            {
                details.Add(localizer(
                    "cmu-body-scanner-human-region-presence",
                    ("presence", region.Presence)));
            }

            if (region.BruteDamage > FixedPoint2.Zero || region.BurnDamage > FixedPoint2.Zero)
            {
                details.Add(localizer(
                    "cmu-body-scanner-human-region-damage",
                    ("brute", region.BruteDamage),
                    ("burn", region.BurnDamage)));
            }

            if (region.Skeletal.Broken)
            {
                details.Add(region.Skeletal.Splinted
                    ? localizer("cmu-body-scanner-human-region-fracture-splinted")
                    : localizer("cmu-body-scanner-human-region-fracture"));
            }

            if (region.Incision != IncisionDepth.Closed)
            {
                details.Add(localizer(
                    "cmu-body-scanner-human-region-incision",
                    ("depth", region.Incision)));
            }

            if (region.Tourniquet.Applied)
            {
                details.Add(region.Tourniquet.Necrotic
                    ? localizer("cmu-body-scanner-human-region-tourniquet-necrotic")
                    : localizer("cmu-body-scanner-human-region-tourniquet"));
            }

            foreach (var bleed in detail.BleedSources)
            {
                if (bleed.Region != region.Region)
                    continue;

                if (bleed.Active)
                {
                    details.Add(localizer(
                        "cmu-body-scanner-human-region-bleed",
                        ("kind", bleed.Kind),
                        ("rate", bleed.Rate)));
                    continue;
                }

                if (IsBleedSuppressedButNeedsSurgery(bleed))
                    details.Add(localizer("cmu-body-scanner-human-region-bleed-suppressed-needs-surgery"));
            }

            foreach (var foreignObject in detail.ForeignObjects)
            {
                if (foreignObject.Region != region.Region ||
                    foreignObject.Kind != ForeignObjectKind.Shrapnel ||
                    foreignObject.Fragments <= 0)
                {
                    continue;
                }

                details.Add(localizer(
                    "cmu-body-scanner-human-region-shrapnel",
                    ("count", foreignObject.Fragments),
                    ("depth", foreignObject.Depth)));
            }

            if (details.Count == 0)
                continue;

            lines.Add(new CMUBodyScannerScanLine(
                CMUBodyScannerScanCategory.Body,
                localizer(
                    "cmu-body-scanner-line-human-region",
                    ("region", localizer(GetRegionLocKey(region.Region))),
                    ("details", string.Join(", ", details)))));
        }
    }

    private static void AppendOrganLines(
        HumanMedicalLedgerDetail detail,
        List<CMUBodyScannerScanLine> lines,
        HumanMedicalScannerLocalizer localizer)
    {
        foreach (var organ in detail.Organs)
        {
            if (organ.Slot == OrganSlot.None)
                continue;
            if (organ.Status == OrganDamageStatus.None && !organ.Missing)
                continue;

            lines.Add(new CMUBodyScannerScanLine(
                CMUBodyScannerScanCategory.Organs,
                localizer(
                    "cmu-body-scanner-line-human-organ",
                    ("organ", localizer(GetOrganLocKey(organ.Slot))),
                    ("status", organ.Missing
                        ? localizer("cmu-body-scanner-human-organ-missing")
                        : organ.Status),
                    ("damage", organ.Damage))));
        }
    }

    private static string StatusLocKey(HudStatus status)
    {
        return status switch
        {
            HudStatus.Healthy => "cmu-human-medical-status-healthy",
            HudStatus.Stable => "cmu-human-medical-status-stable",
            HudStatus.Wounded => "cmu-human-medical-status-wounded",
            HudStatus.Serious => "cmu-human-medical-status-serious",
            HudStatus.Critical => "cmu-human-medical-status-critical",
            _ => "cmu-human-medical-status-unknown",
        };
    }

    private static bool IsBleedSuppressedButNeedsSurgery(BleedSource bleed)
    {
        if (bleed.Treatment.HasFlag(TreatmentFlags.Closed) ||
            bleed.Treatment.HasFlag(TreatmentFlags.Sutured))
        {
            return false;
        }

        return bleed.Treatment.HasFlag(TreatmentFlags.Clamped) ||
            bleed.Treatment.HasFlag(TreatmentFlags.TemporarilySuppressed) ||
            bleed.Treatment.HasFlag(TreatmentFlags.Tourniquetted);
    }

    public static string GetRegionLocKey(BodyRegion region)
    {
        return region switch
        {
            BodyRegion.Head => "cmu-human-medical-region-head",
            BodyRegion.Chest => "cmu-human-medical-region-chest",
            BodyRegion.Groin => "cmu-human-medical-region-groin",
            BodyRegion.LeftArm => "cmu-human-medical-region-left-arm",
            BodyRegion.RightArm => "cmu-human-medical-region-right-arm",
            BodyRegion.LeftHand => "cmu-human-medical-region-left-hand",
            BodyRegion.RightHand => "cmu-human-medical-region-right-hand",
            BodyRegion.LeftLeg => "cmu-human-medical-region-left-leg",
            BodyRegion.RightLeg => "cmu-human-medical-region-right-leg",
            BodyRegion.LeftFoot => "cmu-human-medical-region-left-foot",
            BodyRegion.RightFoot => "cmu-human-medical-region-right-foot",
            _ => "cmu-human-medical-region-unknown",
        };
    }

    public static string GetOrganLocKey(OrganSlot slot)
    {
        return slot switch
        {
            OrganSlot.Brain => "cmu-human-medical-organ-brain",
            OrganSlot.Heart => "cmu-human-medical-organ-heart",
            OrganSlot.LeftLung => "cmu-human-medical-organ-left-lung",
            OrganSlot.RightLung => "cmu-human-medical-organ-right-lung",
            OrganSlot.Liver => "cmu-human-medical-organ-liver",
            OrganSlot.Kidneys => "cmu-human-medical-organ-kidneys",
            OrganSlot.Stomach => "cmu-human-medical-organ-stomach",
            OrganSlot.Eyes => "cmu-human-medical-organ-eyes",
            OrganSlot.Ears => "cmu-human-medical-organ-ears",
            _ => "cmu-human-medical-organ-unknown",
        };
    }
}
