using Content.Server.Maps;
using Robust.Shared.Prototypes;

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

        [DataField]
        public string[] Alias = Array.Empty<string>();

        [DataField("name")]
        public string ModeTitle = "????";

        [DataField]
        public string Description = string.Empty;

        [DataField]
        public bool ShowInVote;

        [DataField]
        public bool RequiresGovforVote;

        [DataField]
        public bool RequiresOpforVote;

        [DataField]
        public int? MinPlayers;

        [DataField]
        public int? MaxPlayers;

        /// <summary>Whether hives can gain or spawn burrowed larva during this preset.</summary>
        [DataField]
        public bool BurrowedLarvaEnabled = true; // CMU14

        /// <summary>
        /// Whether this preset starts the automatic third-party queue without requiring a selected threat.
        /// </summary>
        [DataField]
        public bool ThirdPartyAutoSpawn;

        /// <summary>
        /// Seconds between automatic third-party spawn attempts for preset-owned scheduling.
        /// </summary>
        [DataField]
        public int ThirdPartyInterval = 14000;

        /// <summary>
        /// Maximum fraction of the current population used to preselect third-party bodies.
        /// </summary>
        [DataField]
        public float ThirdPartyRatio = 0.15f;

        /// <summary>
        /// Maximum number of third parties selected for the preset-owned queue.
        /// </summary>
        [DataField]
        public int MaxThirdParties = 7;

        [DataField]
        public IReadOnlyList<EntProtoId> Rules { get; private set; } = Array.Empty<EntProtoId>();

        /// <summary>
        /// If specified, the gamemode will only be run with these maps.
        /// If none are elligible, the global fallback will be used.
        /// </summary>
        [DataField("supportedMaps")]
        public ProtoId<GameMapPoolPrototype>? MapPool;

        /// <summary>
        /// If specified, only these planets (by prototype id, e.g. AUPlanetLV747) can be voted for this preset.
        /// </summary>
        [DataField]
        public List<string>? SupportedPlanets;

        /// <summary>
        /// If specified, use this planet pool prototype for planet voting.
        /// </summary>
        [DataField]
        public string? PlanetPool;
    }
}
