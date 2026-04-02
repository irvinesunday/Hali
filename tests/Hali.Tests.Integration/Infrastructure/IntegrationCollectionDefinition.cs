using Xunit;

namespace Hali.Tests.Integration.Infrastructure;

/// <summary>
/// Ensures all integration tests share one factory instance (one server, one DB)
/// and run serially to avoid PostgreSQL concurrency conflicts.
/// </summary>
[CollectionDefinition("Integration")]
public sealed class IntegrationCollection : ICollectionFixture<HaliWebApplicationFactory> { }
