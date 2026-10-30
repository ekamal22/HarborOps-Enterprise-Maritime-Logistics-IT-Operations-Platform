using Xunit;

// Tests share one PostgreSQL database and mutate its baseline row.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
