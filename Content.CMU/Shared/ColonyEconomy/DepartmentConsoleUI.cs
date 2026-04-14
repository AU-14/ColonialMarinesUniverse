using Content.Shared._RMC14.Requisitions;
using Robust.Shared.Serialization;

namespace Content.Shared.AU14.ColonyEconomy;

[Serializable, NetSerializable]
public enum DepartmentConsoleUi
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DepartmentEmployeeEntry
{
    public NetEntity IdCardUid { get; }
    public string Name { get; }
    public string JobTitle { get; }
    public int Salary { get; }
    public bool HasOverride { get; }

    public DepartmentEmployeeEntry(NetEntity idCardUid, string name, string jobTitle, int salary, bool hasOverride)
    {
        IdCardUid = idCardUid;
        Name = name;
        JobTitle = jobTitle;
        Salary = salary;
        HasOverride = hasOverride;
    }
}

[Serializable, NetSerializable]
public sealed class DepartmentConsoleBuiState : BoundUserInterfaceState
{
    public string DepartmentName { get; }
    public float DepartmentBudget { get; }
    public int DefaultSalary { get; }
    public List<DepartmentEmployeeEntry> Employees { get; }
    public List<DepartmentOrderCatalogCategory> Catalog { get; }

    public DepartmentConsoleBuiState(
        string departmentName,
        float departmentBudget,
        int defaultSalary,
        List<DepartmentEmployeeEntry> employees,
        List<DepartmentOrderCatalogCategory> catalog)
    {
        DepartmentName = departmentName;
        DepartmentBudget = departmentBudget;
        DefaultSalary = defaultSalary;
        Employees = employees;
        Catalog = catalog;
    }
}

// --- Messages from client to server ---


[Serializable, NetSerializable]
public sealed class DepartmentConsoleFireBuiMsg : BoundUserInterfaceMessage
{
    public NetEntity IdCardUid { get; }

    public DepartmentConsoleFireBuiMsg(NetEntity idCardUid)
    {
        IdCardUid = idCardUid;
    }
}

[Serializable, NetSerializable]
public sealed class DepartmentConsoleSetDefaultSalaryBuiMsg : BoundUserInterfaceMessage
{
    public int Salary { get; }

    public DepartmentConsoleSetDefaultSalaryBuiMsg(int salary)
    {
        Salary = salary;
    }
}

[Serializable, NetSerializable]
public sealed class DepartmentConsoleSetIndividualSalaryBuiMsg : BoundUserInterfaceMessage
{
    public NetEntity IdCardUid { get; }
    public int Salary { get; }

    public DepartmentConsoleSetIndividualSalaryBuiMsg(NetEntity idCardUid, int salary)
    {
        IdCardUid = idCardUid;
        Salary = salary;
    }
}

[Serializable, NetSerializable]
public sealed class DepartmentConsoleRemoveOverrideBuiMsg : BoundUserInterfaceMessage
{
    public NetEntity IdCardUid { get; }

    public DepartmentConsoleRemoveOverrideBuiMsg(NetEntity idCardUid)
    {
        IdCardUid = idCardUid;
    }
}

[Serializable, NetSerializable]
public sealed class DepartmentConsoleAnnounceBuiMsg : BoundUserInterfaceMessage
{
    public string Message { get; }

    public DepartmentConsoleAnnounceBuiMsg(string message)
    {
        Message = message;
    }
}

[Serializable, NetSerializable]
public sealed class DepartmentConsoleWithdrawBuiMsg : BoundUserInterfaceMessage
{
    public float Amount { get; }

    public DepartmentConsoleWithdrawBuiMsg(float amount)
    {
        Amount = amount;
    }
}

/// <summary>
///     A simplified catalog category sent to the department console client.
/// </summary>
[Serializable, NetSerializable]
public sealed class DepartmentOrderCatalogCategory
{
    public string Name { get; }
    public List<DepartmentOrderCatalogEntry> Entries { get; }

    public DepartmentOrderCatalogCategory(string name, List<DepartmentOrderCatalogEntry> entries)
    {
        Name = name;
        Entries = entries;
    }
}

[Serializable, NetSerializable]
public sealed class DepartmentOrderCatalogEntry
{
    public int CategoryIndex { get; }
    public int EntryIndex { get; }
    public string Name { get; }
    public int Cost { get; }

    public DepartmentOrderCatalogEntry(int categoryIndex, int entryIndex, string name, int cost)
    {
        CategoryIndex = categoryIndex;
        EntryIndex = entryIndex;
        Name = name;
        Cost = cost;
    }
}

/// <summary>
///     Message to order an item from the ASRS catalog via the department console.
/// </summary>
[Serializable, NetSerializable]
public sealed class DepartmentConsoleOrderBuiMsg : BoundUserInterfaceMessage
{
    public int CategoryIndex { get; }
    public int EntryIndex { get; }
    public string Reason { get; }
    public string DeliverTo { get; }

    public DepartmentConsoleOrderBuiMsg(int categoryIndex, int entryIndex, string reason, string deliverTo)
    {
        CategoryIndex = categoryIndex;
        EntryIndex = entryIndex;
        Reason = reason;
        DeliverTo = deliverTo;
    }
}
