using Content.Shared._RMC14.Vendors;
using Content.Shared.CMU14.Requisitions.StockConsole;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Requisitions.StockConsole;

/// <summary>
/// Fills requisitions stock consoles with what the automated requisitions vendors on the same map have left.
/// </summary>
public sealed class CMURequisitionsStockConsoleSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMURequisitionsStockConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<CMURequisitionsStockConsoleComponent, CMURequisitionsStockConsoleRefreshBuiMsg>(OnRefresh);
    }

    private void OnUiOpened(Entity<CMURequisitionsStockConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnRefresh(Entity<CMURequisitionsStockConsoleComponent> ent, ref CMURequisitionsStockConsoleRefreshBuiMsg args)
    {
        UpdateUi(ent);
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<CMURequisitionsStockConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            if (time < console.NextRefresh || !_ui.IsUiOpen(uid, CMURequisitionsStockConsoleUiKey.Key))
                continue;

            UpdateUi((uid, console));
        }
    }

    private void UpdateUi(Entity<CMURequisitionsStockConsoleComponent> ent)
    {
        ent.Comp.NextRefresh = _timing.CurTime + ent.Comp.RefreshEvery;
        _ui.SetUiState(ent.Owner, CMURequisitionsStockConsoleUiKey.Key,
            new CMURequisitionsStockConsoleBuiState(BuildStock(ent)));
    }

    private List<CMURequisitionsStockVendor> BuildStock(EntityUid console)
    {
        var vendors = new List<CMURequisitionsStockVendor>();
        var map = _transform.GetMapId(console);

        var query = EntityQueryEnumerator<CMURequisitionsVendorComponent, CMAutomatedVendorComponent>();
        while (query.MoveNext(out var uid, out _, out var vendor))
        {
            if (_transform.GetMapId(uid) != map)
                continue;

            var stock = new CMURequisitionsStockVendor { Name = MetaData(uid).EntityName };
            foreach (var section in vendor.Sections)
            {
                var stockSection = new CMURequisitionsStockSection
                {
                    Name = Loc.TryGetString(section.Name, out var sectionName) ? sectionName : section.Name,
                };
                foreach (var entry in section.Entries)
                {
                    stockSection.Items.Add(new CMURequisitionsStockItem
                    {
                        Id = entry.Id.Id,
                        Name = GetEntryName(entry),
                        Amount = entry.Amount,
                        Max = entry.Max,
                    });
                }

                if (stockSection.Items.Count > 0)
                    stock.Sections.Add(stockSection);
            }

            vendors.Add(stock);
        }

        vendors.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return vendors;
    }

    private string GetEntryName(CMVendorEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Name))
            return entry.Name.Replace("\\n", "\n");

        return _prototypes.TryIndex(entry.Id, out var proto) ? proto.Name : entry.Id.Id;
    }
}
