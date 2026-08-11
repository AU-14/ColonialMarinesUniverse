using System.Threading;
using System.Threading.Tasks;

namespace Content.Server.Preferences.Managers;

/// <summary>
/// Serializes preference updates that replace a player's complete profile snapshot.
/// </summary>
internal sealed class PreferenceUpdateLock
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task Run(Func<Task> update)
    {
        await _semaphore.WaitAsync();
        try
        {
            await update();
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
