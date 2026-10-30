using Content.Server.Decals;
using Content.Shared.CMU14.Forensics;
using Content.Shared.Decals;
using Content.Shared.DoAfter;
using Content.Shared.Forensics;
using Content.Shared.Forensics.Components;
using Content.Shared.Forensics.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Forensics;

/// <summary>
///     Lets a forensic scanner read blood that's drawn as decals: drips, arterial spray and decal-drawn blood puddles.
///     Decals aren't entities, so whose blood each one is gets remembered as it's placed, and using the scanner on the
///     floor scans every stain within reach of where it was pointed. Blood that was mapped in, or can't be traced to
///     anyone, shows up as old, degraded blood.
/// </summary>
public sealed partial class CMUBloodEvidenceSystem : EntitySystem
{
    private const float ScanRadius = 0.75f;

    private static readonly ProtoId<TagPrototype> DnaSolutionScannable = "DNASolutionScannable";

    [Dependency] private DecalSystem _decals = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private CMUForensicsExtrasSystem _extras = default!;
    [Dependency] private ForensicsSystem _forensics = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    // Whose blood each blood decal placed this round is.
    private readonly Dictionary<(EntityUid Grid, DecalIndex Decal), string> _blood = new();

    // Where each scanner was pointed, while its floor scan runs.
    private readonly Dictionary<EntityUid, EntityCoordinates> _pending = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _blood.Clear();
            _pending.Clear();
        });
        SubscribeLocalEvent<CMUForensicScanFloorEvent>(OnScanFloor);
        SubscribeLocalEvent<ForensicScannerComponent, CMUBloodScanDoAfterEvent>(OnBloodScanDoAfter);
    }

    /// <summary>Remembers that the blood decal just placed at <paramref name="coordinates"/> came from <paramref name="source"/>.</summary>
    public void RecordBloodDecal(EntityCoordinates coordinates, DecalIndex decal, EntityUid source)
    {
        if (_transform.GetGrid(coordinates) is not { } grid ||
            !TryComp(source, out DnaComponent? dna) ||
            dna.DNA is not { } sample)
        {
            return;
        }

        _blood[(grid, decal)] = sample;
    }

    private void OnScanFloor(ref CMUForensicScanFloorEvent args)
    {
        if (BloodDecals(args.Location).Count == 0 && BloodPuddles(args.Location).Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-forensics-floor-nothing"), args.User, args.User);
            return;
        }

        var scanner = args.Scanner;
        var doAfter = new DoAfterArgs(EntityManager, args.User, scanner.Comp.ScanDelay, new CMUBloodScanDoAfterEvent(),
            scanner, used: scanner)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _pending[scanner.Owner] = args.Location;
    }

    private void OnBloodScanDoAfter(Entity<ForensicScannerComponent> ent, ref CMUBloodScanDoAfterEvent args)
    {
        if (!_pending.Remove(ent.Owner, out var location) || args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var dnas = new List<string>();
        var degraded = false;
        if (_transform.GetGrid(location) is { } grid)
        {
            foreach (var (index, _) in BloodDecals(location))
            {
                if (!_blood.TryGetValue((grid, index), out var dna))
                {
                    degraded = true;
                    continue;
                }

                if (!dnas.Contains(dna))
                    dnas.Add(dna);
            }
        }

        foreach (var puddle in BloodPuddles(location))
        {
            foreach (var dna in _forensics.GetSolutionsDNA(puddle))
            {
                if (!dnas.Contains(dna))
                    dnas.Add(dna);
            }
        }

        var scanner = ent.Comp;
        scanner.Fingerprints = [];
        scanner.Fibers = [];
        scanner.DNAs = dnas;
        scanner.Residues = degraded ? [Loc.GetString("cmu-forensics-degraded-blood")] : [];
        scanner.LastScannedName = Loc.GetString("cmu-forensics-blood-stain");
        scanner.CMUTimeSinceDeath = string.Empty;
        scanner.CMUCauseOfDeath = string.Empty;
        scanner.CMUInjuries = [];
        scanner.CMURecordMatches = _extras.FindRecordMatches(scanner);
        Dirty(ent);

        _ui.OpenUi(ent.Owner, ForensicScannerUiKey.Key, args.User);
    }

    /// <summary>Human blood decals near the spot. Xeno acid blood has no DNA and is left out.</summary>
    private List<(DecalIndex Index, Decal Decal)> BloodDecals(EntityCoordinates location)
    {
        var found = new List<(DecalIndex, Decal)>();
        if (_transform.GetGrid(location) is not { } grid)
            return found;

        var local = _transform.WithEntityId(location, grid).Position;
        foreach (var (index, decal) in _decals.GetDecalsInRange(grid, local, ScanRadius))
        {
            if (decal.Id.Contains("Blood", StringComparison.OrdinalIgnoreCase) &&
                !decal.Id.Contains("Xeno", StringComparison.OrdinalIgnoreCase) &&
                !decal.Id.Contains("Acid", StringComparison.OrdinalIgnoreCase))
            {
                found.Add((index, decal));
            }
        }

        return found;
    }

    private List<EntityUid> BloodPuddles(EntityCoordinates location)
    {
        var found = new List<EntityUid>();
        foreach (var uid in _lookup.GetEntitiesInRange(location, ScanRadius))
        {
            if (_tag.HasTag(uid, DnaSolutionScannable))
                found.Add(uid);
        }

        return found;
    }
}
