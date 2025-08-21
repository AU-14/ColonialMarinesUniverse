using Content.Shared.AU14;
using Robust.Shared.Prototypes;
using System.Collections.Generic;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.Roles;
using Robust.Shared.GameStates;

namespace Content.Shared.AU14.Objectives;
[RegisterComponent, NetworkedComponent]
public sealed partial class AuObjectiveComponent : Component
{


    public bool Active = false;

    [DataField("maxplayers", required: false)]

    public int Maxplayers { get; private set; } = 200;

    [DataField("applicableModes", required: true)]
    public List<string> ApplicableModes { get; private set; } = new();

    [DataField("disallowedThreats", required: false)]
    public List<string> disallowedThreats { get; private set; } = new();

    [DataField("possiblefactions", required: true)]
    public List<string> Factions { get; private set; } = new();

    [DataField("repeating", required: false)]
    public bool Repeating { get; private set; } = false;
//if true, will spawn again after completed/be continous depending on objectie type

    [DataField("objectivelevel", required: true)]
    public int ObjectiveLevel { get; private set; } = 1;
    // 1 minor 2 med 3 final

    [DataField("timer", required: false)]
    public float Timer { get; private set; } = 0f;
    // if zero disabled, if not completed by end of timer (in ms the objectie is failed )


    [DataField("custompoints", required: false)]
    public int CustomPoints { get; private set; } = 0;
    // If set, this will override the points given for completing the objective.

    public string Faction = string.Empty;
//active faction. seperate from disallowed
    public int Intellevel = 0;

    [DataField("ObjectiveDescription", required: true)]
    public string objectiveDescription { get; private set; } = default!;


    [DataField("factioneutral", required: false)]
    public bool FactionNeutral = false;
    // if true can be completed by any faction

    [DataField("roundEndMessage", required: false)]
    public string? RoundEndMessage { get; private set; } = default;
    // Custom message to be displayed when the round ends

    [DataField("maxrepeatable", required: false)]
    public int? MaxRepeatable { get; private set; } = null; // If set, limits how many times this objective can repeat

    public enum ObjectiveStatus
    {
        Incomplete,
        Completed,
        Failed
    }

    // For faction-neutral objectives, tracks status per faction

    public Dictionary<string, ObjectiveStatus> FactionStatuses { get; set; } = new();

    public int TimesCompleted = 0;
}
