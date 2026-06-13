using Xunit;

// These integration tests each spin up the real API via WebApplicationFactory<Program>.
// Running multiple such factories in parallel races on the shared top-level-statement
// entry point and intermittently throws "entry point exited without ever building an
// IHost". Serializing the assembly's tests removes that race.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
