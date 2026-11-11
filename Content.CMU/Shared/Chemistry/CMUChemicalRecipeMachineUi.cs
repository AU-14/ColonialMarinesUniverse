using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Chemistry;

[Serializable, NetSerializable]
public enum CMUChemicalRecipeMachineUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CMUChemicalRecipeMachineUiState(
    string status,
    string? recipeName,
    List<string> inputs,
    List<string> outputs,
    bool canLoad,
    bool canSynthesize,
    bool hasRecipe,
    bool hasVessel) : BoundUserInterfaceState
{
    public string Status = status;
    public string? RecipeName = recipeName;
    public List<string> Inputs = inputs;
    public List<string> Outputs = outputs;
    public bool CanLoad = canLoad;
    public bool CanSynthesize = canSynthesize;
    public bool HasRecipe = hasRecipe;
    public bool HasVessel = hasVessel;
}

[Serializable, NetSerializable]
public sealed class CMUChemicalRecipeMachineLoadMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CMUChemicalRecipeMachineSynthesizeMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CMUChemicalRecipeMachineEjectMessage(bool recipe) : BoundUserInterfaceMessage
{
    public bool Recipe = recipe;
}
