using Content.Shared.AU14;
using Robust.Shared.Prototypes;
using System.Collections.Generic;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.Roles;
using Robust.Shared.Utility;

namespace Content.Shared.AU14.util;
    [Prototype]
    public sealed partial class PlatoonPrototype : IPrototype
    {
        [IdDataField]
        public string ID { get; private set; } = default!;

        [DataField("factions", required: false)]
        public List<string> Factions { get; private set; } = new();

        [DataField("language", required: false)]
        public string Language { get; private set; } = string.Empty;
        [DataField("name", required: true)]
        public string Name { get; private set; } = string.Empty;


        [DataField("reqlist", required: false)]
        public string Reqlist { get; private set; } = string.Empty;

        [DataField("VendorToMarker")]
        public Dictionary<PlatoonMarkerClass, ProtoId<EntityPrototype>> VendorMarkersByClass { get; private set; } = new();

        [DataField("logilist")]
        public RequisitionsComputerComponent Logilist { get; private set; } = default!;

        [DataField("possibleships")]
        public List<string> PossibleShips { get; private set; } = new();

        [DataField("jobClassOverride")]
        public Dictionary<PlatoonJobClass, string> JobClassOverride { get; private set; } = new();
            [DataField("PlatoonFlag")]
        public string PlatoonFlag { get; private set; } = string.Empty;
        //used for capture objectives and deco, spritestate
        [DataField("jobSlotOverride")]
        public Dictionary<PlatoonJobClass, int> JobSlotOverride { get; private set; } = new();

        [DataField("CompatibleDropships")]
        public List<ResPath> CompatibleDropships { get; private set; } = new();

        [DataField("compatibleFighters")]
        public List<ResPath> CompatibleFighters { get; private set; } = new();


    }
