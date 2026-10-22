using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// A vehicle's field manual: the vehicle drawn from its own art, with each part a reader can click to
/// read up on, and pages that aren't one part. The art is the vehicle's sprite states laid over each
/// other, the overlay drawn over its crew last, lifted as the overlay entity is; the click areas are
/// the vehicle's own click maps.
/// </summary>
[Prototype("cmuVehicleManual")]
public sealed partial class CMUVehicleManualPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Title;

    [DataField(required: true)]
    public ResPath Sprite;

    /// <summary>
    /// The states drawn under the crew, bottom first.
    /// </summary>
    [DataField]
    public List<string> Layers = new();

    /// <summary>
    /// The states drawn over the crew, bottom first.
    /// </summary>
    [DataField]
    public List<string> OverlayLayers = new();

    /// <summary>
    /// How far up the overlay is drawn in each facing, in pixels: the overlay entity's
    /// direction offsets.
    /// </summary>
    [DataField]
    public Dictionary<Direction, int> OverlayLift = new();

    /// <summary>
    /// The states swapped in with the panels shown open.
    /// </summary>
    [DataField]
    public Dictionary<string, string> OpenStates = new();

    /// <summary>
    /// The parts and pages, in the order the index lists them.
    /// </summary>
    [DataField(required: true)]
    public List<CMUVehicleManualEntry> Entries = new();
}

/// <summary>
/// One page of a vehicle manual. A part has the click maps it covers on the art; a page without any
/// is listed in the index alone.
/// </summary>
[DataDefinition]
public sealed partial class CMUVehicleManualEntry
{
    [DataField(required: true)]
    public string Id = string.Empty;

    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// The click maps that pick this part, with its panels shut.
    /// </summary>
    [DataField]
    public List<string> Masks = new();

    /// <summary>
    /// The click maps that pick it with its panels open; its shut ones when left out.
    /// </summary>
    [DataField]
    public List<string>? OpenMasks;

    /// <summary>
    /// Drawn out in the warning colour, for the parts a reader most needs to find.
    /// </summary>
    [DataField]
    public bool Highlight;

    /// <summary>
    /// What it is and what it does.
    /// </summary>
    [DataField(required: true)]
    public LocId About;

    [DataField]
    public LocId? Use;

    [DataField]
    public LocId? Damage;

    [DataField]
    public LocId? Repair;

    [DataField]
    public LocId? Replace;
}
