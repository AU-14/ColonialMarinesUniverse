using Robust.Shared.Prototypes;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Content.Shared.AU14.util
{
    [Prototype("GamePlanetPool"),PublicAPI]
    public sealed partial class GamePlanetPoolPrototype : IPrototype
    {
        [IdDataField]
        public string ID { get; private set; } = string.Empty;

        [DataField("planets")]
        public List<string> Planets { get; private set; } = new();
    }
}

