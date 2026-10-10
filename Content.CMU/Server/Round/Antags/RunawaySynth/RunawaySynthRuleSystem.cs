using Content.Server.CMU14.Systems;
using Content.Server.GameTicking.Rules;
using Content.Server.Station.Systems;
using Content.Shared.CMU14.Round.Antags.ColonyBounty;
using Content.Shared.Humanoid;
using Content.Server.CMU14.Round.Antags.ColonyBounty;
using Content.Shared.Paper;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Round.Antags.RunawaySynth;

public sealed partial class RunawaySynthRuleSystem : GameRuleSystem<RunawaySynthRuleComponent>
{
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly SuspectDescriptionSystem _suspectDescription = default!;
    [Dependency] private readonly WantedSystem _wantedSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RunawaySynthComponent, ComponentStartup>(OnSynthSpawned);
    }

    private void OnSynthSpawned(EntityUid uid, RunawaySynthComponent component, ComponentStartup args)
    {
        var station = _stationSystem.GetOwningStation(uid);

        var lines = new List<string>
        {
            _suspectDescription.Describe(uid, _suspectDescription.RandomWitness(uid, station)),
        };

        if (station != null)
        {
            var pool = new List<EntityUid>();
            var enumerator = EntityManager.AllEntityQueryEnumerator<HumanoidProfileComponent>();
            while (enumerator.MoveNext(out var colonist, out _))
            {
                if (colonist == uid
                    || HasComp<ColonyBountyComponent>(colonist)
                    || _stationSystem.GetOwningStation(colonist) != station)
                    continue;

                pool.Add(colonist);
            }

            _random.Shuffle(pool);
            for (var i = 0; i < Math.Min(4, pool.Count); i++)
                lines.Add(_suspectDescription.Describe(pool[i], null));
        }

        _random.Shuffle(lines);

        var listText = "";
        for (var i = 0; i < lines.Count; i++)
            listText += $"  {i + 1}. {lines[i]}\n";

        _wantedSystem.SendFaxToGroup(
            ColonyCmbFax.MarshalBureauFaxGroup,
            "Fugitive Alert",
            ColonyCmbFax.Build("Fugitive Alert",
                "A runaway Synthetic has been detected at your colony. Witnesses described the " +
                "individuals below; one of them is the synth. Match the descriptions to colonists " +
                "by examination, and liquidate it for the $2800 bounty.",
                $"[bold]Witness descriptions:[/bold]\n{listText}\n"),
            "paper_stamp-cmb",
            new List<StampDisplayInfo>
            {
                new() { StampedColor = Color.FromHex("#b0901b"), StampedName = "CMB" },
            },
            ColonyCmbFax.CmbPaperPrototype,
            "Colony Administrator");
    }
}
