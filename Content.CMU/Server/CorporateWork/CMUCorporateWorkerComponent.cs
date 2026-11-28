namespace Content.Server.CMU14.CorporateWork;

/// <summary>
///     How many paid corporate tasks this mob has finished, per kind of task, so repeating the
///     same one pays less. Lives on the mob, so it resets with a new character.
/// </summary>
[RegisterComponent]
[Access(typeof(CMUCorporateWorkSystem))]
public sealed partial class CMUCorporateWorkerComponent : Component
{
    [ViewVariables]
    public Dictionary<string, int> Completed = new();
}
