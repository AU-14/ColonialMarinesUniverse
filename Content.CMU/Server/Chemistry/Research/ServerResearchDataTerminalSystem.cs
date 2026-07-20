using Content.Server._AU14.Chemistry.Reagents;
using Content.Server.AU14.ColonyEconomy;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Shared._AU14.Chemistry.Reagents;
using Content.Shared._AU14.Chemistry.Research;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.CCVar;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.GameTicking;
using Content.Shared.Paper;
using Discord.Rest;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Server._AU14.Chemistry.Research;

public sealed partial class ServerResearchDataTerminalSystem : SharedResearchDataTerminalSystem
{
    [ViewVariables(VVAccess.ReadOnly)]
    private List<GeneratedReagentData> _selectable = [];

    public List<GeneratedReagentData> Selectable { get => _selectable; }

    [ViewVariables(VVAccess.ReadOnly)]
    public List<string> IDS = [];
    public TimeSpan NextReroll = TimeSpan.Zero;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan RerollTime = TimeSpan.FromSeconds(180); //3 minutes
    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan PickedRerollTime = TimeSpan.FromSeconds(360); //6 minutes

    public TimeSpan LastTime = TimeSpan.Zero;

    private string LastPickName = string.Empty;
    private string LastPick = string.Empty;

    private bool Picked = false;

    private bool DDISecured = false;
    private bool ready = false;
    [ViewVariables(VVAccess.ReadOnly)]
    public int ResearchChemAmount = 6; // for sanity

    [ViewVariables(VVAccess.ReadOnly)]
    public float ResearchCashRewardMult = 500;

    /// <summary>
    /// legend = (ID, text, scan/sim time, scan or sim, data, valid, completed)
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<int, (string, string, TimeSpan, bool, GeneratedReagentData, bool, bool)> ResearchData = [];


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

    private Dictionary<Entity<ResearchDataTerminalComponent>, int> _printing = [];
    private HashSet<Entity<ResearchDataTerminalComponent>> _printingLast = [];

    private ISawmill _sawmill = default!;

