using Content.Server.Chat.Systems;
using Content.Server.Stack;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.AU14.ColonyEconomy;
using Content.Shared.GameTicking;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Stacks;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.Access;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.AU14.ColonyEconomy;

public sealed class DepartmentConsoleSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly ColonyBudgetSystem _budget = default!;
    [Dependency] private readonly AdminConsoleSystem _adminConsole = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly SharedAccessSystem _accessSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DepartmentConsoleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DepartmentConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<DepartmentConsoleComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<DepartmentConsoleComponent, DepartmentConsoleFireBuiMsg>(OnFire);
        SubscribeLocalEvent<DepartmentConsoleComponent, DepartmentConsoleSetDefaultSalaryBuiMsg>(OnSetDefaultSalary);
        SubscribeLocalEvent<DepartmentConsoleComponent, DepartmentConsoleSetIndividualSalaryBuiMsg>(OnSetIndividualSalary);
        SubscribeLocalEvent<DepartmentConsoleComponent, DepartmentConsoleRemoveOverrideBuiMsg>(OnRemoveOverride);
        SubscribeLocalEvent<DepartmentConsoleComponent, DepartmentConsoleAnnounceBuiMsg>(OnAnnounce);
        SubscribeLocalEvent<DepartmentConsoleComponent, DepartmentConsoleWithdrawBuiMsg>(OnWithdraw);
        SubscribeLocalEvent<DepartmentConsoleComponent, DepartmentConsoleOrderBuiMsg>(OnOrder);
        SubscribeLocalEvent<DepartmentConsoleComponent, EntInsertedIntoContainerMessage>(OnCashInserted);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    /// <summary>
    ///     When a department console spawns, if another console with the same DepartmentId already
    ///     exists, share the same Members/SalaryOverrides/Budget/DefaultSalary so they stay in sync.
    /// </summary>
    private void OnMapInit(EntityUid uid, DepartmentConsoleComponent comp, MapInitEvent args)
    {
        if (comp.DepartmentId == null)
            return;

        var query = EntityQueryEnumerator<DepartmentConsoleComponent>();
        while (query.MoveNext(out var otherUid, out var other))
        {
            if (otherUid == uid)
                continue;

            if (other.DepartmentId != comp.DepartmentId)
                continue;

            // Found an existing console with the same department — share references
            comp.Members = other.Members;
            comp.SalaryOverrides = other.SalaryOverrides;
            comp.DepartmentBudget = other.DepartmentBudget;
            comp.DefaultSalary = other.DefaultSalary;
            return;
        }
    }

    /// <summary>
    ///     When a player spawns (round-start or late-join), find their ID card and add them
    ///     to every department console whose DepartmentId matches one of their job departments.
    ///     CLF guerillas are a special case: they get added directly to the Labor console
    ///     without being a formal member of the department prototype.
    /// </summary>
    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId == null)
            return;

        if (!_idCard.TryFindIdCard(ev.Mob, out var idCard))
            return;

        var idCardUid = idCard.Owner;

        // Determine which department prototype IDs this job belongs to.
        var jobDepartments = new HashSet<string>();
        foreach (var dept in _prototypeManager.EnumeratePrototypes<DepartmentPrototype>())
        {
            if (dept.Roles.Contains(ev.JobId))
                jobDepartments.Add(dept.ID);
        }

        // Special case: CLF guerillas get added to the Labor department console
        // without being in the department prototype itself.
        if (ev.JobId == "AU14JobCLFGuerilla")
            jobDepartments.Add("AU14DepartmentLabor");

        if (jobDepartments.Count == 0)
            return;

        // Add the ID card to every matching department console.
        var query = EntityQueryEnumerator<DepartmentConsoleComponent>();
        while (query.MoveNext(out var consoleUid, out var console))
        {
            if (console.DepartmentId == null)
                continue;

            if (!jobDepartments.Contains(console.DepartmentId))
                continue;

            if (console.Members.Add(idCardUid))
            {
                GrantDepartmentAccess(idCardUid, console);
                UpdateUiState(consoleUid, console);
            }
        }
    }

    /// <summary>
    ///     Grants the department's access level to the given ID card entity.
    /// </summary>
    private void GrantDepartmentAccess(EntityUid idCardUid, DepartmentConsoleComponent dept)
    {
        if (dept.DepartmentAccessLevel == null)
            return;

        if (!TryComp<AccessComponent>(idCardUid, out var access))
            return;

        if (access.Tags.Contains(dept.DepartmentAccessLevel.Value))
            return;

        var newTags = new HashSet<ProtoId<AccessLevelPrototype>>(access.Tags)
        {
            dept.DepartmentAccessLevel.Value
        };
        _accessSystem.TrySetTags(idCardUid, newTags, access);
    }

    private void OnUiOpened(EntityUid uid, DepartmentConsoleComponent comp, BoundUIOpenedEvent args)
    {
        UpdateUiState(uid, comp);
    }

    /// <summary>
    ///     Builds a catalog snapshot from the nearest ASRS computer matching the department's configured faction.
    /// </summary>
    private List<DepartmentOrderCatalogCategory> BuildCatalog(EntityUid consoleUid, string asrsFaction)
    {
        var result = new List<DepartmentOrderCatalogCategory>();

        var consoleCoords = _transform.GetMapCoordinates(consoleUid);
        Entity<RequisitionsComputerComponent>? nearest = null;
        var nearestDist = float.MaxValue;

        var computers = EntityQueryEnumerator<RequisitionsComputerComponent>();
        while (computers.MoveNext(out var compUid, out var comp))
        {
            if (comp.Faction != asrsFaction)
                continue;

            var compCoords = _transform.GetMapCoordinates(compUid);
            if (consoleCoords.MapId != compCoords.MapId)
                continue;

            var dist = (compCoords.Position - consoleCoords.Position).LengthSquared();
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = (compUid, comp);
            }
        }

        if (nearest == null)
            return result;

        for (var catIdx = 0; catIdx < nearest.Value.Comp.Categories.Count; catIdx++)
        {
            var cat = nearest.Value.Comp.Categories[catIdx];
            var entries = new List<DepartmentOrderCatalogEntry>();
            for (var entIdx = 0; entIdx < cat.Entries.Count; entIdx++)
            {
                var entry = cat.Entries[entIdx];
                var name = entry.Name ?? _prototypeManager.Index<EntityPrototype>(entry.Crate).Name;
                entries.Add(new DepartmentOrderCatalogEntry(catIdx, entIdx, name, entry.Cost));
            }
            result.Add(new DepartmentOrderCatalogCategory(cat.Name, entries));
        }

        return result;
    }

    /// <summary>
    ///     Finds the nearest elevator matching the given faction.
    /// </summary>
    private Entity<RequisitionsElevatorComponent>? FindColonyElevator(EntityUid consoleUid, string faction = "colony")
    {
        var consoleCoords = _transform.GetMapCoordinates(consoleUid);
        Entity<RequisitionsElevatorComponent>? nearest = null;
        var nearestDist = float.MaxValue;

        var elevators = EntityQueryEnumerator<RequisitionsElevatorComponent, TransformComponent>();
        while (elevators.MoveNext(out var uid, out var elevator, out var xform))
        {
            if (elevator.Faction != faction)
                continue;

            var elevCoords = _transform.GetMapCoordinates(uid, xform);
            if (consoleCoords.MapId != elevCoords.MapId)
                continue;

            var dist = (elevCoords.Position - consoleCoords.Position).LengthSquared();
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = (uid, elevator);
            }
        }

        return nearest;
    }

    public void UpdateUiState(EntityUid uid, DepartmentConsoleComponent comp)
    {
        var employees = new List<DepartmentEmployeeEntry>();
        var toRemove = new List<EntityUid>();

        foreach (var idCardUid in comp.Members)
        {
            if (!TryComp<IdCardComponent>(idCardUid, out var idCard))
            {
                toRemove.Add(idCardUid);
                continue;
            }

            var name = idCard.FullName ?? "Unknown";
            var jobTitle = idCard.LocalizedJobTitle ?? "Unknown";
            var hasOverride = comp.SalaryOverrides.ContainsKey(idCardUid);
            var salary = hasOverride ? comp.SalaryOverrides[idCardUid] : comp.DefaultSalary;

            employees.Add(new DepartmentEmployeeEntry(
                GetNetEntity(idCardUid),
                name,
                jobTitle,
                salary,
                hasOverride));
        }

        // Clean up deleted/invalid entities
        foreach (var rem in toRemove)
        {
            comp.Members.Remove(rem);
            comp.SalaryOverrides.Remove(rem);
        }

        var catalog = BuildCatalog(uid, comp.AsrsFaction);

        var state = new DepartmentConsoleBuiState(
            comp.DepartmentName,
            comp.DepartmentBudget,
            comp.DefaultSalary,
            employees,
            catalog);

        _ui.SetUiState(uid, DepartmentConsoleUi.Key, state);
    }

    /// <summary>
    ///     Copies shared department state (Members, SalaryOverrides, DepartmentBudget, DefaultSalary)
    ///     from the source console to all other consoles with the same DepartmentId.
    /// </summary>
    private void SyncDepartmentState(EntityUid sourceUid, DepartmentConsoleComponent source)
    {
        if (source.DepartmentId == null)
            return;

        var query = EntityQueryEnumerator<DepartmentConsoleComponent>();
        while (query.MoveNext(out var uid, out var other))
        {
            if (uid == sourceUid)
                continue;

            if (other.DepartmentId != source.DepartmentId)
                continue;

            other.Members = source.Members;
            other.SalaryOverrides = source.SalaryOverrides;
            other.DepartmentBudget = source.DepartmentBudget;
            other.DefaultSalary = source.DefaultSalary;
        }
    }

    /// <summary>
    ///     Updates the UI on all consoles that share the same DepartmentId as the given console.
    /// </summary>
    private void UpdateAllUiForDepartment(EntityUid sourceUid, DepartmentConsoleComponent source)
    {
        SyncDepartmentState(sourceUid, source);
        UpdateUiState(sourceUid, source);

        if (source.DepartmentId == null)
            return;

        var query = EntityQueryEnumerator<DepartmentConsoleComponent>();
        while (query.MoveNext(out var uid, out var other))
        {
            if (uid == sourceUid)
                continue;

            if (other.DepartmentId != source.DepartmentId)
                continue;

            UpdateUiState(uid, other);
        }
    }

    /// <summary>
    ///     When a player uses an ID card on the department console, hire that ID card.
    /// </summary>
    private void OnInteractUsing(EntityUid uid, DepartmentConsoleComponent comp, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<IdCardComponent>(args.Used, out var idCard))
            return;

        args.Handled = true;

        // Check if the user has access to operate the console
        if (!_accessReader.IsAllowed(args.User, uid))
        {
            _popup.PopupEntity("Access denied.", uid, args.User);
            return;
        }

        var idCardUid = args.Used;

        // Remove from any other departments first
        var query = EntityQueryEnumerator<DepartmentConsoleComponent>();
        while (query.MoveNext(out var deptUid, out var dept))
        {
            if (dept.Members.Remove(idCardUid))
            {
                dept.SalaryOverrides.Remove(idCardUid);
                UpdateUiState(deptUid, dept);
            }
        }

        // Add to this department
        comp.Members.Add(idCardUid);
        GrantDepartmentAccess(idCardUid, comp);
        UpdateAllUiForDepartment(uid, comp);

        var name = idCard.FullName ?? "Unknown";
        _popup.PopupEntity($"{name} has been hired to {comp.DepartmentName}.", uid, args.User);
    }

    private void OnFire(EntityUid uid, DepartmentConsoleComponent comp, DepartmentConsoleFireBuiMsg msg)
    {
        var idCardUid = GetEntity(msg.IdCardUid);
        comp.Members.Remove(idCardUid);
        comp.SalaryOverrides.Remove(idCardUid);
        UpdateAllUiForDepartment(uid, comp);
    }

    private void OnSetDefaultSalary(EntityUid uid, DepartmentConsoleComponent comp, DepartmentConsoleSetDefaultSalaryBuiMsg msg)
    {
        if (msg.Salary < 0)
            return;

        comp.DefaultSalary = msg.Salary;
        UpdateAllUiForDepartment(uid, comp);
    }

    private void OnSetIndividualSalary(EntityUid uid, DepartmentConsoleComponent comp, DepartmentConsoleSetIndividualSalaryBuiMsg msg)
    {
        var idCardUid = GetEntity(msg.IdCardUid);

        if (!comp.Members.Contains(idCardUid))
            return;

        if (msg.Salary < 0)
            return;

        comp.SalaryOverrides[idCardUid] = msg.Salary;
        UpdateAllUiForDepartment(uid, comp);
    }

    private void OnRemoveOverride(EntityUid uid, DepartmentConsoleComponent comp, DepartmentConsoleRemoveOverrideBuiMsg msg)
    {
        var idCardUid = GetEntity(msg.IdCardUid);
        comp.SalaryOverrides.Remove(idCardUid);
        UpdateAllUiForDepartment(uid, comp);
    }

    private void OnAnnounce(EntityUid uid, DepartmentConsoleComponent comp, DepartmentConsoleAnnounceBuiMsg msg)
    {
        if (string.IsNullOrWhiteSpace(msg.Message))
            return;

        // Build filter for department members only:
        // Find all players whose currently-held ID card is a member of this department.
        var filter = Filter.Empty();
        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is not { } playerUid)
                continue;

            if (!_idCard.TryFindIdCard(playerUid, out var idCard))
                continue;

            if (comp.Members.Contains(idCard.Owner))
                filter.AddPlayer(session);
        }

        var sender = $"{comp.DepartmentName} Dept.";
        var announcementSound = new SoundPathSpecifier("/Audio/Announcements/announce.ogg");
        _chatSystem.DispatchFilteredAnnouncement(filter, msg.Message, uid, sender, true, announcementSound);
    }

    /// <summary>
    ///     Accepts cash inserted into the department console and adds it to the department budget.
    /// </summary>
    private void OnCashInserted(EntityUid uid, DepartmentConsoleComponent comp, EntInsertedIntoContainerMessage args)
    {
        int stackCount = 1;
        if (TryComp<StackComponent>(args.Entity, out var stack))
            stackCount = stack.Count;

        comp.DepartmentBudget += stackCount;
        EntityManager.QueueDeleteEntity(args.Entity);
        UpdateAllUiForDepartment(uid, comp);
    }

    /// <summary>
    ///     Handles ordering an item from the ASRS catalog through the department console.
    ///     Deducts from department budget, queues on the elevator with department metadata.
    ///     When ordering from the corporate ASRS ("corporate" faction), applies the current
    ///     sales tax on top of the base cost and routes the tax revenue to the colony budget.
    /// </summary>
    private void OnOrder(EntityUid uid, DepartmentConsoleComponent comp, DepartmentConsoleOrderBuiMsg msg)
    {
        // Find the ASRS computer for this department's configured faction
        var consoleCoords = _transform.GetMapCoordinates(uid);
        Entity<RequisitionsComputerComponent>? nearestComputer = null;
        var nearestDist = float.MaxValue;

        var computers = EntityQueryEnumerator<RequisitionsComputerComponent>();
        while (computers.MoveNext(out var compUid, out var reqComp))
        {
            if (reqComp.Faction != comp.AsrsFaction)
                continue;

            var compCoords = _transform.GetMapCoordinates(compUid);
            if (consoleCoords.MapId != compCoords.MapId)
                continue;

            var dist = (compCoords.Position - consoleCoords.Position).LengthSquared();
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearestComputer = (compUid, reqComp);
            }
        }

        if (nearestComputer == null)
            return;

        var categories = nearestComputer.Value.Comp.Categories;
        if (msg.CategoryIndex < 0 || msg.CategoryIndex >= categories.Count)
            return;

        var category = categories[msg.CategoryIndex];
        if (msg.EntryIndex < 0 || msg.EntryIndex >= category.Entries.Count)
            return;

        var entry = category.Entries[msg.EntryIndex];

        // Apply sales tax when ordering from the corporate ASRS
        var baseCost = entry.Cost;
        var taxMultiplier = comp.AsrsFaction == "corporate" ? _adminConsole.GetSalesTax() : 0f;
        var effectiveCost = (int) Math.Ceiling(baseCost * (1f + taxMultiplier));

        if (effectiveCost > comp.DepartmentBudget)
            return;

        var elevator = FindColonyElevator(uid, comp.AsrsFaction);
        if (elevator == null)
            return;

        var orderedBy = "Unknown";
        if (_idCard.TryFindIdCard(msg.Actor, out var actorId))
            orderedBy = actorId.Comp.FullName ?? "Unknown";

        var deptOrder = new RequisitionsEntry
        {
            Name = entry.Name,
            Cost = entry.Cost,
            Crate = entry.Crate,
            Entities = new List<EntProtoId>(entry.Entities),
            DeptOrderedBy = orderedBy,
            DeptReason = string.IsNullOrWhiteSpace(msg.Reason) ? "No reason given" : msg.Reason,
            DeptDeliverTo = string.IsNullOrWhiteSpace(msg.DeliverTo) ? "No location specified" : msg.DeliverTo,
            DeptAccessLevel = comp.DepartmentAccessLevel?.Id,
            DeptName = comp.DepartmentName,
        };

        comp.DepartmentBudget -= effectiveCost;

        // Route sales tax revenue to the colony budget
        if (taxMultiplier > 0f)
        {
            var taxRevenue = effectiveCost - baseCost;
            if (taxRevenue > 0)
                _budget.AddToBudget(taxRevenue);
        }

        elevator.Value.Comp.Orders.Add(deptOrder);
        Dirty(elevator.Value);
        UpdateAllUiForDepartment(uid, comp);
    }

    /// <summary>
    ///     Called by BudgetConsoleSystem to dispense salaries from each department's own budget to its members.
    /// </summary>
    public void DispenseSalaries()
    {
        var announcements = new List<string>();
        var processedDepartments = new HashSet<string>();
        var deptQuery = EntityQueryEnumerator<DepartmentConsoleComponent>();
        while (deptQuery.MoveNext(out var deptUid, out var dept))
        {
            // Skip if we already processed a console with this department ID
            // (multiple consoles share the same state)
            if (dept.DepartmentId != null && !processedDepartments.Add(dept.DepartmentId))
                continue;

            // Calculate total salary cost for this department
            var deptCost = 0f;
            foreach (var idCardUid in dept.Members)
            {
                if (!TryComp<IdCardComponent>(idCardUid, out _))
                    continue;

                var salary = dept.SalaryOverrides.TryGetValue(idCardUid, out var overrideSalary)
                    ? overrideSalary
                    : dept.DefaultSalary;
                deptCost += salary;
            }

            // Skip this department if it can't afford its salaries
            if (deptCost > dept.DepartmentBudget)
            {
                announcements.Add($"[bold]{dept.DepartmentName}[/bold]: Insufficient department budget (need ${deptCost:F0}, have ${dept.DepartmentBudget:F0})");
                continue;
            }

            // Dispense salaries from the department's own budget (with income tax deduction)
            var incomeTaxRate = _adminConsole.GetIncomeTax();
            var totalTaxCollected = 0f;
            foreach (var idCardUid in dept.Members)
            {
                if (!TryComp<IdCardComponent>(idCardUid, out var idCard))
                    continue;

                var salary = dept.SalaryOverrides.TryGetValue(idCardUid, out var overrideSalary)
                    ? overrideSalary
                    : dept.DefaultSalary;

                var taxAmount = (int) Math.Floor(salary * incomeTaxRate);
                var netSalary = salary - taxAmount;
                totalTaxCollected += taxAmount;

                // Credit the ID card balance with net salary (after tax)
                idCard.AccountBalance += netSalary;
                Dirty(idCardUid, idCard);
            }

            // Route collected income tax to colony budget
            if (totalTaxCollected > 0)
                _budget.AddToBudget(totalTaxCollected);

            announcements.Add($"[bold]{dept.DepartmentName}[/bold]: ${deptCost:F0} dispensed" +
                (totalTaxCollected > 0 ? $" (${totalTaxCollected:F0} income tax)" : ""));

            dept.DepartmentBudget -= deptCost;
            UpdateAllUiForDepartment(deptUid, dept);
        }


        // Announce salary dispensal colony-wide
        if (announcements.Count > 0)
        {
            var message = Loc.GetString("department-console-salaries-dispensed");
            var sender = Loc.GetString("department-console-salary-announcement-title");
            var announcementSound = new SoundPathSpecifier("/Audio/Announcements/announce.ogg");
            _chatSystem.DispatchGlobalAnnouncement(message, sender, true, announcementSound);
        }
    }

    /// <summary>
    ///     Transfer money from the colony budget to a specific department.
    /// </summary>
    public bool TransferToDepartment(EntityUid deptConsoleUid, float amount)
    {
        if (!TryComp<DepartmentConsoleComponent>(deptConsoleUid, out var dept))
            return false;

        // Cannot transfer colony funds to departments not managed by the colony
        if (!dept.ColonyManaged)
            return false;

        if (amount <= 0 || amount > _budget.GetBudget())
            return false;

        _budget.AddToBudget(-amount);
        dept.DepartmentBudget += amount;
        UpdateAllUiForDepartment(deptConsoleUid, dept);
        return true;
    }

    /// <summary>
    ///     Withdraw cash from the department's own budget, spawning physical cash at the console.
    /// </summary>
    private void OnWithdraw(EntityUid uid, DepartmentConsoleComponent comp, DepartmentConsoleWithdrawBuiMsg msg)
    {
        if (msg.Amount <= 0 || msg.Amount > comp.DepartmentBudget)
            return;

        var amount = (int) msg.Amount;
        comp.DepartmentBudget -= amount;
        _stack.SpawnMultiple("RMCSpaceCash", amount, uid);
        UpdateAllUiForDepartment(uid, comp);
    }

    /// <summary>
    ///     Gets all department consoles for the budget console to list.
    /// </summary>
    public List<(NetEntity Uid, string Name, float Budget)> GetAllDepartments()
    {
        var result = new List<(NetEntity, string, float)>();
        var seenDepartments = new HashSet<string>();
        var query = EntityQueryEnumerator<DepartmentConsoleComponent>();
        while (query.MoveNext(out var uid, out var dept))
        {
            // Skip departments not managed by the colony budget console
            if (!dept.ColonyManaged)
                continue;

            // Only list each department once (first console found wins)
            if (dept.DepartmentId != null && !seenDepartments.Add(dept.DepartmentId))
                continue;

            result.Add((GetNetEntity(uid), dept.DepartmentName, dept.DepartmentBudget));
        }
        return result;
    }
}

