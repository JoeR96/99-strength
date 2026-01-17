using Xunit;

namespace A2S.E2ETests;

/// <summary>
/// Collection definition for E2E tests.
/// Ensures tests run sequentially to avoid conflicts with browser automation and database state.
/// The FrontendFixture manages the Vite dev server lifecycle for all tests in this collection.
/// </summary>
[CollectionDefinition("E2E", DisableParallelization = true)]
public class E2ETestCollection : ICollectionFixture<FrontendFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
