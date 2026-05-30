using System.Linq;
using Content.Client.LateJoin;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.ContentPack;
using Robust.Shared.IoC;
using Robust.Shared.Reflection;

namespace Content.IntegrationTests.Tests.UserInterface;

[TestFixture]
public sealed class UiControlTest
{
    // You should not be adding to this.
    private Type[] _ignored = new Type[]
    {
        typeof(LateJoinGui),
    };

    /// <summary>
    /// Tests that all windows can be instantiated successfully.
    /// </summary>
    [Test]
    public async Task TestWindows()
    {
        var pair = await PoolManager.GetServerClient(new PoolSettings()
        {
            Connected = true,
        });
        var activator = pair.Client.ResolveDependency<IDynamicTypeFactory>();
        var refManager = pair.Client.ResolveDependency<IReflectionManager>();
        var loader = pair.Client.ResolveDependency<IModLoader>();

        var windowTypes = refManager.GetAllChildren(typeof(BaseWindow))
            .Where(type => !type.IsAbstract && !_ignored.Contains(type))
            .Where(loader.IsContentType)
            .Where(type => type.GetConstructor(Type.EmptyTypes) != null)
            .ToArray();

        await pair.Client.WaitAssertion(() =>
        {
            foreach (var type in windowTypes)
            {
                // Don't inject because the control themselves have to do it.
                var window = (BaseWindow) activator.CreateInstance(type, oneOff: true, inject: false);
                window.DisposeAllChildren();
            }
        });

        await pair.Client.WaitIdleAsync();

        await pair.CleanReturnAsync();
    }
}
