using System.IO;
using System.Text.Json;
using Xunit;

namespace Hali.Tests.Unit.Auth;

// Regression guard for the JWT audience drift reconciled in issue #185.
// The canonical wire value is "hali-platform" — every non-test touchpoint
// (runtime config, env example, CI workflow) must agree. If any of these
// assertions fail, the drift has returned and future surfaces (web,
// institution-admin, ops) will reject tokens that mobile issues or vice versa.
public class JwtAudienceCanonicalValueTests
{
    private const string CanonicalAudience = "hali-platform";

    private static string RepoRoot()
    {
        string current = Directory.GetCurrentDirectory();
        while (current is not null && !File.Exists(Path.Combine(current, "Hali.sln")))
        {
            current = Directory.GetParent(current)?.FullName!;
        }
        Assert.NotNull(current);
        return current!;
    }

    [Fact]
    public void ApiAppsettings_JwtAudience_IsCanonical()
    {
        string path = Path.Combine(RepoRoot(), "src", "Hali.Api", "appsettings.json");
        Assert.True(File.Exists(path), $"appsettings.json not found at {path}");

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        string? audience = doc.RootElement.GetProperty("Auth").GetProperty("JwtAudience").GetString();

        Assert.Equal(CanonicalAudience, audience);
    }

    [Fact]
    public void EnvExample_JwtAudience_IsCanonical()
    {
        string path = Path.Combine(RepoRoot(), ".env.example");
        Assert.True(File.Exists(path), $".env.example not found at {path}");

        string[] lines = File.ReadAllLines(path);
        string? audienceLine = null;
        foreach (string line in lines)
        {
            if (line.StartsWith("JWT_AUDIENCE=", System.StringComparison.Ordinal))
            {
                audienceLine = line;
                break;
            }
        }

        Assert.NotNull(audienceLine);
        Assert.Equal($"JWT_AUDIENCE={CanonicalAudience}", audienceLine);
    }

    [Fact]
    public void CiWorkflow_JwtAudienceEnv_IsCanonical()
    {
        string path = Path.Combine(RepoRoot(), ".github", "workflows", "ci.yml");
        Assert.True(File.Exists(path), $"ci.yml not found at {path}");

        string contents = File.ReadAllText(path);

        // Every Auth__JwtAudience env in the workflow must target the canonical value.
        // Any occurrence of a non-canonical value is a regression.
        Assert.DoesNotContain("Auth__JwtAudience: \"hali\"", contents);
        Assert.DoesNotContain("Auth__JwtAudience: \"hali-mobile\"", contents);

        int canonicalCount = 0;
        int index = 0;
        string needle = $"Auth__JwtAudience: \"{CanonicalAudience}\"";
        while ((index = contents.IndexOf(needle, index, System.StringComparison.Ordinal)) >= 0)
        {
            canonicalCount++;
            index += needle.Length;
        }

        Assert.True(canonicalCount >= 1, "Expected at least one Auth__JwtAudience entry using the canonical value");
    }
}
