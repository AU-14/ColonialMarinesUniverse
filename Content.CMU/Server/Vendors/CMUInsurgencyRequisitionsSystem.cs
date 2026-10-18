using Content.Server._RMC14.Vendors;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared._RMC14.NightVision;
using Content.Shared._RMC14.Scoping;
using Content.Shared._RMC14.Vendors;
using Content.Shared.CMU14.Threats;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Vendors;

public sealed partial class CMUInsurgencyRequisitionsSystem : GameRuleSystem<InsurgencyRuleComponent>
{
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUInsurgencyRequisitionsComponent, MapInitEvent>(OnMapInit,
            after: [typeof(CMAutomatedVendorSystem)]);
    }

    protected override void Started(EntityUid uid, InsurgencyRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        UpdateVendors();
    }

    protected override void Ended(EntityUid uid, InsurgencyRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        UpdateVendors();
    }

    private bool IsInsurgencyActive()
    {
        var rules = EntityQueryEnumerator<InsurgencyRuleComponent, ActiveGameRuleComponent>();
        return rules.MoveNext(out _, out _, out _);
    }

    private void OnMapInit(Entity<CMUInsurgencyRequisitionsComponent> ent, ref MapInitEvent args)
    {
        if (TryComp<CMAutomatedVendorComponent>(ent, out var vendor))
            UpdateVendor(ent, vendor, IsInsurgencyActive());
    }

    private void UpdateVendors()
    {
        var active = IsInsurgencyActive();
        var vendors = EntityQueryEnumerator<CMUInsurgencyRequisitionsComponent, CMAutomatedVendorComponent>();
        while (vendors.MoveNext(out var uid, out var restriction, out var vendor))
            UpdateVendor((uid, restriction), vendor, active);
    }

    private void UpdateVendor(Entity<CMUInsurgencyRequisitionsComponent> ent, CMAutomatedVendorComponent vendor, bool active)
    {
        var changed = false;
        if (active)
        {
            foreach (var section in vendor.Sections)
            {
                for (var i = section.Entries.Count - 1; i >= 0; i--)
                {
                    var entry = section.Entries[i];
                    var prototype = _prototypes.Index(entry.Id);
                    if (!prototype.TryComp<ScopeComponent>(out var scope, _factory)
                        || !scope.Attachment
                        || !prototype.TryComp<NightVisionItemComponent>(out _, _factory))
                        continue;

                    ent.Comp.HiddenScopes.Add((section, i, entry));
                    section.Entries.RemoveAt(i);
                    changed = true;
                }
            }
        }
        else
        {
            // Entries were removed back-to-front; restore in the opposite order to preserve indices.
            for (var i = ent.Comp.HiddenScopes.Count - 1; i >= 0; i--)
            {
                var (section, index, entry) = ent.Comp.HiddenScopes[i];
                section.Entries.Insert(index, entry);
                changed = true;
            }

            ent.Comp.HiddenScopes.Clear();
        }

        if (changed)
            Dirty(ent.Owner, vendor);
    }
}
