namespace Integration.Shared.Fixtures;

/// <summary>
/// Collection definition for integration tests.
/// All test classes decorated with [Collection("Integration")] will share
/// the same CustomWebApplicationFactory instance, preventing ObjectDisposedException.
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<CustomWebApplicationFactory>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
