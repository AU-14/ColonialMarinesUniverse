#nullable enable
using System.IO;
using System.Linq;

namespace Content.IntegrationTests;

/// <summary>Recover from the known RT variant-freeze race before a test receives its game instance.</summary>
internal static class PrototypeStartup
{
    private const int MaxAttempts = 3;

    internal static async Task<T> Start<T>(Func<T> create, Func<T, Task> initialize, TextWriter log)
        where T : IDisposable
    {
        for (var attempt = 1; ; attempt++)
        {
            var instance = create();
            try
            {
                await initialize(instance);
                return instance;
            }
            catch (Exception e)
            {
                // Never reuse a partially initialized prototype manager, including on the final failure.
                instance.Dispose();
                if (attempt >= MaxAttempts || !IsVariantFreezeFailure(e))
                    throw;

                await log.WriteLineAsync(
                    $"Prototype variant startup failed ({attempt}/{MaxAttempts}); retrying with a fresh {typeof(T).Name}.\n{e}");
            }
        }
    }

    private static bool IsVariantFreezeFailure(Exception exception)
    {
        if (exception is AggregateException aggregate)
        {
            return aggregate.InnerExceptions.Count > 0 &&
                   aggregate.InnerExceptions.All(IsVariantFreezeFailure);
        }

        return exception is ArgumentNullException { ParamName: "key" } &&
               exception.StackTrace?.Contains(
                   "Robust.Shared.Prototypes.PrototypeManager.KindData.Freeze()",
                   StringComparison.Ordinal) == true;
    }
}
