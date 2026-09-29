using Content.Server.CMU14.Chemistry.Reagents;
using Content.Server.CMU14.ColonyEconomy;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Shared.CMU14.Chemistry.Reagents;
using Content.Shared.CMU14.Chemistry.Research;
using Content.Shared.CMU14.Chemistry.Reagent;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.GameTicking;
using Content.Shared.Paper;
using Discord.Rest;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Content.Server.CMU14.Chemistry.Research;

public sealed partial class ServerResearchDataTerminalSystem : SharedResearchDataTerminalSystem
{
    public sealed class FactionResearch
    {
        public List<GeneratedReagentData> Selectable = [];
        public HashSet<string> CompletedChemicals = [];
        public TimeSpan NextReroll;
        public TimeSpan LastTime;
        public bool Picked;
        public string LastPickName = string.Empty;
        public string LastPick = string.Empty;
        public Dictionary<int, (string, string, TimeSpan, bool, GeneratedReagentData, bool, bool)> Reports = [];
    }

    private readonly Dictionary<string, FactionResearch> _factions = new(StringComparer.OrdinalIgnoreCase);
    public FactionResearch GetResearch(string faction)
    {
        if (!_factions.TryGetValue(faction, out var research))
            _factions[faction] = research = new FactionResearch();
        return research;
    }

    public List<GeneratedReagentData> Selectable => GetResearch("corporate").Selectable;
    public TimeSpan NextReroll => GetResearch("corporate").NextReroll;
    public Dictionary<int, (string, string, TimeSpan, bool, GeneratedReagentData, bool, bool)> ResearchData => GetResearch("corporate").Reports;
    public TimeSpan RerollTime = TimeSpan.FromSeconds(180);
    public TimeSpan PickedRerollTime = TimeSpan.FromSeconds(360);
    public int ResearchChemAmount = 6;
    public float ResearchCashRewardMult = 500;
    private bool ready;
    private int _nextContractId;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan XClearanceLockout = TimeSpan.FromMinutes(60);

    [Dependency] private ServerReagentGeneratorSystem _generator = default!;
    [Dependency] private IGameTiming _timer = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private IPrototypeManager _protoman = default!;
    [Dependency] private MetaDataSystem _mets = default!;
    [Dependency] private CorporateConsoleSystem _corpo = default!;
    [Dependency] private ColonyBudgetSystem _colbud = default!;
    [Dependency] private SharedRequisitionsSystem _reqsys = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private ILogManager _logman = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private XRFScannerSystem _scanner = default!;
    [Dependency] private SharedGameTicker _ticker = default!;

    private Dictionary<Entity<ResearchDataTerminalComponent>, int> _printing = [];
    private HashSet<Entity<ResearchDataTerminalComponent>> _printingLast = [];

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logman.GetSawmill("reagent");

