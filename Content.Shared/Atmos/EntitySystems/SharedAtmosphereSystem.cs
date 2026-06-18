using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Prototypes;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;

namespace Content.Shared.Atmos.EntitySystems
{
    public abstract partial class SharedAtmosphereSystem : EntitySystem
    {
        // CMU14 start
        private const string CMUAnesthesiaSawmillName = "cmu.medical.anesthesia";
        // CMU14 end

        // CMU14 start
        [Dependency] private ILogManager _log = default!;
        // CMU14 end
        [Dependency] private IPrototypeManager _prototypeManager = default!;
        [Dependency] private SharedInternalsSystem _internals = default!;

        private EntityQuery<InternalsComponent> _internalsQuery;

        // CMU14 start
        private ISawmill _anesthesiaSawmill = default!;
        // CMU14 end

        protected readonly GasPrototype[] GasPrototypes = new GasPrototype[Atmospherics.TotalNumberOfGases];

        public override void Initialize()
        {
            base.Initialize();

            // CMU14 start
            _anesthesiaSawmill = _log.GetSawmill(CMUAnesthesiaSawmillName);
            // CMU14 end

            _internalsQuery = GetEntityQuery<InternalsComponent>();

            InitializeBreathTool();

            for (var i = 0; i < Atmospherics.TotalNumberOfGases; i++)
            {
                GasPrototypes[i] = _prototypeManager.Index<GasPrototype>(i.ToString());
            }
        }

        public GasPrototype GetGas(int gasId) => GasPrototypes[gasId];

        public GasPrototype GetGas(Gas gasId) => GasPrototypes[(int) gasId];

        public IEnumerable<GasPrototype> Gases => GasPrototypes;
    }
}
