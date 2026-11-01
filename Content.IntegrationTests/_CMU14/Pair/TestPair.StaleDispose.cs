using JetBrains.Annotations;
using NUnit.Framework.Internal;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests.Pair;

// `await using var pair` + `CleanReturnAsync()` disposes twice. the clean return already put the pair back
// in the pool, so if another worker borrowed it in between, the engine's DisposeAsync sees InUse and
// dirty-kills that other test's server. only the test that borrowed it gets to dirty-return it
public sealed partial class TestPair : IAsyncDisposable
{
    private Test? _borrower;

    // the engine marks the pair InUse inside GetPair, before MarkBorrowed runs, so "who clean-returned it"
    // has to be remembered too or a late dispose slips through that window
    private Test? _returnedBy;

    internal void MarkBorrowed()
    {
        _borrower = TestExecutionContext.CurrentContext.CurrentTest;
        _returnedBy = null;
    }

    [HandlesResourceDisposal]
    public new async ValueTask CleanReturnAsync()
    {
        // set before the base call, it hands the pair back to the pool on its way out
        _returnedBy = TestExecutionContext.CurrentContext.CurrentTest;
        await base.CleanReturnAsync();
    }

    public new async ValueTask DisposeAsync()
    {
        var test = TestExecutionContext.CurrentContext.CurrentTest;
        if (ReferenceEquals(_returnedBy, test) ||
            State == PairState.InUse && _borrower != null && !ReferenceEquals(_borrower, test))
            return;

        await base.DisposeAsync();
    }
}
