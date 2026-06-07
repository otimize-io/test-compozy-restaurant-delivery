using Xunit;

// All test assemblies run serially (xunit parallelization disabled). This eliminates concurrency-induced
// flakiness — notably the in-memory MassTransit harness teardown race (a late consume resolving a service
// as the provider disposes) and shared static state such as Serilog's static Log.Logger. A PoC can afford
// serial tests in exchange for deterministic runs. Combined with MaxCpuCount=1 in coverlet.runsettings,
// the whole suite executes one test at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
