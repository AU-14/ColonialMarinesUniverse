// SPDX-License-Identifier: AGPL-3.0-only
// Ported from the cooking of crystall-punk-14 (MIT) and adapted to this project.
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.Cooking;

namespace Content.Server.CMU14.Cooking;

// Server side of cooking: reacts to the contents of a pot or pan changing, so that the dish can be recognised.
public sealed class CookingSystem : SharedCookingSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FoodHolderComponent, SolutionChangedEvent>(OnHolderChanged);
    }

    // A holder that has been emptied holds no dish any more.
    private void OnHolderChanged(Entity<FoodHolderComponent> ent, ref SolutionChangedEvent args)
    {
        if (args.Solution.Comp.Solution.Volume != 0 || ent.Comp.FoodData is null)
            return;

        ent.Comp.FoodData = null;
        Dirty(ent);
    }
}
