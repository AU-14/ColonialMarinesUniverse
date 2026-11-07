using System;
using System.Data.Common;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Database;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using Robust.Shared.Log;

namespace Content.Tests.CMU14.Database;

[TestFixture]
public sealed class CMUDatabaseGuardRecoveryTest
{
    [Test]
    public async Task CancelledPostgresInitializationDoesNotBlockTheNextRequest()
    {
        var database = CreateDatabase(true);
        Set(database, "_msLag", 60_000);
        using var cancellation = new CancellationTokenSource();
        var pending = Acquire(database, cancellation.Token);
        cancellation.Cancel();
        Assert.ThrowsAsync<TaskCanceledException>(async () => await pending);

        Set(database, "_msLag", 0);
        await AssertNextRequestCompletes(database);
    }

    [Test]
    public async Task FailedSqliteContextCreationDoesNotBlockTheNextRequest()
    {
        var database = CreateDatabase(false);
        var fail = true;
        Set(database, "_options", (Func<DbContextOptions<SqliteServerDbContext>>) (() =>
        {
            if (fail)
                throw new InvalidOperationException("Injected options factory failure");

            return new DbContextOptionsBuilder<SqliteServerDbContext>().Options;
        }));
        Assert.ThrowsAsync<InvalidOperationException>(async () => await Acquire(database));

        fail = false;
        await AssertNextRequestCompletes(database);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task FailedConnectionDisposalDoesNotBlockTheNextRequest(bool postgres)
    {
        var database = CreateDatabase(postgres);
        var connection = new Mock<DbConnection>();
        connection.SetupProperty(c => c.ConnectionString, "");
        connection.Setup(c => c.DisposeAsync())
            .Returns(() => ValueTask.FromException(new TimeoutException("Injected connection disposal timeout")));
        if (postgres)
        {
            Set(database, "_options", new DbContextOptionsBuilder<PostgresServerDbContext>()
                .UseNpgsql(connection.Object, contextOwnsConnection: true).Options);
        }
        else
        {
            Set(database, "_options", (Func<DbContextOptions<SqliteServerDbContext>>) (() =>
                new DbContextOptionsBuilder<SqliteServerDbContext>()
                    .UseSqlite(connection.Object, contextOwnsConnection: true).Options));
        }

        var guard = await Acquire(database);
        var context = (ServerDbContext) guard.GetType().GetProperty("DbContext")!.GetValue(guard)!;
        // Materialize the owned connection so EF disposes it through the real guard.
        _ = context.Database.GetDbConnection();
        Assert.ThrowsAsync<TimeoutException>(async () => await guard.DisposeAsync());
        connection.Verify(c => c.DisposeAsync(), Times.Once);

        SetDefaultOptions(database, postgres);
        await AssertNextRequestCompletes(database);
    }

    private static async Task AssertNextRequestCompletes(ServerDbBase database)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await using var next = await Acquire(database, deadline.Token);
    }

    private static ServerDbBase CreateDatabase(bool postgres)
    {
        var type = postgres ? typeof(ServerDbPostgres) : typeof(ServerDbSqlite);
        // Isolate guard ownership from startup migrations and the PostgreSQL notification listener.
        // Acquisition, cancellation, context creation and disposal still run the production methods.
        var database = (ServerDbBase) RuntimeHelpers.GetUninitializedObject(type);
        Set(database, "_dbReadyTask", Task.CompletedTask);
        typeof(ServerDbBase).GetField("_opsLog", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(database, Mock.Of<ISawmill>());
        object semaphore = postgres
            ? new SemaphoreSlim(1, 1)
            : Activator.CreateInstance(type.GetNestedType("ConcurrencySemaphore", BindingFlags.NonPublic)!, 1, false)!;
        Set(database, "_prefsSemaphore", semaphore);
        SetDefaultOptions(database, postgres);
        return database;
    }

    private static void SetDefaultOptions(ServerDbBase database, bool postgres)
    {
        if (postgres)
            Set(database, "_options", new DbContextOptionsBuilder<PostgresServerDbContext>().Options);
        else
            Set(database, "_options", (Func<DbContextOptions<SqliteServerDbContext>>) (() =>
                new DbContextOptionsBuilder<SqliteServerDbContext>().Options));
    }

    private static void Set(ServerDbBase database, string field, object value)
        => database.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(database, value);

    private static async Task<IAsyncDisposable> Acquire(ServerDbBase database, CancellationToken cancellation = default)
    {
        var task = (Task) database.GetType().GetMethod("GetDb", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(database, new object[] { cancellation, nameof(CMUDatabaseGuardRecoveryTest) })!;
        await task;
        return (IAsyncDisposable) task.GetType().GetProperty("Result")!.GetValue(task)!;
    }
}
