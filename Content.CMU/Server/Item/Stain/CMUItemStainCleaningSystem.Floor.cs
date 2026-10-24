using System;
using System.Numerics;
using Content.Shared.CMU14.Hygiene;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids;
using Content.Shared.Fluids.Components;
using Content.Shared.Interaction;
using Content.Shared.Timing;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.Item.Stain;

public sealed partial class CMUItemStainCleaningSystem
{
    [Dependency] private SharedPuddleSystem _floorPuddles = default!;
    [Dependency] private SharedTransformSystem _floorTransform = default!;
    [Dependency] private SharedMapSystem _floorMap = default!;
    [Dependency] private SharedInteractionSystem _floorInteraction = default!;
    [Dependency] private SharedAudioSystem _floorAudio = default!;
    [Dependency] private SharedMeleeWeaponSystem _floorMelee = default!;
    [Dependency] private UseDelaySystem _floorUseDelay = default!;

    private const string FloorDelayId = "cmu-soap-floor";
    private static readonly SoundSpecifier FloorSound = new SoundPathSpecifier("/Audio/CMU14/Hygiene/soap_scrub.ogg")
    {
        Params = AudioParams.Default.WithVolume(-6f),
    };

    private void OnSoapFloorInteract(Entity<CMUFourUseSoapComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;
        args.Handled = TryCleanSoapFloor(ent, args.User, args.Target, args.ClickLocation);
    }

    private void OnSoapFloorActivate(Entity<CMUFourUseSoapComponent> ent, ref UserActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;
        args.Handled = TryCleanSoapFloor(ent, args.User, args.Target, Transform(args.Target).Coordinates);
    }

    private bool TryCleanSoapFloor(Entity<CMUFourUseSoapComponent> ent, EntityUid user,
        EntityUid? target, EntityCoordinates coordinates)
    {
        // Leave people, clothing, and other objects to the existing forensic/body cleaning interaction.
        if (ent.Comp.UsesRemaining <= 0 ||
            target is { } clicked && !HasComp<PuddleComponent>(clicked))
            return false;
        if (target is { } puddleTarget)
            coordinates = Transform(puddleTarget).Coordinates;
        if (!_floorInteraction.InRangeUnobstructed(user, coordinates))
            return false;
        if (_floorUseDelay.IsDelayed(ent.Owner, FloorDelayId))
            return true;

        var tile = default(TileRef);
        var grid = _floorTransform.GetGrid(coordinates);
        var hasTile = TryComp<MapGridComponent>(grid, out var mapGrid) &&
                      _floorMap.TryGetTileRef(grid!.Value, mapGrid, coordinates, out tile);
        if (target == null && hasTile && _floorPuddles.TryGetPuddle(tile, out var found))
            target = found;

        var cleaned = false;
        if (target is { } puddle && !TerminatingOrDeleted(puddle) &&
            TryComp<PuddleComponent>(puddle, out var puddleComp) &&
            _solutions.TryGetSolution(puddle, puddleComp.SolutionName, out var solution, out var contents))
        {
            // The mop's normal 120u pickup, without adding water or storing hazardous liquid in edible soap.
            var amount = FixedPoint2.Min(contents.Volume, ent.Comp.FloorPickupAmount);
            if (amount > FixedPoint2.Zero)
            {
                _solutions.SplitSolution(solution.Value, amount);
                cleaned = true;
                if (contents.Volume == FixedPoint2.Zero)
                    QueueDel(puddle);
            }
        }
        if (hasTile)
            cleaned |= _floorPuddles.CleanDecalsAt(tile);
        if (!cleaned)
            return false;

        Spawn("PuddleSparkle", coordinates);
        _floorAudio.PlayPvs(FloorSound, coordinates);
        var userXform = Transform(user);
        var targetPosition = _floorTransform.ToMapCoordinates(coordinates).Position;
        var localPosition = Vector2.Transform(targetPosition, _floorTransform.GetInvWorldMatrix(userXform));
        localPosition = userXform.LocalRotation.RotateVec(localPosition);
        _floorMelee.DoLunge(user, ent.Owner, Angle.Zero, localPosition, null);
        _floorUseDelay.SetLength(ent.Owner, TimeSpan.FromSeconds(1), FloorDelayId);
        _floorUseDelay.TryResetDelay((ent.Owner, Comp<UseDelayComponent>(ent.Owner)), id: FloorDelayId);
        _popup.PopupEntity(Loc.GetString("cmu-soap-floor-cleaned"), ent, user);
        ConsumeSoapUse(ent, user);
        return true;
    }
}
