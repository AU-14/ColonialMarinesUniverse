using Content.Server._AU14.Chemistry.Reagents;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Shared._AU14.Chemistry.Reagents;
using Content.Shared._AU14.Chemistry.Research;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Server._AU14.Chemistry.Research;

public sealed partial class ServerResearchDataTerminalSystem : SharedResearchDataTerminalSystem
{
    private List<GeneratedReagentData> _selectable = [];

    public List<GeneratedReagentData> Selectable { get => _selectable; }
    public TimeSpan NextReroll = TimeSpan.Zero;
    public int Clearance = 1; //6 is "X" clearance
    public int Credits = 0;
    public bool DDIDiscovered = false;

    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan RerollTime = TimeSpan.FromSeconds(180); //3 minutes
    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan PickedRerollTime = TimeSpan.FromSeconds(360); //6 minutes

    private bool DDISecured = false;

    [ViewVariables(VVAccess.ReadWrite)]
    public int ResearchChemAmount = 3;

    [Dependency] private ServerReagentGeneratorSystem _generator = default!;
    [Dependency] private IGameTiming _timer = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ResearchDataTerminalComponent, UpdateResearchConsoleEvent>(OnTerminalUpdate);
    }

    private void OnTerminalUpdate(Entity<ResearchDataTerminalComponent> ent, ref UpdateResearchConsoleEvent args)
    {
        _chat.TrySendInGameICMessage(ent.Owner, Loc.GetString("research-chem-terminal-update"),
            InGameICChatType.Speak, false, ignoreActionBlocker: true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
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
                
                _selectable.Remove(reagent);
                break;
            }
        }
    }

    private void RerollChems()
    {
        _selectable.Clear();
        for (int i = 0; i < ResearchChemAmount; i++)
        {
            GeneratedReagentData data = new();
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
        }
        NextReroll = _timer.CurTime + RerollTime;
        var ev = new UpdateResearchConsoleEvent(_selectable, NextReroll);
        RaiseLocalEvent(ev);
        RaiseNetworkEvent(ev);
    }
}
