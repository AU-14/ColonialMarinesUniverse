using NUnit.Framework.Internal;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests.Pair;

// `await using var pair` + `CleanReturnAsync()` disposes twice. the clean return already put the pair back
// in the pool, so if another worker borrowed it in between, the engine's DisposeAsync sees InUse and
// dirty-kills that other test's server. only the test that borrowed it gets to dirty-return it
public sealed partial class TestPair : IAsyncDisposable
{
    private Test? _borrower;

    internal void MarkBorrowed() => _borrower = TestExecutionContext.CurrentContext.CurrentTest;

    public new async ValueTask DisposeAsync()
    {
        if (State == PairState.InUse && _borrower != null &&
            !ReferenceEquals(_borrower, TestExecutionContext.CurrentContext.CurrentTest))
            return;

        await base.DisposeAsync();
    }
}
