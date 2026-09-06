using Content.Server.AU14.Systems;
using Content.Server.GameTicking.Rules;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
using Content.Server._CMU14.Round.Antags.ColonyBounty;
using Content.Shared.Paper;
using Content.Shared.StationRecords;
using Robust.Shared.Maths;
using Robust.Shared.Random;

namespace Content.Server._CMU14.Round.Antags.RunawaySynth;

public sealed partial class RunawaySynthRuleSystem : GameRuleSystem<RunawaySynthRuleComponent>
{
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly StationRecordsSystem _stationRecords = default!;
    [Dependency] private readonly WantedSystem _wantedSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RunawaySynthComponent, ComponentStartup>(OnSynthSpawned);
    }

    private void OnSynthSpawned(EntityUid uid, RunawaySynthComponent component, ComponentStartup args)
    {
        var synthName = EntityManager.GetComponentOrNull<MetaDataComponent>(uid)?.EntityName ?? "Unknown";

        var station = _stationSystem.GetOwningStation(uid);
        var nameList = new List<string> { synthName };

        if (station != null)
        {
            var allNames = new List<string>();
            foreach (var (_, record) in _stationRecords.GetRecordsOfType<GeneralStationRecord>(station.Value))
            {
                if (record.Name != synthName
                    && !record.Name.Contains("(Unknown)")
                    && !record.Name.Contains("Fugitive")
                    && !record.Name.Contains("Runaway"))
                    allNames.Add(record.Name);
            }

            _random.Shuffle(allNames);
            var count = Math.Min(4, allNames.Count);
            for (var i = 0; i < count; i++)
                nameList.Add(allNames[i]);
        }

        _random.Shuffle(nameList);

        var listText = "";
        for (var i = 0; i < nameList.Count; i++)
            listText += $"  {i + 1}. {nameList[i]}\n";

        _wantedSystem.SendFaxToGroup(
            ColonyCmbFax.MarshalBureauFaxGroup,
            "Fugitive Alert",
            ColonyCmbFax.Build("Fugitive Alert",
                "A runaway Synthetic has been detected at your colony. One of the following colonists is the synth. " +
                "Liquidate it and the $2500 bounty is yours.",
                $"[bold]Suspect List:[/bold]\n{listText}\n"),
            "paper_stamp-cmb",
            new List<StampDisplayInfo>
            {
                new() { StampedColor = Color.FromHex("#b0901b"), StampedName = "CMB" },
            },
            ColonyCmbFax.CmbPaperPrototype,
            "Colony Administrator");
    }
}
