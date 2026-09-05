using Robust.Shared.Prototypes;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Content.Shared.CMU14.util
{
    [Prototype("GamePlanetPool"),PublicAPI]
    public sealed partial class GamePlanetPoolPrototype : IPrototype
    {
        [IdDataField]
        public string ID { get; private set; } = string.Empty;

        [DataField("planets")]
        public List<string> Planets { get; private set; } = new();

        /// <summary>
        /// Resolves a preset's votable planet ids: a preset-level pool wins,
        /// otherwise pool ids inlined in supportedPlanets are spliced in.
        /// </summary>
        public static List<string> ExpandPlanetIds(
            IPrototypeManager prototypes,
            string? planetPool,
            List<string>? supportedPlanets)
        {
            if (!string.IsNullOrWhiteSpace(planetPool)
                && prototypes.TryIndex<GamePlanetPoolPrototype>(planetPool, out var pool))
                return pool.Planets;

            if (supportedPlanets is not { Count: > 0 })
                return [];

            var expanded = new List<string>(supportedPlanets.Count);
            foreach (var id in supportedPlanets)
            {
                if (prototypes.TryIndex<GamePlanetPoolPrototype>(id, out var inline))
                    expanded.AddRange(inline.Planets);
                else
                    expanded.Add(id);
            }

            return expanded;
        }
    }
}

