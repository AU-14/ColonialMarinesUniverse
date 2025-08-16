using Content.Shared.AU14;
using Robust.Shared.Prototypes;
using System.Collections.Generic;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.Roles;

namespace Content.Shared.AU14.util;
    [Prototype]
    public sealed partial class PlatoonPrototype : IPrototype
    {
        [IdDataField]
        public string ID { get; private set; } = default!;

        [DataField("language", required: false)]
        public string Language { get; private set; } = string.Empty;
        [DataField("name", required: true)]
        public string Name { get; private set; } = string.Empty;


        [DataField("reqList", required: false)]
        public string Req { get; private set; } = string.Empty;
//Curretly reqlist and languge are unused but putting here for future - eg

        [DataField("VendorToMarker")]
        public Dictionary<PlatoonMarkerClass, ProtoId<EntityPrototype>> VendorMarkersByClass { get; private set; } = new();

       // [DataField("ship")]
        //public ProtoId<GameMapPrototype>? GameMap;

      //  [DataField("language")]
       // public ProtoId<LanguagePrototype> Language { get; private set; } = default!;

        [DataField("logilist")]
        public RequisitionsComputerComponent Logilist { get; private set; } = default!;

        [DataField("possibleships")]
        public List<string> PossibleShips { get; private set; } = new();

        [DataField("jobClassOverride")]
        public Dictionary<PlatoonJobClass, string> JobClassOverride { get; private set; } = new();

        [DataField("jobSlotOverride")]
        public Dictionary<PlatoonJobClass, int> JobSlotOverride { get; private set; } = new();
    }
