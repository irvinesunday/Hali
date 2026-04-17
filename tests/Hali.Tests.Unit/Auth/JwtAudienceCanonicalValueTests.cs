using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        DirectoryInfo? current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Hali.sln")))
        {
            current = current.Parent;
        }
        Assert.NotNull(current);
        return current!.FullName;
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
    public void CiWorkflow_AllJwtAudienceEnvValues_AreCanonical()
    {
        string path = Path.Combine(RepoRoot(), ".github", "workflows", "ci.yml");
        Assert.True(File.Exists(path), $"ci.yml not found at {path}");

        string contents = File.ReadAllText(path);

        // Extract every Auth__JwtAudience value in the workflow and assert each
        // one is the canonical value — this catches any new non-canonical value,
        // not just the specific strings the PR replaced. Accept double-quoted,
        // single-quoted, or unquoted YAML scalars so the test does not break
        // (or silently miss drift) if ci.yml formatting changes later.
        MatchCollection matches = Regex.Matches(
            contents,
            @"Auth__JwtAudience:\s*(?:""(?<dq>[^""]*)""|'(?<sq>[^']*)'|(?<bare>[^\r\n#]+))");

        Assert.True(matches.Count >= 1, "Expected at least one Auth__JwtAudience entry in ci.yml");

        foreach (Match match in matches)
        {
            string value =
                match.Groups["dq"].Success ? match.Groups["dq"].Value :
                match.Groups["sq"].Success ? match.Groups["sq"].Value :
                match.Groups["bare"].Value.Trim();
            Assert.Equal(CanonicalAudience, value);
        }
    }
}
