using A2S.Tests.Shared;
using Xunit;

namespace A2S.Api.Tests;

/// <summary>
/// Collection definition for integration tests.
/// Disables parallelization because tests share static user context via TestCurrentUserService.
/// </summary>
[CollectionDefinition("Integration", DisableParallelization = true)]
public class IntegrationTestCollection : ICollectionFixture<TestWebApplicationFactory<Program>>
{
}
