using Content.Server.AU14;
using Content.Server.Maps;
using Content.Shared._RMC14.Rules;
using Content.Shared.AU14;
using Content.Shared.AU14.util;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

namespace Content.Server.GameTicking.Presets
{
    /// <summary>
    ///     A round-start setup preset, such as which antagonists to spawn.
    /// </summary>
    [Prototype]
    public sealed partial class GamePresetPrototype : IPrototype
    {
        [IdDataField]
        public string ID { get; private set; } = default!;

        [DataField("alias")]
        public string[] Alias = Array.Empty<string>();

        [DataField("name")]
        public string ModeTitle = "????";

        [DataField("description")]
        public string Description = string.Empty;

        [DataField("showInVote")]
        public bool ShowInVote;

        [DataField("requiresGovforVote")]
        public bool RequiresGovforVote;

        [DataField("requiresOpforVote")]
        public bool RequiresOpforVote;

        [DataField("minPlayers")]
        public int? MinPlayers;

        [DataField("maxPlayers")]
        public int? MaxPlayers;

        /// <summary>Whether hives can gain or spawn burrowed larva during this preset.</summary>
        [DataField("burrowedLarvaEnabled")]
        public bool BurrowedLarvaEnabled = true; // CMU14

        /// <summary>
        /// Whether this preset starts the automatic third-party queue without requiring a selected threat.
        /// </summary>
        [DataField("thirdPartyAutoSpawn")]
        public bool ThirdPartyAutoSpawn;

        /// <summary>
        /// Seconds between automatic third-party spawn attempts for preset-owned scheduling.
        /// </summary>
        [DataField("thirdPartyInterval")]
        public int ThirdPartyInterval = 14000;

        /// <summary>
        /// Maximum fraction of the current population used to preselect third-party bodies.
        /// </summary>
        [DataField("thirdPartyRatio")]
        public float ThirdPartyRatio = 0.15f;

        /// <summary>
        /// Maximum number of third parties selected for the preset-owned queue.
        /// </summary>
        [DataField("maxThirdParties")]
        public int MaxThirdParties = 7;

        [DataField("rules", customTypeSerializer: typeof(PrototypeIdListSerializer<EntityPrototype>))]
        public IReadOnlyList<string> Rules { get; private set; } = Array.Empty<string>();

        /// <summary>
        /// If specified, the gamemode will only be run with these maps.
        /// If none are elligible, the global fallback will be used.
        /// </summary>
        [DataField("supportedMaps", customTypeSerializer: typeof(PrototypeIdSerializer<GameMapPoolPrototype>))]
        public string? MapPool;

        /// <summary>
        /// If specified, only these planets (by prototype id, e.g. AUPlanetLV747) can be voted for this preset.
        /// </summary>
        [DataField("supportedPlanets")]
        public List<string>? SupportedPlanets;

        /// <summary>
        /// If specified, use this planet pool prototype for planet voting.
        /// </summary>
        [DataField("planetPool", customTypeSerializer: typeof(PrototypeIdSerializer<GamePlanetPoolPrototype>))]
        public string? PlanetPool;



    }
}
