using System.Diagnostics.CodeAnalysis;
using Content.Shared.CMU14.Chemistry.Reagents;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Synthesizer;

/// <summary>
/// N6 (GraphAudit-ChemBotanyPlan): a product → reaction lookup. The upstream reaction cache is indexed by reactant
/// only, so the chemical synthesizer (and anything else that needs "how is X made?") uses this index instead.
/// <para>
/// Breakdown reactions (<see cref="ReactionPrototype.Source"/>) and the generated <c>{ID}Ignition</c> reactions are
/// not recipes and are skipped. Each list is sorted in the order the reaction engine runs reactions
/// (<see cref="ReactionPrototype.CompareTo"/>). The index is rebuilt lazily after any reaction prototype reload, which
/// includes the reload procgen chemistry does after generating a reagent mid-round.
/// </para>
/// </summary>
public sealed class CMUReactionProductIndexSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedReagentGeneratorSystem _generator = default!;

    private Dictionary<string, List<ReactionPrototype>>? _index;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        // Procgen cleanup removes generated reactions with RemoveString, which raises no reload event.
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => Invalidate());
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<ReactionPrototype>())
            _index = null;
    }

    /// <summary>
    /// The recipes (non-breakdown, non-ignition reactions) that produce <paramref name="reagentId"/>, in engine order.
    /// </summary>
    public bool TryGetRecipes(string reagentId, [NotNullWhen(true)] out IReadOnlyList<ReactionPrototype>? recipes)
    {
        _index ??= BuildIndex();

        if (_index.TryGetValue(reagentId, out var list) && list.Count > 0)
        {
            recipes = list;
            return true;
        }

        recipes = null;
        return false;
    }

    /// <summary>Drops the cached index; the next lookup rebuilds it.</summary>
    public void Invalidate()
    {
        _index = null;
    }

    private Dictionary<string, List<ReactionPrototype>> BuildIndex()
    {
        var index = new Dictionary<string, List<ReactionPrototype>>();
        foreach (var reaction in _proto.EnumeratePrototypes<ReactionPrototype>())
        {
            if (reaction.Source || _generator.IsIgnitionRecipe(reaction.ID))
                continue;

            foreach (var product in reaction.Products.Keys)
            {
                if (!index.TryGetValue(product.Id, out var list))
                {
                    list = new List<ReactionPrototype>();
                    index[product.Id] = list;
                }

                if (!list.Contains(reaction))
                    list.Add(reaction);
            }
        }

        foreach (var list in index.Values)
        {
            list.Sort();
        }

        return index;
    }
}
