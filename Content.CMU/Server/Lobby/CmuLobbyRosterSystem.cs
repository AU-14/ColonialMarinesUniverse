using System.Linq;
using Content.Server.CrewManifest;
using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Lobby;
using Content.Shared.CMU14.CriminalRecords;
using Content.Shared.CrewManifest;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Roles;
using Content.Shared.Station.Components;
using Content.Shared.StationRecords;
using Content.Shared.StationRecords.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Lobby;

public sealed partial class CmuLobbyRosterSystem : EntitySystem
{
    [Dependency] private CrewManifestSystem _manifest = default!;
    [Dependency] private StationRecordsSystem _records = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private GameTicker _ticker = default!;
    private readonly Dictionary<(NetUserId, NetEntity), TimeSpan> _nextRequest = new();
    private readonly Dictionary<StationRecordKey, Dictionary<string, EntProtoId>> _outfits = new();

    public override void Initialize()
    {
        SubscribeNetworkEvent<CmuLobbyRosterRequest>(OnRequest);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawn, after: [typeof(StationRecordsSystem)]);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _nextRequest.Clear();
            _outfits.Clear();
        });
    }

    private void OnSpawn(PlayerSpawnCompleteEvent args)
    {
        // Colony criminal records use a separate holder. The public station record needs its own arrival portrait.
        if (_records.GetRecordByName(args.Station, args.Profile.Name) is not { } id)
            return;
        var key = new StationRecordKey(id, args.Station);
        if (!_records.TryGetRecord<GeneralStationRecord>(key, out var record))
            return;
        record.CMUPortrait = new CMURecordPortrait
        {
            Species = args.Profile.Species,
            Sex = args.Profile.Sex,
            Appearance = args.Profile.Appearance,
        };
        // Job and platoon gear is equipped before PlayerSpawnComplete. Freeze that public
        // appearance so opening the roster never reveals somebody's current field equipment.
        var outfit = new Dictionary<string, EntProtoId>();
        var slots = _inventory.GetSlotEnumerator(args.Mob,
            SlotFlags.HEAD | SlotFlags.EYES | SlotFlags.EARS | SlotFlags.MASK |
            SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING | SlotFlags.NECK |
            SlotFlags.BACK | SlotFlags.BELT | SlotFlags.GLOVES | SlotFlags.FEET);
        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is { } item && MetaData(item).EntityPrototype is { } prototype)
                outfit[slot.ID] = prototype.ID;
        }
        _outfits[key] = outfit;
        _records.Synchronize(key);
    }

    private void OnRequest(CmuLobbyRosterRequest request, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        var station = GetEntity(request.Station);
        if (!HasComp<StationDataComponent>(station)) return;
        var key = (session.UserId, request.Station);
        if (_timing.RealTime < _nextRequest.GetValueOrDefault(key)) return;
        _nextRequest[key] = _timing.RealTime + TimeSpan.FromSeconds(0.5);
        var entries = new List<CmuLobbyRosterEntry>();
        // Match the existing public manifest permission and its server-side hidden-faction filtering.
        if (_configuration.GetCVar(CCVars.CrewManifestWithoutEntity))
        {
            var (_, manifest) = _manifest.GetCrewManifest(station);
            if (manifest != null)
            {
                var portraits = _records.GetRecordsOfType<GeneralStationRecord>(station)
                    .GroupBy(record => (record.Item2.Name, record.Item2.JobPrototype))
                    .ToDictionary(group => group.Key, group => group.First());
                foreach (var entry in manifest.Entries)
                {
                    var (id, record) = portraits.GetValueOrDefault((entry.Name, entry.JobPrototype));
                    entries.Add(new CmuLobbyRosterEntry(entry, record?.CMUPortrait,
                        _outfits.GetValueOrDefault(new StationRecordKey(id, station))));
                }
            }
            AddAnonymousInsurgents(station, entries);
        }
        RaiseNetworkEvent(new CmuLobbyRosterResponse(request.Station, entries), session.Channel);
    }

    private void AddAnonymousInsurgents(EntityUid station, List<CmuLobbyRosterEntry> entries)
    {
        if (!string.Equals((_ticker.CurrentPreset ?? _ticker.Preset)?.ID, "Insurgency", StringComparison.OrdinalIgnoreCase))
            return;

        // CLF stays excluded from the public manifest. The join roster exposes only occupied
        // job slots, never the hidden record's identity or an image that could identify them.
        foreach (var (_, record) in _records.GetRecordsOfType<GeneralStationRecord>(station))
        {
            if (!ProtoMan.TryIndex(record.JobPrototype, out JobPrototype? job) ||
                !string.Equals(job.RoundForce, "CLF", StringComparison.OrdinalIgnoreCase))
                continue;

            var anonymous = new CrewManifestEntry(string.Empty, job.LocalizedName, job.Icon.Id, job.ID);
            entries.Add(new CmuLobbyRosterEntry(anonymous, null, identityHidden: true));
        }
    }
}
