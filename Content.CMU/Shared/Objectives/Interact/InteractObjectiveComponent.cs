using Robust.Shared.GameStates;

namespace Content.Shared.AU14.Objectives.Interact;

/// <summary>
/// Component for interact-type objectives. Players must interact with specific entities
/// (optionally requiring tools, skills, and access) to complete the objective.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class InteractObjectiveComponent : Component
{

    [DataField("Interactables", required: true)]
    public List<string> Interactables { get; private set; } = new();
    // prototypes able to be interacted with for this obj


    [DataField("doAfterMessagebegin", required: false)]
    public string DoAfterMessageBegin { get; private set; } = "You begin working...";
    // message displayed when player starts the action

    [DataField("doAfterMessagecomplete", required: false)]
    public string DoAfterMessageComplete { get; private set; } = "You finish working.";
    // message displayed when player completes the action


    [DataField("spawnmarker", required: false)]
    public string SpawnMarker { get; private set; } = string.Empty;
    // spawn markers, same as fetch, kill etc etc etc

    [DataField("amounttospawn", required: false)]
    public int AmountToSpawn { get; private set; } = 1;
    // if spawn is true spawns on markers

    [DataField("spawn")]
    public bool Spawn { get; private set; } = false;
    // if the ents should be spawned


    [DataField("interactionsneeded")]
    public int Interactionsneeded { get; private set; } = 1;
    // amount of interactions needed for each specific ent completion; useful for stuff like repairing and you want it to take multiple times

    [DataField("completionsperent")]
    public int CompletionsPerEnt { get; private set; } = 1;
    // Amount of times each specific ent can be completed. Only used if this is a repeating objective.

    [DataField("skills", required: false)]
    public List<string> Skills { get; private set; } = new();
    // skills needed to interact


    [DataField("access", required: false)]
    public List<string> Access { get; private set; } = new();
    // access levels needed to interact, cool if you want something like a lock the commander needs to do personally

    [DataField("tool", required: false)]
    // tools needed to do each individual interact, sequential and looping so if you for example have this as wrench, screwdriver, crowbar and you have interactions needed set to 9 it will cycle through wrench, screwdriver, crowbar for each iteration
    public List<string>? Tools { get; private set; } = null;


    [DataField("destroyoncomplete")]
    public bool DestroyOnComplete { get; private set; } = false;
    // if each specific ent should be destroyed after completionsperent

    [DataField("interacttime", required: false)]
    public float InteractTime { get; private set; } = 4;
    // time for the doafter in seconds

    /// <summary>
    /// Total completions needed across all entities to complete the objective.
    /// If 0, defaults to AmountToSpawn (one completion per spawned entity).
    /// </summary>
    [DataField("totalcompletionsneeded", required: false)]
    public int TotalCompletionsNeeded { get; private set; } = 0;

    // --- Runtime state (not serialized) ---

    /// <summary>
    /// Whether entities have already been spawned/registered for this objective.
    /// </summary>
    public bool EntitiesSpawned = false;

    /// <summary>
    /// Per-faction tracking of how many entity completions have been achieved.
    /// </summary>
    public Dictionary<string, int> CompletionsPerFaction { get; set; } = new();
}