        SubscribeLocalEvent<PostGameMapLoad>(OnLoadingMaps);
        SubscribeLocalEvent<ResearchDataTerminalComponent, BoundUIOpenedEvent>(OnUiOpen);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
        Subs.BuiEvents<ResearchDataTerminalComponent>(ResearchDataTerminalUI.Key, subs =>
        {
            subs.Event<ResearchDataTerminalAttemptUpgradeBuiMsg>(OnUpgradeAttempt);
            subs.Event<CMUResearchReduceCooldownBuiMsg>(OnReduceCooldown);
            subs.Event<ResearchDataTerminalPickChemBuiMsg>(OnPickChem);
            subs.Event<ResearchDataTerminalPrintLastBuiMsg>(OnPrintLast);
            subs.Event<ResearchDataTerminalPrintChemBuiMsg>(OnPrintRequest);
        });
        Subs.CVar(_cfg, CCVars.PickWaitTime, time => PickedRerollTime = TimeSpan.FromSeconds(time), true);
        Subs.CVar(_cfg, CCVars.RefreshTime, time => RerollTime = TimeSpan.FromSeconds(time), true);
        Subs.CVar(_cfg, CCVars.TerminalChems, chems => ResearchChemAmount = chems, true);
        Subs.CVar(_cfg, CCVars.CashRewardMult, dosh => ResearchCashRewardMult = dosh, true);
        Subs.CVar(_cfg, CCVars.XClearanceLockout, t => XClearanceLockout = TimeSpan.FromSeconds(t), true);
    }

    protected override void OnResearchBalanceChanged(string faction) => UpdateFactionUI(faction);

    private void UpdateFactionUI(string faction, bool announce = false)
    {
        var query = EntityQueryEnumerator<ResearchDataTerminalComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!string.Equals(comp.Faction, faction, StringComparison.OrdinalIgnoreCase))
                continue;
            if (announce)
                _chat.TrySendInGameICMessage(uid, Loc.GetString("research-chem-terminal-update"),
                    InGameICChatType.Speak, false, ignoreActionBlocker: true);
            UpdateUI((uid, comp));
        }
    }

    private void OnCleanup(RoundRestartCleanupEvent args)
    {
        ready = false;
        DDIDiscovered = false;
        _factions.Clear();
        _nextContractId = 0;
        ResetResearchAccounts();
        _printing.Clear();
        _printingLast.Clear();
    }

    public void OnLoadingMaps(PostGameMapLoad args)
    {
        ready = true;
    }


    private int UpgradeCost(string faction)
    {
        var clearance = GetClearance(faction);
        return (_researchLevelIncreaseMult * clearance) + 1;
    }

    private void OnUpgradeAttempt(Entity<ResearchDataTerminalComponent> ent, ref ResearchDataTerminalAttemptUpgradeBuiMsg args)
    {
        var faction = ent.Comp.Faction;
        var clearance = GetClearance(faction);
        if (clearance == 5 && _ticker.RoundDuration() < XClearanceLockout)
        {
            UpdateUI(ent);
            return;
        }
        var cost = UpgradeCost(faction);
        if (clearance >= 6 || GetCredits(faction) < cost)
            return;
        UpdateClearance(GetCredits(faction) - cost, clearance + 1, faction);
        if (clearance == 5)
        {
            if (TryGetCipherElevator(ent.Owner, args.Actor, out var elevator))
            {
                elevator.Comp.Orders.Add(new RequisitionsEntry { Cost = 0, Crate = "CMUCrateSecureCipheringExperiment" });
                SpawnNextToOrDrop("CMUCipherHintPaperInformDeliv", ent.Owner);
            }
            else
                SpawnNextToOrDrop("CMUCipherHintPaper", ent.Owner);
        }
        UpdateFactionUI(faction);
    }

    public bool ReduceCooldown(string faction)
    {
        var research = GetResearch(faction);
        if (!research.Picked || research.NextReroll <= _timer.CurTime || GetCredits(faction) < 1)
            return false;
        UpdateClearance(GetCredits(faction) - 1, -1, faction);
        research.NextReroll = TimeSpan.FromTicks(Math.Max(_timer.CurTime.Ticks,
            (research.NextReroll - TimeSpan.FromSeconds(60)).Ticks));
        if (research.NextReroll <= _timer.CurTime)
            RerollChems(faction);
        UpdateFactionUI(faction);
        return true;
    }

    private void OnReduceCooldown(Entity<ResearchDataTerminalComponent> ent, ref CMUResearchReduceCooldownBuiMsg args)
    {
        ReduceCooldown(ent.Comp.Faction);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        foreach (var printee in _printingLast)
            PrintLast(printee);
        _printingLast.Clear();
        foreach (var printee in _printing)
            PrintData(printee.Key, printee.Value);
        _printing.Clear();
        if (!ready)
            return;
        // Initialize accounts even when their terminals are delivered later in the round.
        foreach (var faction in new[] { "corporate", "govfor", "opfor", "colony" })
            GetResearch(faction);
        foreach (var (faction, research) in _factions.ToArray())
        {
            if (_timer.CurTime >= research.NextReroll)
                RerollChems(faction);
        }
    }

    public void LegalizeChem(GeneratedReagentData chem)
    {
        _generator.ChemicalGenClassesList["TAU"].Add(chem.ID);
        foreach (var ef in chem.Effects)
        {
            _generator.CheckGeneratedProperties(ef.Key);
        }
        HashSet<string> str = [chem.RecipeHint];
        _generator.GenerateRecipe(ref chem, str);
        var ev = new GenerateReagentEvent(chem);
        RaiseLocalEvent(ev);
        RaiseNetworkEvent(ev);
        _generator.ProceduralReagentData.Add(chem.ID, chem);
    }

    /// <summary>
    /// Registers an exact admin-authored chemical and prints a materializable contract for it.
    /// </summary>
    public EntityUid? IssueAdminContract(EntityUid source, GeneratedReagentData chem)
    {
        LegalizeChem(chem);
        return PrintContract(source, chem.ID, true);
    }

    public void CompleteChemical(ReagentPrototype proto, string faction, EntityUid? scanner)
    {
        if (!GetResearch(faction).CompletedChemicals.Add(proto.ID))
            return;
        // Chemical definitions are global, but each faction earns its own first-scan reward.
        if (_generator.IdentifiedChemicals.TryAdd(proto.ID, proto.Reward))
            RaiseNetworkEvent(new IdentifyChemicalEvent(proto.ID, proto.Reward));
        UpdateClearance(GetCredits(faction) + proto.Reward, -1, faction);
        if (faction != string.Empty)
        {
            if (string.Equals(faction, "corporate", StringComparison.OrdinalIgnoreCase))
            {
                _corpo.AddToCorporateBudget(ResearchCashRewardMult * proto.Reward);
                return;
            }
            if (string.Equals(faction, "colony", StringComparison.OrdinalIgnoreCase))
            {
                _colbud.AddToBudget(ResearchCashRewardMult * proto.Reward);
                return;
            }
            if (string.Equals(faction, "govfor", StringComparison.OrdinalIgnoreCase))
            {
                _reqsys.ChangeBudget((int)MathF.Round(ResearchCashRewardMult * proto.Reward), "govfor");
                return;
            }
            if (string.Equals(faction, "opfor", StringComparison.OrdinalIgnoreCase))
            {
                _reqsys.ChangeBudget((int)MathF.Round(ResearchCashRewardMult * proto.Reward), "opfor");
                return;
            }
        }
        if (scanner is null) // guess they don't *really* need that money then
            return;
        int amount = (int)MathF.Round(ResearchCashRewardMult * proto.Reward);
        while (amount > 0)
        {
            switch (amount)
            {
                case >= 1000:
                    SpawnNextToOrDrop("RMCSpaceCash1000", scanner.Value);
                    amount -= 1000;
                    break;
                case >= 100:
                    SpawnNextToOrDrop("RMCSpaceCash100", scanner.Value);
                    amount -= 100;
                    break;
                case >= 10:
                    SpawnNextToOrDrop("RMCSpaceCash10", scanner.Value);
                    amount -= 10;
                    break;
                default:
                    SpawnNextToOrDrop("RMCSpaceCash", scanner.Value);
                    amount--;
                    break;
            }
        }
    }

    private void OnUiOpen(Entity<ResearchDataTerminalComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUI(ent);
    }

    private void UpdateUI(Entity<ResearchDataTerminalComponent> ent)
    {
        var faction = ent.Comp.Faction;
        var research = GetResearch(faction);
        TimeSpan? xLockedUntil = null;
        if (GetClearance(faction) == 5 && _ticker.RoundDuration() < XClearanceLockout)
            xLockedUntil = _timer.CurTime + (XClearanceLockout - _ticker.RoundDuration());
        var state = new ResearchDataTerminalBuiState(
            ids: research.Selectable.ToList(),
            data: new(research.Reports),
            nextUpdate: research.NextReroll,
            lastTime: research.LastTime,
            credits: GetCredits(faction),
            clearance: GetClearance(faction),
            upgradecost: UpgradeCost(faction),
            xLockedUntil: xLockedUntil,
            picked: research.Picked);
        _ui.SetUiState(ent.Owner, ResearchDataTerminalUI.Key, state);
    }

    /// <summary>
    /// Atomically reserves one contract for a faction before contract generation raises further events.
    /// </summary>
    public bool TryReserveContract(string faction, string id, out GeneratedReagentData reagent)
    {
        var research = GetResearch(faction);
        var index = research.Selectable.FindIndex(candidate => candidate.ID == id);
        if (research.Picked || index < 0)
        {
            reagent = default;
            return false;
        }

        reagent = research.Selectable[index];
        research.Picked = true;
        research.NextReroll = _timer.CurTime + PickedRerollTime;
        research.LastTime = _timer.CurTime;
        research.Selectable.RemoveAt(index);

        // Push the lock before contract generation can raise any further events. Open windows on every
        // same-faction terminal are disabled together, and stale requests are rejected above.
        UpdateFactionUI(faction);
        return true;
    }

    public bool PickChem(string id, Entity<ResearchDataTerminalComponent>? ent = null)
    {
        var faction = ent?.Comp.Faction ?? "corporate";
        if (!TryReserveContract(faction, id, out var reagent))
            return false;

        LegalizeChem(reagent);
        if (ent is { } terminal)
            PrintContract(terminal, reagent.ID);
        return true;
    }

    private void OnPickChem(Entity<ResearchDataTerminalComponent> ent, ref ResearchDataTerminalPickChemBuiMsg args)
    {
        PickChem(args.Pick, ent);
        UpdateUI(ent);
    }

    private void OnPrintLast(Entity<ResearchDataTerminalComponent> ent, ref ResearchDataTerminalPrintLastBuiMsg args)
    {

        _printingLast.Add(ent);
    }

    private void OnPrintRequest(Entity<ResearchDataTerminalComponent> ent, ref ResearchDataTerminalPrintChemBuiMsg args)
    {
        //_sawmill.Info($"WE ARE TRYING TO PRINT INDEX {args.Index}");
        _printing[ent] = args.Index;
    }

    private void PrintLast(Entity<ResearchDataTerminalComponent> ent)
    {
        var research = GetResearch(ent.Comp.Faction);
        if (research.LastPickName == string.Empty || research.LastPick == string.Empty)
            return;
        string name = Loc.GetString("research-data-synthesis-name", ("NAME", research.LastPickName));
        var paper = SpawnNextToOrDrop("CMUWYPaper", ent.Owner);
        _mets.SetEntityName(paper, name);
        //unparity, because the experiment number is re-randomized
        _paper.SetContent(paper, research.LastPick);
        RemCompDeferred<ResearchReportComponent>(paper);
    }

    private void PrintContract(Entity<ResearchDataTerminalComponent> ent, string id)
    {
        PrintContract(ent.Owner, id, false);
    }

    private EntityUid? PrintContract(EntityUid source, string id, bool materializable)
    {
        var dat = _generator.ProceduralReagentData[id];
        var reagents = _protoman.GetInstances<ReagentPrototype>();
        string name = Loc.GetString("research-data-contract-name", ("NAME", dat.Name));

        string text = string.Empty;
        text += Loc.GetString("cmu-paper-header-experiment") + '\n';
        text += Loc.GetString("cmu-paper-subheader-experiment", ("NAME", dat.Name)) + '\n';
        string expnum = string.Empty;
        List<string> exppre = new List<string>{ "C", "Q", "V", "W", "X", "Y", "Z" };
        expnum += _random.Pick(exppre);
        expnum += _random.Next(100, 1000);
        List<string> expsuf = new List<string> { "a", "b", "c" };
        text += Loc.GetString("cmu-paper-contract-experiment", ("EXP", expnum), ("NAME", dat.Name)) + '\n';
        HashSet<string> ingredients = [];
        HashSet<string> catalysts = [];
        foreach(var ingredient in dat.Recipe)
        {
            if (ingredient.Value.Item2)
            {
                catalysts.Add(Loc.GetString("research-report-ingredient", ("AMOUNT", ingredient.Value.Item1),
                    ("NAME", reagents[ingredient.Key].LocalizedName)) + '\n');
            }
            else
            {
                ingredients.Add(Loc.GetString("research-report-ingredient", ("AMOUNT", ingredient.Value.Item1),
                    ("NAME", reagents[ingredient.Key].LocalizedName)) + '\n');
            }
        }
        foreach(string str in ingredients)
        {
            text += str;
        }
        if (catalysts.Count > 0)
        {
            text += Loc.GetString("research-chem-catalyst") + '\n';
            foreach(string str in catalysts)
            {
                text += str;
            }
        }
        if (materializable)
        {
            text += Loc.GetString("admin-chemical-contract-properties-header") + '\n';
            var properties = _protoman.GetInstances<ReagentPropertyPrototype>();
            foreach (var property in dat.Effects.OrderBy(effect => properties[effect.Key].LocalizedName))
            {
                text += Loc.GetString(
                    "admin-chemical-contract-property-entry",
                    ("name", properties[property.Key].LocalizedName),
                    ("level", property.Value)) + '\n';
            }
        }

        text += Loc.GetString("cmu-paper-contract-footer") + '\n';
        var paperPrototype = materializable ? "CMUAdminChemicalContract" : "CMUResearchContract";
        var paper = SpawnNextToOrDrop(paperPrototype, source);
        _mets.SetEntityName(paper, name);
        _paper.SetContent(paper, text);
        if (materializable && TryComp<ResearchReportComponent>(paper, out var report))
        {
            report.Data = dat;
            report.Valid = true;
            report.Completed = true;
            DirtyEntity(paper);
        }
        if (!materializable)
        {
            var research = GetResearch(GetFaction(source));
            research.LastPickName = dat.Name;
            research.LastPick = text;
        }
        return paper;
    }
    private void PrintData(Entity<ResearchDataTerminalComponent> ent, int idx)
    {
        if (GetResearch(ent.Comp.Faction).Reports.TryGetValue(idx, out var value))
        {
            string name = string.Empty;
            if (value.Item4)
            {
                name = Loc.GetString("research-report-simulation-name", ("ID", value.Item5.Name));
            }
            else
            {
                name = Loc.GetString("research-report-analysis-name", ("NAME1", value.Item5.Name), ("NAME2", string.Empty));
            }
            var paper = SpawnNextToOrDrop("CMUWYPaper", ent.Owner);
            _mets.SetEntityName(paper, name);
            _paper.SetContent(paper, value.Item2);
            var datcomp = EnsureComp<ResearchReportComponent>(paper);
            datcomp.Valid = value.Item6;
            datcomp.Completed = value.Item7;
            datcomp.Data = value.Item5;
        }
    }

    private bool TryGetCipherElevator(EntityUid terminal, EntityUid actor, out Entity<RequisitionsElevatorComponent> elevator)
    {
        if (TryComp<MarineComponent>(actor, out var marine)
            && !string.IsNullOrEmpty(marine.Faction))
        {
            var actorQuery = EntityQueryEnumerator<RequisitionsElevatorComponent>();
            while (actorQuery.MoveNext(out var elevUid, out var elevComp))
            {
                if (elevComp.Faction.Equals(marine.Faction, StringComparison.OrdinalIgnoreCase))
                {
                    elevator = (elevUid, elevComp);
                    return true;
                }
            }
        }

        var xrf = GetNearestXRF(terminal);
        if (xrf != EntityUid.Invalid)
        {
            var net = _scanner.GetFactionElevator(xrf, null);
            if (net != NetEntity.Invalid
                && TryComp<RequisitionsElevatorComponent>(GetEntity(net), out var xrfComp))
            {
                elevator = (GetEntity(net), xrfComp);
                return true;
            }
        }

        elevator = default;
        return false;
    }

    public EntityUid GetNearestXRF(EntityUid ent)
    {
        if (!TryComp(ent, out TransformComponent? excomp))
            return EntityUid.Invalid;
        var scannersq = EntityQueryEnumerator<XRFScannerComponent>();
        List<(EntityUid, TransformComponent)> scanners = [];
        EntityUid closest = EntityUid.Invalid;
        float closestDistance = float.MaxValue;
        while (scannersq.MoveNext(out var xent, out var scanner))
        {
            if (!string.Equals(scanner.Faction, GetFaction(ent), StringComparison.OrdinalIgnoreCase))
                continue;
            if (!TryComp(xent, out TransformComponent? xcomp))
                continue;
            if (excomp.MapUid != xcomp.MapUid)
                continue;
            float dist = System.Numerics.Vector2.Distance(_xform.GetWorldPosition(ent), _xform.GetWorldPosition(xent));
            if (closestDistance > dist)
            {
                closestDistance = dist;
                closest = xent;
            }
        }
        return closest;
    }


    private void RerollChems(string faction)
    {
        var research = GetResearch(faction);
        research.Selectable.Clear();
        research.Picked = false;
        for (int i = 0; i < ResearchChemAmount; i++)
        {
            GeneratedReagentData data = new();
            data.Recipe = [];
            data.Effects = [];
            data.Class = ReagentClass.Ultra;
            _generator.GenerateName(ref data);
            // Offers are generated before registration; reserve a unique ID across faction pools.
            data.ID = $"{data.ID}-{faction}-{_nextContractId++}";
            data.GenTier = _random.Next(1, 4);
            _generator.GenerateStats(ref data);

            var roll = _random.Next(1, 101);
            switch (data.GenTier)
            {
                case 1:
                    data.ScanPointYield = 3;
                    if (roll <= 60)
                        data.RecipeHint = _random.Pick(_generator.ChemicalGenClassesList["C1"]);
                    else
                        data.RecipeHint = _random.Pick(_generator.ChemicalGenClassesList["C2"]);
                    break;
                case 2:
                    data.ScanPointYield = 5;
                    if (roll <= 40)
                        data.RecipeHint = _random.Pick(_generator.ChemicalGenClassesList["C2"]);
                    else
                        data.RecipeHint = _random.Pick(_generator.ChemicalGenClassesList["C3"]);
                    break;
                case 3:
                    data.ScanPointYield = 7;
                    data.RecipeHint = _random.Pick(_generator.ChemicalGenClassesList["H1"]);
                    break;
                default:
                    data.ScanPointYield = 3;
                    data.RecipeHint = _random.Pick(_generator.ChemicalGenClassesList["C1"]);
                    break;
            }
            data.PropertyHint = _random.Pick(data.Effects.Keys);
            research.Selectable.Add(data);
        }
        research.NextReroll = _timer.CurTime + RerollTime;
        research.LastTime = _timer.CurTime;
        UpdateFactionUI(faction, announce: true);
    }
}
