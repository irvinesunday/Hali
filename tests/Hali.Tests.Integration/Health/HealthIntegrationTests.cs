using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Hali.Tests.Integration.Infrastructure;
using Xunit;

namespace Hali.Tests.Integration.Health;

[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class HealthIntegrationTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public HealthIntegrationTests(HaliWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Health_ReturnsHealthy_WithDatabaseAndRedis()
    {
        var response = await Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal("healthy",   body.GetProperty("status").GetString());
        Assert.Equal("connected", body.GetProperty("database").GetString());
        Assert.Equal("connected", body.GetProperty("redis").GetString());
    }
}
