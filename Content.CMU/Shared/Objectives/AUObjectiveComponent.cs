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
    [IdDataField]
    public string ID { get; private set; } = default!;


    public Boolean Active = false;

    [DataField("maxplayers", required: false)]

    public int Maxplayers { get; private set; } = 200;

    [DataField("applicableModes", required: true)]
    public List<string> ApplicableModes { get; private set; } = new();

    [DataField("disallowedThreats", required: true)]
    public List<string> disallowedThreats { get; private set; } = new();

    [DataField("possiblefactions", required: true)]
    public List<string> Factions { get; private set; } = new();

    [DataField("repeating", required: false)]
    public bool Repeating { get; private set; } = false;
//if true, will spawn again after completed/be continous depending on objectie type

    [DataField("objectivelevel", required: true)]
    public int ObjectiveLevel { get; private set; } = 1;
    // 1 minor 2 med 3 major

    [DataField("timer", required: false)]
    public float Timer { get; private set; } = 0f;
    // if zero disabled, if not completed by end of timer (in ms the objectie is failed )

    [DataField("final", required: true)]
    public bool Final { get; private set; } = false;
    //if its a final (win) objective

    public bool Completed = false;

    [DataField("custompoints", required: false)]
    public int CustomPoints { get; private set; } = 0;
    // If set, this will override the points given for completing the objective.

    public string Faction = string.Empty;
//active faction. seperate from disallowed
    public int Intellevel = 0;

    [DataField("ObjectiveDescription", required: true)]
    public string objectiveDescription { get; private set; } = default!;

    public bool failed = false;
    // if true objective is uncompleteable.
}
