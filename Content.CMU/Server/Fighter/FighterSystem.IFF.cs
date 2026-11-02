using Content.Server._RMC14.Vehicle;
using Content.Shared._RMC14.Vehicle.Supply;
using Content.Shared.CMU14.Fighter;
using Content.Shared.Examine;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Fighter;

public sealed partial class FighterSystem
{
    [Dependency] private FighterIFFSystem _iff = default!;

    private void InitializeIFF()
    {
        SubscribeLocalEvent<FighterIFFComponent, MapInitEvent>(OnIFFInit);
        SubscribeLocalEvent<FighterIFFComponent, VehicleSupplyDeliveredEvent>(OnSupplyDelivered);
        SubscribeLocalEvent<FighterIFFComponent, ExaminedEvent>(OnIFFExamined);
        SubscribeLocalEvent<FighterIFFComponent, VehicleSupplyVendedEvent>(OnFighterEquipmentVended);
    }

    private void OnIFFInit(Entity<FighterIFFComponent> ent, ref MapInitEvent args) =>
        SetEquipmentFaction(ent, ent.Comp.Faction ?? _iff.GetSiteFaction(ent));

    private void OnSupplyDelivered(Entity<FighterIFFComponent> ent, ref VehicleSupplyDeliveredEvent args) =>
        SetEquipmentFaction(ent, ent.Comp.Faction ?? _iff.GetSiteFaction(args.Lift) ??
            (args.Requester is { } user ? _iff.GetOperatorFaction(user) : null));

    private void OnFighterEquipmentVended(Entity<FighterIFFComponent> ent, ref VehicleSupplyVendedEvent args) =>
        SetEquipmentFaction(ent, ent.Comp.Faction ?? _iff.GetOperatorFaction(args.User));

    private void SetEquipmentFaction(Entity<FighterIFFComponent> ent, string? faction)
    {
        ent.Comp.Faction = FighterIFFSystem.Normalize(faction);
        Dirty(ent);
        if (TryComp(ent, out FighterGroundComponent? ground) &&
            TryComp(ground.Aircraft, out FighterWeaponsComponent? weapons))
        {
            weapons.Faction = ent.Comp.Faction;
            Dirty(ground.Aircraft!.Value, weapons);
        }
    }

    private void OnIFFExamined(Entity<FighterIFFComponent> ent, ref ExaminedEvent args) =>
        args.PushMarkup(Loc.GetString(ent.Comp.Faction == null ? "cmu-fighter-iff-unassigned" : "cmu-fighter-iff-owner",
            ("faction", ent.Comp.Faction ?? string.Empty)));

    private bool CanBoardAircraft(EntityUid user, Entity<FighterAircraftComponent> aircraft)
    {
        // A mapped ship may acquire its side after its contents received MapInit.
        if (aircraft.Comp.GroundEntity is { } hull && TryComp(hull, out FighterIFFComponent? iff) && iff.Faction == null)
            SetEquipmentFaction((hull, iff), _iff.GetSiteFaction(hull));
        if (!TryComp(aircraft, out FighterWeaponsComponent? weapons)) return false;
        // A supply-delivered airframe is already owned; an unmapped test airframe can be claimed by its first crew.
        return FighterIFFSystem.Normalize(weapons.Faction) == null ||
               FighterIFFSystem.Same(weapons.Faction, _iff.GetOperatorFaction(user));
    }

    private string? GetHullFaction(EntityUid hull) =>
        TryComp(hull, out FighterIFFComponent? iff) ? iff.Faction : null;

    private bool CanUseGroundSite(EntityUid hull, EntityCoordinates coordinates)
    {
        var faction = GetHullFaction(hull);
        if (_iff.GetSiteFaction(coordinates.EntityId) is { } owner && !FighterIFFSystem.Same(faction, owner))
            return false;
        return true;
    }
}
