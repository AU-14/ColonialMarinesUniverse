using System.Numerics;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.Fluids;
using Content.Shared.Forensics.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// Cleaning a vehicle's dirt, blood and crayon off, and splashing blood onto it. A space cleaner
/// spray or splash takes off a share for each unit; scrubbing it down with soap or a wet mop takes
/// all of it.
/// </summary>
public sealed class CMUVehicleGrimeSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private RMCReagentSystem _reagents = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;

    private const int Splats = 5;
    private const int Drips = 3;

    // A top face's decal axes (right, down) for each direction it can be drawn from.
    private static readonly (Vector3 Right, Vector3 Down)[] TopAxes =
    [
        (new Vector3(0, -1, 0), new Vector3(1, 0, 0)),
        (new Vector3(0, 1, 0), new Vector3(-1, 0, 0)),
        (new Vector3(1, 0, 0), new Vector3(0, 1, 0)),
        (new Vector3(-1, 0, 0), new Vector3(0, -1, 0)),
    ];

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleGrimeComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CMUVehicleGrimeComponent, CMUVehicleScrubDoAfterEvent>(OnScrub);
        SubscribeLocalEvent<CMUVehicleGrimeComponent, ReactionEntityEvent>(OnReaction);
    }

    private void OnInteractUsing(Entity<CMUVehicleGrimeComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !IsScrubber(args.Used))
            return;

        args.Handled = true;
        if (!IsGrimy(ent))
        {
            _popup.PopupClient(Loc.GetString("cmu-vehicle-grime-clean"), ent, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.ScrubDelay, new CMUVehicleScrubDoAfterEvent(), ent, ent, args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget,
        });
    }

    private void OnScrub(Entity<CMUVehicleGrimeComponent> ent, ref CMUVehicleScrubDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        Clean(ent, 1f);
        _popup.PopupClient(Loc.GetString("cmu-vehicle-grime-scrubbed", ("vehicle", ent.Owner)), ent, args.User);
    }

    private void OnReaction(Entity<CMUVehicleGrimeComponent> ent, ref ReactionEntityEvent args)
    {
        if (args.Method != ReactionMethod.Touch || !args.Reagent.CleansItemStains)
            return;

        Clean(ent, args.ReagentQuantity.Quantity.Float() * ent.Comp.CleanPerUnit);
    }

    /// <summary>
    /// Soap, or a mop wet with something that cleans.
    /// </summary>
    private bool IsScrubber(EntityUid used)
    {
        if (HasComp<CleansForensicsComponent>(used))
            return true;

        if (!TryComp(used, out AbsorbentComponent? absorbent) ||
            !_solutions.TryGetSolution(used, absorbent.SolutionName, out _, out var solution))
        {
            return false;
        }

        foreach (var reagent in solution.Contents)
        {
            if (reagent.Quantity >= 1 &&
                _reagents.TryIndex(reagent.Reagent, out var proto) &&
                proto.CleansItemStains)
            {
                return true;
            }
        }

        return false;
    }

    public bool IsGrimy(Entity<CMUVehicleGrimeComponent> ent)
    {
        return ent.Comp.Dirt > 0f ||
               TryComp(ent, out CMUVehicleCrayonComponent? crayon) && (crayon.Drawings.Count > 0 || crayon.Blood.Count > 0);
    }

    /// <summary>
    /// Takes off a share (0 to 1) of the dirt, and of the blood and crayon, oldest first. Server only.
    /// </summary>
    public void Clean(Entity<CMUVehicleGrimeComponent> ent, float share)
    {
        if (_net.IsClient || share <= 0f)
            return;

        share = MathF.Min(1f, share);
        ent.Comp.RawDirt = MathF.Max(0f, ent.Comp.RawDirt - share);
        SetDirt(ent, ent.Comp.RawDirt);

        if (!TryComp(ent, out CMUVehicleCrayonComponent? crayon))
            return;

        var blood = (int) MathF.Ceiling(crayon.Blood.Count * share);
        var drawings = (int) MathF.Ceiling(crayon.Drawings.Count * share);
        if (blood + drawings == 0)
            return;

        crayon.Blood.RemoveRange(0, blood);
        crayon.Drawings.RemoveRange(0, drawings);
        Dirty(ent, crayon);
    }

    /// <summary>
    /// Sets the dirt, rounded down to a step; only a new step is sent to clients.
    /// </summary>
    public void SetDirt(Entity<CMUVehicleGrimeComponent> ent, float raw)
    {
        ent.Comp.RawDirt = Math.Clamp(raw, 0f, 1f);
        var stepped = MathF.Floor(ent.Comp.RawDirt * ent.Comp.DirtSteps) / ent.Comp.DirtSteps;
        if (MathF.Abs(stepped - ent.Comp.Dirt) < 0.001f)
            return;

        ent.Comp.Dirt = stepped;
        Dirty(ent);
    }

    /// <summary>
    /// Splashes blood on a point of the body (voxel coordinates: forward, right, up) on a top face
    /// ("up") or the front ("+f"). Splats are big, drips small. Server only.
    /// </summary>
    public void AddBlood(EntityUid vehicle, Color color, Vector3 anchor, string normal, bool splat)
    {
        if (_net.IsClient || !TryComp(vehicle, out CMUVehicleCrayonComponent? crayon))
            return;

        var (right, down) = normal == "up"
            ? _random.Pick(TopAxes)
            : (new Vector3(0, -1, 0), new Vector3(0, 0, -1));
        var decal = splat ? $"CMUJeepBloodSplat{_random.Next(1, Splats + 1)}" : $"CMUJeepBloodDrip{_random.Next(1, Drips + 1)}";
        crayon.Blood.Add(new CMUCrayonDrawing
        {
            Decal = decal,
            Color = color,
            Anchor = anchor,
            Normal = normal,
            Right = right,
            Down = down,
        });

        if (crayon.Blood.Count > crayon.MaxBlood)
            crayon.Blood.RemoveAt(0);

        Dirty(vehicle, crayon);
    }
}
