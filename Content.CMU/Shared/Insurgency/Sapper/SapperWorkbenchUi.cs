using System;
using System.Collections.Generic;
using Robust.Shared.Serialization;

namespace Content.Shared._AU14.Insurgency.Sapper;

[Serializable, NetSerializable]
public enum SapperWorkbenchUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class SapperWorkbenchBuiState : BoundUserInterfaceState
{
    public string? WeaponName { get; }

    /// <summary>Prototype id of the loaded weapon, so the client can draw its sprite centered.</summary>
    public string? WeaponPrototype { get; }

    public List<SapperWorkbenchSlotState> Slots { get; }
    public List<SapperWorkbenchStatLine> Stats { get; }
    public List<SapperWorkbenchMaterialState> Materials { get; }
    public List<SapperWorkbenchRecipeState> Recipes { get; }

    public SapperWorkbenchBuiState(
        string? weaponName,
        string? weaponPrototype,
        List<SapperWorkbenchSlotState> slots,
        List<SapperWorkbenchStatLine> stats,
        List<SapperWorkbenchMaterialState> materials,
        List<SapperWorkbenchRecipeState> recipes)
    {
        WeaponName = weaponName;
        WeaponPrototype = weaponPrototype;
        Slots = slots;
        Stats = stats;
        Materials = materials;
        Recipes = recipes;
    }
}

[Serializable, NetSerializable]
public sealed class SapperWorkbenchSlotState
{
    public string SlotId { get; }
    public string SlotName { get; }
    public string? AttachmentName { get; }
    public bool CanAdd { get; }
    public bool CanRemove { get; }

    public SapperWorkbenchSlotState(string slotId, string slotName, string? attachmentName, bool canAdd, bool canRemove)
    {
        SlotId = slotId;
        SlotName = slotName;
        AttachmentName = attachmentName;
        CanAdd = canAdd;
        CanRemove = canRemove;
    }
}

/// <summary>One stored material: its prototype id (for eject messages), display name, and sheet count.</summary>
[Serializable, NetSerializable]
public sealed class SapperWorkbenchMaterialState
{
    public string Id { get; }
    public string Name { get; }
    public int Count { get; }

    public SapperWorkbenchMaterialState(string id, string name, int count)
    {
        Id = id;
        Name = name;
        Count = count;
    }
}

[Serializable, NetSerializable]
public sealed class SapperWorkbenchStatLine
{
    public string Text { get; }
    public bool Buff { get; }

    public SapperWorkbenchStatLine(string text, bool buff)
    {
        Text = text;
        Buff = buff;
    }
}

[Serializable, NetSerializable]
public sealed class SapperWorkbenchRecipeState
{
    public int Index { get; }
    public string Name { get; }
    public string Prototype { get; }
    public Dictionary<string, int> Materials { get; }

    /// <summary>Loose-item ingredients, each with a representative icon prototype for the UI.</summary>
    public List<SapperWorkbenchIngredientState> Ingredients { get; }

    public bool CanBuild { get; }

    public SapperWorkbenchRecipeState(int index, string name, string prototype, Dictionary<string, int> materials, List<SapperWorkbenchIngredientState> ingredients, bool canBuild)
    {
        Index = index;
        Name = name;
        Prototype = prototype;
        Materials = materials;
        Ingredients = ingredients;
        CanBuild = canBuild;
    }
}

/// <summary>One loose-item ingredient line: display name, needed count, icon entity prototype.</summary>
[Serializable, NetSerializable]
public sealed class SapperWorkbenchIngredientState
{
    public string Name { get; }
    public int Count { get; }
    public string? Icon { get; }

    public SapperWorkbenchIngredientState(string name, int count, string? icon)
    {
        Name = name;
        Count = count;
        Icon = icon;
    }
}

[Serializable, NetSerializable]
public sealed class SapperWorkbenchAddAttachmentMessage : BoundUserInterfaceMessage
{
    public string SlotId { get; }

    public SapperWorkbenchAddAttachmentMessage(string slotId)
    {
        SlotId = slotId;
    }
}

[Serializable, NetSerializable]
public sealed class SapperWorkbenchRemoveAttachmentMessage : BoundUserInterfaceMessage
{
    public string SlotId { get; }

    public SapperWorkbenchRemoveAttachmentMessage(string slotId)
    {
        SlotId = slotId;
    }
}

[Serializable, NetSerializable]
public sealed class SapperWorkbenchTakeWeaponMessage : BoundUserInterfaceMessage
{
}

/// <summary>Eject all stored sheets of one material from the bench.</summary>
[Serializable, NetSerializable]
public sealed class SapperWorkbenchEjectMaterialMessage : BoundUserInterfaceMessage
{
    public string MaterialId { get; }

    public SapperWorkbenchEjectMaterialMessage(string materialId)
    {
        MaterialId = materialId;
    }
}

[Serializable, NetSerializable]
public sealed class SapperWorkbenchCraftMessage : BoundUserInterfaceMessage
{
    public int RecipeIndex { get; }

    public SapperWorkbenchCraftMessage(int recipeIndex)
    {
        RecipeIndex = recipeIndex;
    }
}
