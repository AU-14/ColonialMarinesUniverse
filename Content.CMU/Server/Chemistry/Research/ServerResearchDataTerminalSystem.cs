using Content.Server._AU14.Chemistry.Reagents;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Shared._AU14.Chemistry.Reagents;
using Content.Shared._AU14.Chemistry.Research;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.GameTicking;
using Robust.Client.UserInterface;
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

    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan RerollTime = TimeSpan.FromSeconds(180); //3 minutes
    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan PickedRerollTime = TimeSpan.FromSeconds(360); //6 minutes

    private string LastPickName = string.Empty;
    private string LastPick = string.Empty;

    private bool Picked = false;

    private bool DDISecured = false;
    private bool ready = false;
    [ViewVariables(VVAccess.ReadWrite)]
    public int ResearchChemAmount = 3;

    
    /// <summary>
    /// key = ID, value = (text, scan/sim time, scan or sim, data)
    /// </summary>
    public Dictionary<string, (string, TimeSpan, bool, GeneratedReagentData)> ResearchData = [];


    [Dependency] private ServerReagentGeneratorSystem _generator = default!;
    [Dependency] private IGameTiming _timer = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    private bool _upgrading = false;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<UpdateResearchConsoleEvent>(OnTerminalUpdate);
        SubscribeLocalEvent<PostGameMapLoad>(OnLoadingMaps);
        SubscribeLocalEvent<ResearchDataTerminalComponent, BoundUIOpenedEvent>(OnUiOpen);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
        Subs.BuiEvents<ResearchDataTerminalComponent>(ResearchDataTerminalUI.Key, subs =>
        {
            subs.Event<ResearchDataTerminalAttemptUpgradeBuiMsg>(OnUpgradeAttempt);
            subs.Event<ResearchDataTerminalPickChemBuiMsg>(OnPickChem);
        });
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
        ResearchData.Clear();
        LastPick = string.Empty;
        LastPickName = string.Empty;
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
                UpdateClearance(Credits - cost, Clearance + 1);
            }
            _upgrading = false;
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

    public void CompleteChemical(ReagentPrototype proto)
    {
        _generator.IdentifiedChemicals.Add(proto.ID, proto.Reward);
        var ev = new UpdateDataTerminalClearanceEvent(-1, Credits + proto.Reward);
        RaiseLocalEvent(ev);
        RaiseNetworkEvent(ev);
        var ncv = new IdentifyChemicalEvent(proto.ID, proto.Reward);
        RaiseNetworkEvent(ncv);
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
            credits: Credits,
            clearance: Clearance,
            upgradecost: cost
            picked: Picked);
        _ui.SetUiState(ent, ResearchDataTerminalUI.Key, state);
    }
    public void PickChem(string id)
    {
        Picked = true;
        foreach (var reagent in _selectable)
        {
            if (reagent.ID == id)
            {
                LegalizeChem(reagent);
                _selectable.Remove(reagent);
                IDS.Remove(reagent.ID);
                NextReroll = _timer.CurTime + PickedRerollTime;
                var ev = new UpdateResearchConsoleEvent(_selectable, NextReroll);
                RaiseNetworkEvent(ev);
                break;
            }
        }
    }
    private void OnPickChem(Entity<ResearchDataTerminalComponent> ent, ref ResearchDataTerminalPickChemBuiMsg args)
    {
        PickChem(args.Pick);
        UpdateUI(ent);
    }


    private void PrintLast(Entity<ResearchDataTerminalComponent> ent)
    {
        if (LastPickName == string.Empty || LastPick == string.Empty)
            return;

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
        var ev = new UpdateResearchConsoleEvent(_selectable, NextReroll);
        RaiseLocalEvent(ev);
        RaiseNetworkEvent(ev);
    }
}
