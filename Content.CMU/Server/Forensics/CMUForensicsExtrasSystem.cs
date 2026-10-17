using System.Linq;
using Content.Shared.CMU14.CriminalRecords;
using Content.Shared.CMU14.Hearing;
using Content.Shared.Forensics;
using Content.Shared.Forensics.Components;
using Content.Shared.Forensics.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.StationRecords;
using Content.Shared.StationRecords.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Forensics;

/// <summary>
/// Extra forensic evidence: time since death, gunshot residue, contact samples on clothing and record matching.
/// </summary>
public sealed partial class CMUForensicsExtrasSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private ForensicsSystem _forensics = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StationRecordsSystem _records = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private CMUUniversalRecordsSystem _universalRecords = default!;

    private static readonly TimeSpan ResidueDuration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Records that were on the computer before the current scan. Scanning a person files them, and they shouldn't
    /// then "match" themselves.
    /// </summary>
    private HashSet<uint>? _preScanRecords;

    public override void Initialize()
    {
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<CMUGunFiredEvent>(OnGunFired);
        SubscribeLocalEvent<ForensicScannerScannedEvent>(OnScanned);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
        {
            var death = EnsureComp<CMUTimeOfDeathComponent>(args.Target);
            death.DiedAt = _timing.CurTime;
            death.Cause = DetermineCauseOfDeath(args.Target);
        }
        else if (args.OldMobState == MobState.Dead)
            RemComp<CMUTimeOfDeathComponent>(args.Target);
    }

    private void OnGunFired(ref CMUGunFiredEvent args)
    {
        if (!HasComp<HumanoidProfileComponent>(args.User))
            return;

        // Gloves catch the residue instead of the hands
        var holder = _inventory.TryGetSlotEntity(args.User, "gloves", out var gloves) ? gloves.Value : args.User;
        EnsureComp<CMUGunshotResidueComponent>(holder).ExpiresAt = _timing.CurTime + ResidueDuration;
    }

    private void OnScanned(ref ForensicScannerScannedEvent args)
    {
        if (!TryComp(args.Scanner, out ForensicScannerComponent? scanner))
            return;

        var target = args.Target;
        var isPerson = HasComp<HumanoidProfileComponent>(target);

        // Hair and skin left on their clothes by anyone they've been in contact with
        if (isPerson)
        {
            foreach (var dna in _forensics.CMUGetWornSampleDna(target))
            {
                if (!scanner.DNAs.Contains(dna))
                    scanner.DNAs.Add(dna);
            }
        }

        if (HasResidue(target) ||
            isPerson && _inventory.TryGetSlotEntity(target, "gloves", out var gloves) && HasResidue(gloves.Value))
        {
            var residue = Loc.GetString("cmu-forensics-gunshot-residue");
            if (!scanner.Residues.Contains(residue))
                scanner.Residues.Add(residue);
        }

        scanner.CMUTimeSinceDeath = GetTimeSinceDeath(target);
        scanner.CMUCauseOfDeath = GetCauseOfDeath(target);
        scanner.CMUInjuries = isPerson ? BuildInjuryReport(target) : new();
        scanner.CMURecordMatches = FindRecordMatches(scanner);
        _preScanRecords = null;
        Dirty(args.Scanner, scanner);

        if (scanner.Fingerprints.Count == 0 && scanner.DNAs.Count == 0)
            return;

        if (!_container.TryGetContainingContainer((args.Scanner, null), out var container))
            return;

        var user = container.Owner;
        if (scanner.CMURecordMatches.Count > 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-forensics-match",
                ("names", string.Join(", ", scanner.CMURecordMatches))), user, user, PopupType.Medium);
            _audio.PlayPvs(scanner.SoundMatch, args.Scanner);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("cmu-forensics-no-match"), user, user);
        }
    }

    /// <summary>
    /// Remembers which records exist before a scan adds its own. Called before the scanner files anyone.
    /// </summary>
    public void SnapshotRecordsBeforeScan()
    {
        _preScanRecords = new HashSet<uint>();
        if (_universalRecords.GetRecords() is not { } station)
            return;

        foreach (var (id, _) in _records.GetRecordsOfType<GeneralStationRecord>(station))
        {
            _preScanRecords.Add(id);
        }
    }

    private bool HasResidue(EntityUid uid)
    {
        return TryComp(uid, out CMUGunshotResidueComponent? residue) && residue.ExpiresAt > _timing.CurTime;
    }

    private string GetTimeSinceDeath(EntityUid target)
    {
        if (!_mobState.IsDead(target) || !TryComp(target, out CMUTimeOfDeathComponent? death))
            return string.Empty;

        var minutes = (int) (_timing.CurTime - death.DiedAt).TotalMinutes;
        if (minutes < 1)
            return Loc.GetString("cmu-forensics-death-recent");

        // Rough, like a real estimate: exact under ten minutes, then to the nearest five
        if (minutes >= 10)
            minutes = (int) Math.Round(minutes / 5.0) * 5;

        return Loc.GetString("cmu-forensics-death-minutes", ("minutes", minutes));
    }

    private List<string> FindRecordMatches(ForensicScannerComponent scanner)
    {
        var matches = new List<string>();
        if (_universalRecords.GetRecords() is not { } station)
            return matches;

        foreach (var (id, record) in _records.GetRecordsOfType<GeneralStationRecord>(station))
        {
            if (_preScanRecords != null && !_preScanRecords.Contains(id))
                continue;

            var prints = record.Fingerprint != null && scanner.Fingerprints.Contains(record.Fingerprint);
            var dna = record.DNA != null && scanner.DNAs.Contains(record.DNA);
            if ((prints || dna) && !matches.Contains(record.Name))
                matches.Add(record.Name);
        }

        return matches.OrderBy(n => n).ToList();
    }
}

[RegisterComponent]
public sealed partial class CMUTimeOfDeathComponent : Component
{
    [DataField]
    public TimeSpan DiedAt;

    /// <summary>
    /// Localised cause of death, worked out at the moment of death before anything can be healed or moved.
    /// </summary>
    [DataField]
    public string Cause = string.Empty;
}

[RegisterComponent]
public sealed partial class CMUGunshotResidueComponent : Component
{
    [DataField]
    public TimeSpan ExpiresAt;
}
