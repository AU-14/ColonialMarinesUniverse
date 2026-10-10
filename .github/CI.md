# CI

`workflows/ci.yml` is the normal build and test pipeline for pull requests,
merge queues, and pushes to master/staging/stable. Keep `Required Checks` as
the aggregate merge gate.

- Build the YAML linter project and its dependencies once. This includes the
  client, server, shared code, unit tests, and integration tests without building
  unrelated tools, benchmarks, or the engine's separate test suites.
- Run unit tests on the build runner. Share compiled output with the prototype
  validator and parallel integration jobs; share client/server output in one
  runtime artifact.
- Run attribution, map, and RSI validation together when relevant assets change.
  Both `Resources/` and `Content.CMU/Resources/` are covered.
- Run CMU chemistry, medical, ship, spatial, and diagnostic tests in separate
  groups. The `CMU14.` filters cover both `CMU14` and `_CMU14` namespaces.
  Namespace filters end in a dot to avoid running similarly named root fixtures
  twice.

Integration tests use four NUnit workers per process, configured in
`Content.IntegrationTests/AssemblyInfo.cs`. The camera-network, condition-driven
surgery, field-mixing, wound-foundation, organ-damage, and heart-physiology
fixtures also allow their methods and parameterized cases to run concurrently.
These fixtures keep their server/pair and mutable state local to each test.
Existing non-parallel fixtures remain isolated. Keep this bounded: unrestricted parallel serialization initialization
has a known JIT contention problem. `NUnit.NumberOfTestWorkers` can override the
default when comparing concurrency settings. The existing `DOTNET_PROCESSOR_COUNT=1`
workaround remains in CI to serialize the engine's PLINQ prototype loading; the
explicit NUnit worker limit still permits test concurrency. Test result artifacts include TRX
timings, and a test that stops progressing for five minutes aborts its test host.

Normal CI sets `NUnit.ExplicitMode=None` for unit and integration tests. Timing
benchmarks are explicitly selected experiments; deterministic allocation,
cache, scheduling, and networking regressions still run in normal CI, including
`Tests.Performance` in the diagnostic group. See [the test audit](CI-test-audit.md)
for the group-by-group decisions.

The following workflows are manual (`workflow_dispatch`):

- `build-test-debug.yml`: full test suite, including bulk entity/component
  sweeps, localization policy tests, and upstream content freezes. Those checks
  are omitted from normal PR CI. Explicit benchmarks still require a separate,
  explicit test selection; running the full suite does not enable them.
- `build-map-renderer.yml`: build and run the development map renderer.
- `test-packaging.yml`: produce client and Windows server test packages.

Publishing, changelog generation, path labels, map checks, and RSI previews keep
their existing workflows. There is no periodic full-suite rerun or separate
CRLF, submodule-policy, PR-template, changelog-edit, or source-branch policy gate.
