[assembly: Parallelizable(ParallelScope.Children)]

// Run four tests concurrently, while retaining a bounded pool: unrestricted
// concurrency can contend heavily while compiling serialization expression trees.
// https://github.com/dotnet/runtime/issues/107197
// NUnit.NumberOfTestWorkers can override this for profiling or smaller machines.
[assembly: LevelOfParallelism(4)]
