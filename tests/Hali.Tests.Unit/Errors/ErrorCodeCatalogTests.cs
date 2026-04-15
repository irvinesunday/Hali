using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Hali.Application.Errors;
using Xunit;

namespace Hali.Tests.Unit.Errors;

/// <summary>
/// H3 (#153) drift-prevention guardrail. These tests fail if any of the
/// following regress:
///
///  1. A constant in <see cref="ErrorCodes"/> stops matching the
///     <c>namespace.reason</c> snake_case wire convention.
///  2. A typed <see cref="AppException"/> subclass's constructor default
///     <c>code</c> value is not present in the catalog.
///  3. A new <c>throw new XxxException("literal.code", ...)</c> slips into
///     <c>src/Hali.Api/Controllers/**</c> or <c>src/Hali.Application/**</c>
///     without going through the catalog.
///  4. The published <c>ErrorCode</c> enum in <c>02_openapi.yaml</c> drifts
///     out of parity with the public (non-internal-only) subset of the
///     catalog.
///
/// Rationale: pure reflection cannot introspect string-literal arguments at
/// a throw site (they become IL operands). A source-file scan is the
/// strongest practical expression of the "reflection-based guard" the issue
/// calls for. It reads the <c>src/</c> tree at test time from the solution
/// root located by walking up from the test assembly's output directory.
/// </summary>
public class ErrorCodeCatalogTests
{
    private static readonly Regex CodeFormat = new(
        @"^[a-z][a-z0-9_]*\.[a-z][a-z0-9_]*$",
        RegexOptions.Compiled);

    private static readonly string[] TypedExceptionNames =
    {
        "ValidationException",
        "NotFoundException",
        "ConflictException",
        "UnauthorizedException",
        "ForbiddenException",
        "RateLimitException",
        "DependencyException",
        "InvariantViolationException",
    };

    /// <summary>
    /// Each <c>public const string</c> on <see cref="ErrorCodes"/> must be a
    /// valid <c>namespace.reason</c> wire code. This catches typos and
    /// accidental non-snake_case entries before they reach a review.
    /// </summary>
    [Fact]
    public void CatalogConstants_FollowWireFormatConvention()
    {
        foreach (var (name, value) in GetCatalogConstants())
        {
            Assert.True(
                CodeFormat.IsMatch(value),
                $"ErrorCodes.{name} = \"{value}\" does not match the expected \"namespace.reason\" snake_case format.");
        }
    }

    /// <summary>
    /// Default <c>code</c> parameter values baked into exception-class
    /// constructors must come from the catalog — otherwise a rename in the
    /// catalog would silently skip a throw site that relies on the default.
    /// Only checks concrete <see cref="AppException"/> subclasses loaded in
    /// the Hali.Application assembly.
    /// </summary>
    [Fact]
    public void ExceptionConstructorDefaults_UseCatalogValues()
    {
        var catalog = new HashSet<string>(
            GetCatalogConstants().Select(c => c.value),
            StringComparer.Ordinal);

        var subclasses = typeof(AppException).Assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(AppException).IsAssignableFrom(t))
            .ToList();

        Assert.NotEmpty(subclasses);

