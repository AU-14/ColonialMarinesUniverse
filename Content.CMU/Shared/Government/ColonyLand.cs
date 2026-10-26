// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Government;

// The land of the city: what the administrator marks out for building (the zones) and whom he gives a plot to. The tables are here; what is done
// with them is in Server/Government.

[Serializable, NetSerializable]
public enum ZoneKind : byte
{
    Housing,
    Trade,
    Production,
}

// A kind of zone: what a plot in it costs to hold. The rent is taken from the bank account of the holder every cycle, with the land tax on top.
[Prototype("colonyZoneKind")]
public sealed partial class ColonyZoneKindPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public ZoneKind Kind;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    [DataField]
    public int RentPerCycle = 60;
}

// The marks that the surveyor's tablet puts: the administrator stands where the middle of the zone is, picks the size, and marks. (A plot is five tiles on a side, so the sizes are the ones that cut into plots.)
// The tablet carries the zones that are marked already, so the client of the one who holds it can draw them (and the square of the next mark) over the ground.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurveyorTabletComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Size = 10;

    [AutoNetworkedField]
    public List<SurveyorMark> Marks = new();

    // No more zones can be marked: the square of the next mark is red.
    [AutoNetworkedField]
    public bool Full;
}

// A zone as the tablet shows it: the grid, the corner of the lowest coordinates (in the tiles of the grid), the side and the kind.
[Serializable, NetSerializable]
public readonly record struct SurveyorMark(NetEntity Grid, Vector2i Min, int Size, ZoneKind Kind);

