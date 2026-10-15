using Content.Server.Database;
using Moq;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class UserDisconnectRegressionTest
{
    [Test]
    public void DisconnectBeforeDataLoadDoesNotRunCleanupForAnAbsentUser()
    {
        var manager = new UserDbDataManager();
        var user = new Mock<ICommonSession>();
        user.SetupGet(s => s.UserId).Returns(new NetUserId(Guid.NewGuid()));
        var callbacks = 0;
        manager.AddOnPlayerDisconnect(_ => callbacks++);
        Assert.DoesNotThrow(() => manager.ClientDisconnected(user.Object));
        Assert.DoesNotThrow(() => manager.ClientDisconnected(user.Object));
        Assert.That(callbacks, Is.Zero);
    }
}
