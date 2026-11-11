using System.Linq;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Requisitions;

// GetPrototypeTextures spawns and deletes a whole dummy entity for every prototype
// without an Icon. The ASRS rebuilt ~300 of those on every state push, which froze the client
// for seconds, and one prototype throwing on the client killed the list mid-build (empty
// catalog, then a full state resync that reopened the window twice).
public static class RequisitionsIconCache
{
    // icons resolved per frame while a freshly built list fills in
    private const int IconsPerFrame = 8;

    private static readonly Dictionary<string, List<Texture>> Cache = new();
    private static int _budget;

    /// <summary>Called once a frame by the itemized view, before its rows update.</summary>
    public static void NewFrame()
    {
        _budget = IconsPerFrame;
    }

    // callers get their own list: LayeredTextureRect users clear and refill theirs
    public static bool TryGetCached(EntityPrototype prototype, out List<Texture> textures)
    {
        if (Cache.TryGetValue(prototype.ID, out var cached))
        {
            textures = new List<Texture>(cached);
            return true;
        }

        textures = default!;
        return false;
    }

    /// <summary>Resolves an icon now if this frame's budget allows, otherwise returns false to retry next frame.</summary>
    public static bool TryGetBudgeted(SpriteSystem sprites, EntityPrototype prototype, out List<Texture> textures)
    {
        if (TryGetCached(prototype, out textures))
            return true;

        if (_budget <= 0)
            return false;

        _budget--;
        textures = Get(sprites, prototype);
        return true;
    }

    public static List<Texture> Get(SpriteSystem sprites, EntityPrototype prototype)
    {
        if (TryGetCached(prototype, out var cached))
            return cached;

        List<Texture> textures;
        try
        {
            textures = sprites.GetPrototypeTextures(prototype).Select(layer => layer.Default).ToList();
        }
        catch (Exception e)
        {
            Logger.GetSawmill("requisitions").Error($"Couldn't build the ASRS icon for {prototype.ID}: {e}");
            // explicit initializer: a collection expression emits CollectionsMarshal.SetCount, which the sandbox rejects
            textures = new List<Texture> { sprites.GetFallbackTexture() };
        }

        Cache[prototype.ID] = textures;
        return new List<Texture>(textures);
    }
}