        foreach (var type in subclasses)
        {
            foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            {
                foreach (var p in ctor.GetParameters())
                {
                    if (p.Name != "code" || p.ParameterType != typeof(string))
                    {
                        continue;
                    }

                    if (!p.HasDefaultValue)
                    {
                        continue;
                    }

                    if (p.DefaultValue is not string defaultCode)
                    {
                        continue;
                    }

                    Assert.True(
                        catalog.Contains(defaultCode),
                        $"{type.Name} ctor '{ctor}' has default code \"{defaultCode}\" which is not present in ErrorCodes.");
                }
            }
        }
    }

    /// <summary>
    /// Every typed throw in the scoped source directories must pass an
    /// <see cref="ErrorCodes"/> reference as its code — not a bare string
    /// literal. This is the guardrail the issue body specifically calls out.
    /// </summary>
    [Fact]
    public void ScopedSources_HaveNoBareStringLiteralCodesInTypedThrows()
    {
        var repoRoot = FindRepoRoot();
        var scopeRoots = new[]
        {
            Path.Combine(repoRoot, "src", "Hali.Api", "Controllers"),
            Path.Combine(repoRoot, "src", "Hali.Application"),
        };

        var violations = new List<string>();

        foreach (var root in scopeRoots)
        {
            Assert.True(Directory.Exists(root), $"Expected scope root not found: {root}");

            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                // Skip the catalog itself and the exception-class definitions —
                // those legitimately carry the canonical string literals.
                var rel = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
                if (rel.StartsWith("src/Hali.Application/Errors/", StringComparison.Ordinal))
                {
                    continue;
                }

                var content = File.ReadAllText(file);
                foreach (var exName in TypedExceptionNames)
                {
                    // Match: throw new XxxException(
                    //           <first-arg>  <- code-position
                    // where <first-arg> can be either a positional string
                    // literal or a named argument (`code: "..."` or
                    // `message: "..."`). We reject the case where a bare
                    // string literal is passed as the code position
                    // (positional first arg, or named `code: "..."`).
                    // ValidationException places `message` first and `code`
                    // second, so we also look for `code: "literal"`.

                    // Flag: code: "literal"
                    var namedCode = new Regex(
                        $@"throw\s+new\s+{Regex.Escape(exName)}\s*\([^;]{{0,1500}}?\bcode\s*:\s*""[^""]+""",
                        RegexOptions.Multiline | RegexOptions.Singleline);
                    foreach (Match m in namedCode.Matches(content))
                    {
                        violations.Add($"{rel}: bare literal in `code:` named arg -> {Snippet(m.Value)}");
                    }

                    // Flag: positional first-arg literal for types whose
                    // first ctor parameter is the code (NotFoundException,
                    // ConflictException, DependencyException,
                    // InvariantViolationException).
                    if (exName is "NotFoundException"
                        or "ConflictException"
                        or "DependencyException"
                        or "InvariantViolationException")
                    {
                        var positional = new Regex(
                            $@"throw\s+new\s+{Regex.Escape(exName)}\s*\(\s*""[^""]+""",
                            RegexOptions.Multiline | RegexOptions.Singleline);
                        foreach (Match m in positional.Matches(content))
                        {
                            violations.Add($"{rel}: bare literal in positional code arg -> {Snippet(m.Value)}");
                        }
                    }
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Found bare string-literal error codes in scoped source files. "
            + "All codes must be passed as ErrorCodes.* constants. Violations:\n  - "
            + string.Join("\n  - ", violations));
    }

    /// <summary>
    /// The <c>ErrorCode</c> enum published in <c>02_openapi.yaml</c> must be
    /// in exact parity with the public (non-internal-only) subset of the
    /// catalog. Internal-only codes (see <see cref="ErrorCodes.InternalOnlyCodes"/>)
    /// are redacted by <c>ExceptionToApiErrorMapper</c> and must NOT appear
    /// on the wire or in the spec.
    /// </summary>
    [Fact]
    public void OpenApiErrorCodeEnum_MatchesCatalogPublicSubset()
    {
        var repoRoot = FindRepoRoot();
        var specPath = Path.Combine(repoRoot, "02_openapi.yaml");
        Assert.True(File.Exists(specPath), $"02_openapi.yaml not found at {specPath}");

        var specEnum = ReadOpenApiErrorCodeEnum(specPath);
        Assert.NotEmpty(specEnum);

        var catalogPublic = new HashSet<string>(
            GetCatalogConstants()
                .Select(c => c.value)
                .Where(v => !ErrorCodes.InternalOnlyCodes.Contains(v)),
            StringComparer.Ordinal);

        var missingFromSpec = catalogPublic.Except(specEnum, StringComparer.Ordinal).OrderBy(s => s).ToList();
        var missingFromCatalog = specEnum.Except(catalogPublic, StringComparer.Ordinal).OrderBy(s => s).ToList();
        var leakedInternal = specEnum.Intersect(ErrorCodes.InternalOnlyCodes, StringComparer.Ordinal).OrderBy(s => s).ToList();

        Assert.True(
            missingFromSpec.Count == 0,
            "Codes present in ErrorCodes but missing from 02_openapi.yaml:ErrorCode enum:\n  - "
            + string.Join("\n  - ", missingFromSpec));

        Assert.True(
            missingFromCatalog.Count == 0,
            "Codes present in 02_openapi.yaml:ErrorCode enum but missing from ErrorCodes:\n  - "
            + string.Join("\n  - ", missingFromCatalog));

        Assert.True(
            leakedInternal.Count == 0,
            "Internal-only codes must NOT be published in the OpenAPI ErrorCode enum (they are redacted to server.internal_error on the wire):\n  - "
            + string.Join("\n  - ", leakedInternal));
    }

    private static IEnumerable<(string name, string value)> GetCatalogConstants()
    {
        return typeof(ErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (f.Name, (string)f.GetRawConstantValue()!));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Hali.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate repo root (Hali.sln) walking up from " + AppContext.BaseDirectory);
    }

    private static HashSet<string> ReadOpenApiErrorCodeEnum(string specPath)
    {
        // The spec file uses a fixed shape for ErrorCode that we can parse
        // with straightforward line iteration — adding a full YAML parser
        // dependency just for this would be overkill and slows test startup.
        var result = new HashSet<string>(StringComparer.Ordinal);
        var lines = File.ReadAllLines(specPath);

        var inErrorCode = false;
        var inEnum = false;

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();

            if (!inErrorCode)
            {
                if (Regex.IsMatch(line, @"^\s{4}ErrorCode:\s*$"))
                {
                    inErrorCode = true;
                }
                continue;
            }

            // A new top-level schema key starts at the same indent (4 spaces)
            // ending with `:` — that terminates the ErrorCode block.
            if (Regex.IsMatch(line, @"^\s{4}\S.*:\s*$") && !line.TrimStart().StartsWith('-'))
            {
                if (!line.TrimStart().StartsWith("ErrorCode:", StringComparison.Ordinal))
                {
                    break;
                }
            }

            if (!inEnum)
            {
                if (Regex.IsMatch(line, @"^\s{6}enum:\s*$"))
                {
                    inEnum = true;
                }
                continue;
            }

            // Enum items are `        - some.code`
            var m = Regex.Match(line, @"^\s{8}-\s*([A-Za-z0-9_.]+)\s*$");
            if (m.Success)
            {
                result.Add(m.Groups[1].Value);
                continue;
            }

            // Anything else at depth <= 6 ends the enum block.
            if (line.Length > 0 && !line.StartsWith("        ", StringComparison.Ordinal))
            {
                break;
            }
        }

        return result;
    }

    private static string Snippet(string value)
    {
        value = value.Replace('\n', ' ').Replace('\r', ' ');
        return value.Length > 160 ? value.Substring(0, 160) + "…" : value;
    }
}
