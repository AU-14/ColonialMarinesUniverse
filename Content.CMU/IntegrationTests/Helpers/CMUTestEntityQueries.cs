global using Content.IntegrationTests.CMU14.Helpers;

using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Helpers;

/// <summary>
/// Keeps entity IDs alongside components in LINQ-based test queries, including their original pause filtering.
/// </summary>
public static class CMUTestEntityQueries
{
    public static IEnumerable<Entity<T>> QueryEntities<T>(this IEntityManager entities, bool includePaused = false)
        where T : IComponent
    {
        if (includePaused)
        {
            foreach (var (uid, component) in entities.AllEntityQueryEnumerator<T>())
                yield return (uid, component);
        }
        else
        {
            foreach (var (uid, component) in entities.EntityQueryEnumerator<T>())
                yield return (uid, component);
        }
    }

    public static IEnumerable<(Entity<T1>, Entity<T2>)> QueryEntities<T1, T2>(
        this IEntityManager entities, bool includePaused = false)
        where T1 : IComponent where T2 : IComponent
    {
        if (includePaused)
        {
            foreach (var (uid, first, second) in entities.AllEntityQueryEnumerator<T1, T2>())
                yield return (new(uid, first), new(uid, second));
        }
        else
        {
            foreach (var (uid, first, second) in entities.EntityQueryEnumerator<T1, T2>())
                yield return (new(uid, first), new(uid, second));
        }
    }
}
