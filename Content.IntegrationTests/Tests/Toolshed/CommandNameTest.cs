using Robust.Shared.Console;

namespace Content.IntegrationTests.Tests.Toolshed;

[TestFixture]
public sealed class CommandNameTest : ToolshedTest
{
    [Test]
    public async Task ToolshedCommandsDoNotConflictWithConsoleCommands()
    {
        var console = Server.ResolveDependency<IConsoleHost>();

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var command in Toolshed.DefaultEnvironment.AllCommands())
                {
                    Assert.That(console.AvailableCommands.ContainsKey(command.FullName()), Is.False,
                        $"Toolshed command {command.FullName()} conflicts with a console command.");
                }
            });
        });
    }
}
