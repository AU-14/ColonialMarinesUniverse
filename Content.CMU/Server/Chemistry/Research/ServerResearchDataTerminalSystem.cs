using Content.Server._AU14.Chemistry.Reagents;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Shared._AU14.Chemistry.Reagents;
using Content.Shared._AU14.Chemistry.Research;
using Content.Shared.GameTicking;
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
    public int Clearance = 1; //6 is "X" clearance
    public int Credits = 0;
    public bool DDIDiscovered = false;

    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan RerollTime = TimeSpan.FromSeconds(180); //3 minutes
    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan PickedRerollTime = TimeSpan.FromSeconds(360); //6 minutes

    private bool DDISecured = false;
    private bool ready = false;
    [ViewVariables(VVAccess.ReadWrite)]
    public int ResearchChemAmount = 3;

    [Dependency] private ServerReagentGeneratorSystem _generator = default!;
    [Dependency] private IGameTiming _timer = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ChatSystem _chat = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<UpdateResearchConsoleEvent>(OnTerminalUpdate);
        SubscribeLocalEvent<PostGameMapLoad>(OnLoadingMaps);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
    }

    private void OnTerminalUpdate(UpdateResearchConsoleEvent args)
    {
        var query = EntityQueryEnumerator<ResearchDataTerminalComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            _chat.TrySendInGameICMessage(uid, Loc.GetString("research-chem-terminal-update"),
            InGameICChatType.Speak, false, ignoreActionBlocker: true);
        }
    }

    private void OnCleanup(RoundRestartCleanupEvent args)
    {
        ready = false;
        NextReroll = TimeSpan.Zero;
    }

    public void OnLoadingMaps(PostGameMapLoad args)
    {
        ready = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!ready)
            return;
        if (_timer.CurTime >= NextReroll)
        {
            RerollChems();
        }
    }

    public void PickChem(string id)
    {
        foreach (var reagent in _selectable)
        {
            if (reagent.ID == id)
            {
                _generator.LegalizeChem(reagent);
                _selectable.Remove(reagent);
                IDS.Remove(reagent.ID);
                NextReroll = _timer.CurTime + PickedRerollTime;
                var ev = new UpdateResearchConsoleEvent(_selectable, NextReroll);
                RaiseNetworkEvent(ev);
                break;
            }
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
            _generator.GenerateName(ref data);
            data.GenTier = _random.Next(1, 4);
            _generator.GenerateStats(ref data);

            var roll = _random.Next(1, 101);
            switch (data.GenTier)
            {
                case 1:
                    data.ScanPointYield = 3;
                    if (roll <= 60)
                        data.RecipeHint = _random.Pick(_generator.GenClasses["C1"]);
                    else
                        data.RecipeHint = _random.Pick(_generator.GenClasses["C2"]);
                    break;
                case 2:
                    data.ScanPointYield = 5;
                    if (roll <= 40)
                        data.RecipeHint = _random.Pick(_generator.GenClasses["C2"]);
                    else
                        data.RecipeHint = _random.Pick(_generator.GenClasses["C3"]);
                    break;
                case 3:
                    data.ScanPointYield = 7;
                    data.RecipeHint = _random.Pick(_generator.GenClasses["H1"]);
                    break;
                default:
                    data.ScanPointYield = 3;
                    data.RecipeHint = _random.Pick(_generator.GenClasses["C1"]);
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
