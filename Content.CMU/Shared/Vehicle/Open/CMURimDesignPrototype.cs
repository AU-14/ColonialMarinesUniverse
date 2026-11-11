using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// A rim design the vehicle spray painter can put on a vehicle's wheels. The client draws it over the
/// rims from the rim maps its art keeps beside each wheel state (rim.swsl). A rim is 11 pixels
/// across: a lip round its edge, a cap in its middle, and the design's pattern between them. Each
/// part takes one of four colours: the wheels' own paint (the base), an accent, the lip's and the
/// cap's. A design that leaves a colour out draws that part in a shade of the base instead.
/// </summary>
[Prototype("cmuRimDesign")]
public sealed partial class CMURimDesignPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// Where it sits in the painter's list.
    /// </summary>
    [DataField]
    public int Order;

    /// <summary>
    /// The plain rims the wheels came with: fitting it takes a design off.
    /// </summary>
    [DataField]
    public bool Factory;

    [DataField]
    public CMURimPattern Pattern = CMURimPattern.Dish;

    /// <summary>
    /// Spokes, holes or slots round the rim. It is also how many times the pattern repeats round it,
    /// which sets how far it turns a frame: a quarter of one repeat, so the four frame loop is seamless.
    /// </summary>
    [DataField]
    public int Count = 4;

    /// <summary>
    /// How wide a spoke or slot is, as a share of its slice of the rim.
    /// </summary>
    [DataField]
    public float Width = 0.4f;

    /// <summary>
    /// How far a spoke curls on its way out, in slices, for turbines and meshes.
    /// </summary>
    [DataField]
    public float Twist;

    /// <summary>
    /// The radius holes and slots sit at, and how big they are, in pixels.
    /// </summary>
    [DataField]
    public float HoleRadius = 3.4f;

    [DataField]
    public float HoleSize = 0.9f;

    /// <summary>
    /// The cap's radius and where the lip starts, in pixels from the hub.
    /// </summary>
    [DataField]
    public float Cap = 1.5f;

    [DataField]
    public float Lip = 4.9f;

    /// <summary>
    /// Lug nuts round the cap, in its colour's shadow. A multiple of <see cref="Count"/>, so they loop.
    /// </summary>
    [DataField]
    public int Nuts;

    [DataField]
    public float NutRadius = 3f;

    /// <summary>
    /// Bolts round the lip, in the cap's colour, like a beadlock ring. A multiple of <see cref="Count"/>.
    /// </summary>
    [DataField]
    public int LipBolts;

    /// <summary>
    /// The colours it has besides the base, which the painter offers to pick.
    /// </summary>
    [DataField]
    public List<CMURimRegion> Regions = new() { CMURimRegion.Accent, CMURimRegion.Lip, CMURimRegion.Cap };

    /// <summary>
    /// The accent, lip and cap colours it comes in.
    /// </summary>
    [DataField]
    public Color Accent = Color.FromHex("#C8CDD2");

    [DataField]
    public Color LipColor = Color.FromHex("#3C4046");

    [DataField]
    public Color CapColor = Color.FromHex("#E6E9EC");
}

[Serializable, NetSerializable]
public enum CMURimPattern : byte
{
    /// <summary>A plain dish.</summary>
    Dish,

    /// <summary>Spokes in the accent colour over open gaps.</summary>
    Spokes,

    /// <summary>A dish with round holes.</summary>
    Holes,

    /// <summary>A dish with slots along the rim.</summary>
    Slots,

    /// <summary>Spokes curling both ways, crossing.</summary>
    Mesh,

    /// <summary>A hubcap: a disc in the accent colour.</summary>
    Disc,
}

[Serializable, NetSerializable]
public enum CMURimRegion : byte
{
    Base,
    Accent,
    Lip,
    Cap,
}