    private bool _upgrading = false;
    private NetEntity _cipherPicker = NetEntity.Invalid;
    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logman.GetSawmill("reagent");
        SubscribeLocalEvent<UpdateResearchConsoleEvent>(OnTerminalUpdate);
        SubscribeLocalEvent<PostGameMapLoad>(OnLoadingMaps);
        SubscribeLocalEvent<ResearchDataTerminalComponent, BoundUIOpenedEvent>(OnUiOpen);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
        Subs.BuiEvents<ResearchDataTerminalComponent>(ResearchDataTerminalUI.Key, subs =>
        {
            subs.Event<ResearchDataTerminalAttemptUpgradeBuiMsg>(OnUpgradeAttempt);
            subs.Event<ResearchDataTerminalPickChemBuiMsg>(OnPickChem);
            subs.Event<ResearchDataTerminalPrintLastBuiMsg>(OnPrintLast);
            subs.Event<ResearchDataTerminalPrintChemBuiMsg>(OnPrintRequest);
        });
        Subs.CVar(_cfg, CCVars.PickWaitTime, time => PickedRerollTime = TimeSpan.FromSeconds(time), true);
        Subs.CVar(_cfg, CCVars.RefreshTime, time => RerollTime = TimeSpan.FromSeconds(time), true);
        Subs.CVar(_cfg, CCVars.TerminalChems, chems => ResearchChemAmount = chems, true);
        Subs.CVar(_cfg, CCVars.CashRewardMult, dosh => ResearchCashRewardMult = dosh, true);
    }

    private void OnTerminalUpdate(UpdateResearchConsoleEvent args)
    {
        var query = EntityQueryEnumerator<ResearchDataTerminalComponent>();
        Picked = false;
        while (query.MoveNext(out var uid, out var comp))
        {
            _chat.TrySendInGameICMessage(uid, Loc.GetString("research-chem-terminal-update"),
            InGameICChatType.Speak, false, ignoreActionBlocker: true);
            UpdateUI(uid);
        }
    }

    private void OnCleanup(RoundRestartCleanupEvent args)
    {
        ready = false;
        Clearance = 1;
        Credits = 0;
        DDIDiscovered = false;
        _upgrading = false;
        DDISecured = false;
        NextReroll = TimeSpan.Zero;
        LastTime = TimeSpan.Zero;
        ResearchData.Clear();
        LastPick = string.Empty;
        LastPickName = string.Empty;
        _printing.Clear();
        _printingLast.Clear();
        _cipherPicker = NetEntity.Invalid;
    }

    public void OnLoadingMaps(PostGameMapLoad args)
    {
        ready = true;
    }


    private void OnUpgradeAttempt(Entity<ResearchDataTerminalComponent> ent, ref ResearchDataTerminalAttemptUpgradeBuiMsg args)
    {
        int cost = 1;
        if (Clearance == 5)
        {
            cost = 5;
        }
        else cost = (_researchLevelIncreaseMult * Clearance) + 1;
        if (Credits >= cost)
        {
            if (Clearance == 5)
            {
                NetEntity net = GetNetEntity(ent.Owner);
                _cipherPicker = net;
            }
            _upgrading = true;
        }
        UpdateUI(ent);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_upgrading)
        {
            int cost = 1;
            if (Clearance == 5)
            {
                cost = 5;
            }
            else cost = (_researchLevelIncreaseMult * Clearance) + 1;
            if (Clearance < 6)
            {
                bool ciph = false;
                if (Clearance + 1 == 6)
                {
                    ciph = true;
                }
                UpdateClearance(Credits - cost, Clearance + 1);
                var query = EntityQueryEnumerator<ResearchDataTerminalComponent>();
                EntityUid cip = GetEntity(_cipherPicker);
                while(query.MoveNext(out var ent, out var comp))
                {
                    UpdateUI(ent);
                    if (ciph)
                    {
                        if (ent == cip)
                        {
                            SpawnNextToOrDrop("CMUCipherHintPaper", ent);
                        }
                        else
                        {
                            SpawnNextToOrDrop("CMUCipherHintPaperNoSpawn", ent);
                        }
                    }
                }
            }
            _upgrading = false;
        }
        if (_printingLast.Count > 0)
        {
            foreach(var printee in _printingLast)
            {
                PrintLast(printee);
                _printingLast.Remove(printee);
            }
        }
        if (_printing.Count > 0)
        {
            foreach(var printee in _printing)
            {
                PrintData(printee.Key, printee.Value);
                _printing.Remove(printee.Key);
            }
        }
        if (!ready)
            return;
        if (_timer.CurTime >= NextReroll)
        {
            RerollChems();
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

    public void CompleteChemical(ReagentPrototype proto, string faction, EntityUid? scanner)
    {
        _generator.IdentifiedChemicals.Add(proto.ID, proto.Reward);
        var ev = new UpdateDataTerminalClearanceEvent(-1, Credits + proto.Reward);
        RaiseLocalEvent(ev);
        RaiseNetworkEvent(ev);
        var ncv = new IdentifyChemicalEvent(proto.ID, proto.Reward);
        RaiseNetworkEvent(ncv);
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
        int cost = 1;
        if (Clearance == 5)
        {
            cost = 5;
        }
        else cost = (_researchLevelIncreaseMult * Clearance) + 1;
        var state = new ResearchDataTerminalBuiState(
            ids: _selectable,
            data: ResearchData,
            nextUpdate: NextReroll,
            lastTime: LastTime,
            credits: Credits,
            clearance: Clearance,
            upgradecost: cost,
            picked: Picked);
        _ui.SetUiState(ent.Owner, ResearchDataTerminalUI.Key, state);
    }
    private void UpdateUI(EntityUid ent)
    {
        int cost = 1;
        if (Clearance == 5)
        {
            cost = 5;
        }
        else cost = (_researchLevelIncreaseMult * Clearance) + 1;
        var state = new ResearchDataTerminalBuiState(
            ids: _selectable,
            data: ResearchData,
            nextUpdate: NextReroll,
            lastTime: LastTime,
            credits: Credits,
            clearance: Clearance,
            upgradecost: cost,
            picked: Picked);
        _ui.SetUiState(ent, ResearchDataTerminalUI.Key, state);
    }
    public void PickChem(string id, Entity<ResearchDataTerminalComponent>? ent = null)
    {
        Picked = true;
        foreach (var reagent in _selectable)
        {
            if (reagent.ID == id)
            {
                LegalizeChem(reagent);
                _selectable.Remove(reagent);
                IDS.Remove(reagent.ID);
                if (ent is not null)
                {
                    PrintContract(ent.Value, reagent.ID);
                }
                NextReroll = _timer.CurTime + PickedRerollTime;
                LastTime = _timer.CurTime;
                var ev = new UpdateResearchConsoleEvent(_selectable, NextReroll);
                RaiseNetworkEvent(ev);
                break;
            }
        }
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
        _printing.Add(ent, args.Index);
    }

    private void PrintLast(Entity<ResearchDataTerminalComponent> ent)
    {
        if (LastPickName == string.Empty || LastPick == string.Empty)
            return;
        string name = Loc.GetString("research-data-synthesis-name", ("NAME", LastPickName));
        var paper = SpawnNextToOrDrop("CMUWYPaper", ent.Owner);
        _mets.SetEntityName(paper, name);
        //unparity, because the experiment number is re-randomized
        _paper.SetContent(paper, LastPick);
        RemCompDeferred<ResearchReportComponent>(paper);
    }

    private void PrintContract(Entity<ResearchDataTerminalComponent> ent, string id)
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
        text += Loc.GetString("cmu-paper-contract-footer") + '\n';
        var paper = SpawnNextToOrDrop("CMUResearchContract", ent.Owner);
        _mets.SetEntityName(paper, name);
        _paper.SetContent(paper, text);
        LastPickName = dat.Name;
        LastPick = text;
    }
    private void PrintData(Entity<ResearchDataTerminalComponent> ent, int idx)
    {
        if (ResearchData.TryGetValue(idx, out var value))
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
    private void RerollChems()
    {
        _selectable.Clear();
        IDS.Clear();
        for (int i = 0; i < ResearchChemAmount; i++)
        {
            GeneratedReagentData data = new();
            data.Recipe = [];
            data.Effects = [];
            data.Class = ReagentClass.Ultra;
            _generator.GenerateName(ref data);
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
            _selectable.Add(data);
            IDS.Add(data.ID);
        }
        NextReroll = _timer.CurTime + RerollTime;
        LastTime = _timer.CurTime;
        var ev = new UpdateResearchConsoleEvent(_selectable, NextReroll);
        RaiseLocalEvent(ev);
        RaiseNetworkEvent(ev);
    }
}
