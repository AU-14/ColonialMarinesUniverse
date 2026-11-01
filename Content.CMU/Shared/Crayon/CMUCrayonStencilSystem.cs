using System.Diagnostics.CodeAnalysis;

namespace Content.Shared.CMU14.Crayon;

/// <summary>
/// The custom symbol each player draws with: picked in any crayon window, stamped by any crayon,
/// cleared when they pick a stock decal. The server sets it; clients read it.
/// </summary>
public sealed class CMUCrayonStencilSystem : EntitySystem
{
    /// <summary>
    /// The custom symbol the user stamps with any crayon, if they picked one.
    /// </summary>
    public bool TryGetStencil(Entity<CMUCrayonStencilComponent?> user, [NotNullWhen(true)] out CMUCrayonSymbol? symbol)
    {
        symbol = null;
        if (!Resolve(user, ref user.Comp, false))
            return false;

        symbol = user.Comp.Symbol;
        return symbol != null;
    }

    public bool TrySetStencil(EntityUid user, CMUCrayonSymbol symbol)
    {
        if (!symbol.IsValid())
            return false;

        var comp = EnsureComp<CMUCrayonStencilComponent>(user);
        comp.Symbol = symbol;
        Dirty(user, comp);
        return true;
    }

    public void ClearStencil(Entity<CMUCrayonStencilComponent?> user)
    {
        if (!Resolve(user, ref user.Comp, false) || user.Comp.Symbol == null)
            return;

        user.Comp.Symbol = null;
        Dirty(user, user.Comp);
    }
}
