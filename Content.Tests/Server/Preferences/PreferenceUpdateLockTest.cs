using System.Threading;
using System.Threading.Tasks;
using Content.Server.Preferences.Managers;
using NUnit.Framework;

namespace Content.Tests.Server.Preferences;

[TestFixture]
[TestOf(typeof(PreferenceUpdateLock))]
public sealed class PreferenceUpdateLockTest
{
    [Test]
    public async Task UpdatesRunSequentially()
    {
        var updateLock = new PreferenceUpdateLock();
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runningUpdates = 0;

        async Task Update(bool wait)
        {
            Assert.That(Interlocked.Increment(ref runningUpdates), Is.EqualTo(1));
            if (wait)
            {
                firstStarted.SetResult();
                await releaseFirst.Task;
            }

            Interlocked.Decrement(ref runningUpdates);
        }

        var first = updateLock.Run(() => Update(true));
        await firstStarted.Task;
        var second = updateLock.Run(() => Update(false));

        Assert.That(second.IsCompleted, Is.False);
        releaseFirst.SetResult();
        await Task.WhenAll(first, second);
        Assert.That(runningUpdates, Is.Zero);
    }
}
