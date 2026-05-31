using ShadowGuard;
using Xunit;

namespace ShadowGuard.Tests;

public sealed class SeverityHelperTests
{
    [Theory]
    [InlineData(0, SeverityLevel.None)]
    [InlineData(18, SeverityLevel.Low)]
    [InlineData(42, SeverityLevel.Medium)]
    [InlineData(78, SeverityLevel.High)]
    [InlineData(92, SeverityLevel.Critical)]
    public void FromScore_MapsExpectedSeverity(double score, SeverityLevel expected)
    {
        Assert.Equal(expected, SeverityHelper.FromScore(score));
    }
}

public sealed class GateDecisionServiceTests
{
    [Fact]
    public void Evaluate_CleanScan_ReturnsPass()
    {
        var result = new ScanResult
        {
            Summary = new ScanSummary { OverallScore = 0, HighCount = 0, CriticalCount = 0 },
            Findings = new List<Finding>()
        };

        var decision = new GateDecisionService().Evaluate(result, new ScanPolicy());

        Assert.Equal(GateOutcome.Pass, decision.Outcome);
    }

    [Fact]
    public void Evaluate_ScoreAboveThreshold_ReturnsBlock()
    {
        var result = new ScanResult
        {
            Summary = new ScanSummary { OverallScore = 80 },
            Findings = new List<Finding>()
        };

        var decision = new GateDecisionService().Evaluate(result, new ScanPolicy { BlockScoreThreshold = 70 });

        Assert.Equal(GateOutcome.Block, decision.Outcome);
    }

    [Fact]
    public void Evaluate_HighMaliciousFindingWhenPolicyEnabled_ReturnsBlock()
    {
        var result = new ScanResult
        {
            Summary = new ScanSummary { OverallScore = 20 },
            Findings = new List<Finding>
            {
                new()
                {
                    Category = "Malicious",
                    Severity = SeverityLevel.High,
                    Score = 80,
                    RuleName = "Historical incident package"
                }
            }
        };

        var decision = new GateDecisionService().Evaluate(result, new ScanPolicy { BlockOnMalicious = true });

        Assert.Equal(GateOutcome.Block, decision.Outcome);
    }

    [Fact]
    public void Evaluate_MediumLicenseFindingWhenPolicyEnabled_ReturnsBlock()
    {
        var result = new ScanResult
        {
            Summary = new ScanSummary { OverallScore = 20 },
            Findings = new List<Finding>
            {
                new()
                {
                    Category = "License",
                    Severity = SeverityLevel.Medium,
                    Score = 55,
                    RuleName = "License risk"
                }
            }
        };

        var decision = new GateDecisionService().Evaluate(result, new ScanPolicy { BlockOnLicenseRisk = true });

        Assert.Equal(GateOutcome.Block, decision.Outcome);
    }

    [Fact]
    public void Evaluate_SourceRiskWhenWarnPolicyEnabled_ReturnsWarn()
    {
        var result = new ScanResult
        {
            Summary = new ScanSummary { OverallScore = 10 },
            Findings = new List<Finding>
            {
                new() { Category = "Source", Severity = SeverityLevel.Medium, Score = 48 }
            }
        };

        var decision = new GateDecisionService().Evaluate(result, new ScanPolicy { WarnOnUnknownSource = true });

        Assert.Equal(GateOutcome.Warn, decision.Outcome);
    }
}

public sealed class PluginRuleTests
{
    [Fact]
    public void Matches_SupportsExactContainsAndEcosystemRules()
    {
        var component = new DependencyComponent
        {
            Name = "event-stream",
            Version = "1.0.0",
            SourceType = "Registry",
            Ecosystem = "npm"
        };

        Assert.True(new PluginRule { MatchType = "ExactName", Pattern = "event-stream" }.Matches(component));
        Assert.True(new PluginRule { MatchType = "ContainsName", Pattern = "stream" }.Matches(component));
        Assert.True(new PluginRule { MatchType = "Ecosystem", Pattern = "npm" }.Matches(component));
    }

    [Fact]
    public void Matches_SupportsRegexNameRules()
    {
        var component = new DependencyComponent
        {
            Name = "shadowguard-demo-package",
            Version = "1.0.0",
            SourceType = "Registry",
            Ecosystem = "npm"
        };

        var rule = new PluginRule
        {
            MatchType = "RegexName",
            Pattern = "^shadowguard-.*-package$"
        };

        Assert.True(rule.Matches(component));
    }

    [Fact]
    public void Matches_SupportsVersionPatternRules()
    {
        var component = new DependencyComponent
        {
            Name = "typescript",
            Version = "5.0.0-rc.1",
            SourceType = "Registry",
            Ecosystem = "npm"
        };

        var rule = new PluginRule
        {
            MatchType = "VersionPattern",
            Pattern = "(?i)(alpha|beta|rc|preview)"
        };

        Assert.True(rule.Matches(component));
    }

    [Fact]
    public void Matches_EmptyPattern_ReturnsFalse()
    {
        var component = new DependencyComponent
        {
            Name = "lodash",
            Version = "4.17.21",
            SourceType = "Registry",
            Ecosystem = "npm"
        };

        var rule = new PluginRule
        {
            MatchType = "ContainsName",
            Pattern = ""
        };

        Assert.False(rule.Matches(component));
    }

    [Fact]
    public void Matches_InvalidRegex_DoesNotThrowAndReturnsFalse()
    {
        var component = new DependencyComponent
        {
            Name = "lodash",
            Version = "4.17.21",
            SourceType = "Registry",
            Ecosystem = "npm"
        };

        var rule = new PluginRule
        {
            MatchType = "RegexName",
            Pattern = "(invalid"
        };

        Assert.False(rule.Matches(component));
    }
}

public sealed class CycloneDxValidatorTests
{
    [Fact]
    public void Validate_ValidSbom_ReturnsValidResult()
    {
        var document = new SbomDocument
        {
            BomFormat = "CycloneDX",
            SpecVersion = "1.5",
            Version = 1,
            SerialNumber = "urn:uuid:11111111-1111-1111-1111-111111111111",
            Metadata = new SbomMetadata
            {
                Timestamp = DateTimeOffset.UtcNow.ToString("O"),
                ProjectName = "demo"
            },
            Components = new List<SbomComponent>
            {
                new()
                {
                    Type = "library",
                    Name = "lodash",
                    Version = "4.17.21",
                    BomRef = "pkg-npm-lodash",
                    Purl = "pkg:npm/lodash@4.17.21",
                    Scope = "required"
                }
            }
        };

        var result = new CycloneDxValidator().Validate(document);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_InvalidBomFormat_ReturnsError()
    {
        var document = new SbomDocument
        {
            BomFormat = "Unknown",
            SpecVersion = "1.5",
            Version = 1,
            Components = new List<SbomComponent>()
        };

        var result = new CycloneDxValidator().Validate(document);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("bomFormat", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_DuplicateBomRef_ReturnsError()
    {
        var document = new SbomDocument
        {
            BomFormat = "CycloneDX",
            SpecVersion = "1.5",
            Version = 1,
            Components = new List<SbomComponent>
            {
                new() { Type = "library", Name = "a", Version = "1.0.0", BomRef = "duplicate", Scope = "required" },
                new() { Type = "library", Name = "b", Version = "1.0.0", BomRef = "duplicate", Scope = "required" }
            }
        };

        var result = new CycloneDxValidator().Validate(document);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("duplicate bom-ref", StringComparison.OrdinalIgnoreCase));
    }
}


public sealed class ProjectScannerTests
{
    [Fact]
    public void DiscoverComponents_Pep508DirectReference_DetectsGitSource()
    {
        var workspace = Path.Combine(Path.GetTempPath(), "shadowguard-scanner-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);

        try
        {
            File.WriteAllText(
                Path.Combine(workspace, "requirements.txt"),
                "requests==2.31.0\ninternal-agent @ git+https://github.com/example/internal-agent.git\n");

            var components = new ProjectScanner().DiscoverComponents(workspace);

            var pinned = Assert.Single(components, component => component.Name == "requests");
            Assert.Equal("2.31.0", pinned.Version);
            Assert.Equal("Registry", pinned.SourceType);

            var directReference = Assert.Single(components, component => component.Name == "internal-agent");
            Assert.Equal("git+https://github.com/example/internal-agent.git", directReference.Version);
            Assert.Equal("Git", directReference.SourceType);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }
}
public sealed class RiskScoringServiceTests
{
    private static ScanResult Scan(DependencyComponent component)
    {
        return new RiskScoringService().BuildResult(
            "demo-project",
            new[] { component },
            Array.Empty<PluginDefinition>());
    }

    [Theory]
    [InlineData("event-stream", SeverityLevel.Critical, 92)]
    [InlineData("flatmap-stream", SeverityLevel.Critical, 95)]
    [InlineData("ua-parser-js", SeverityLevel.High, 78)]
    [InlineData("node-ipc", SeverityLevel.High, 72)]
    [InlineData("EVENT-STREAM", SeverityLevel.Critical, 92)]
    public void BuildResult_HistoricalIncidentPackage_RaisesMaliciousFinding(string name, SeverityLevel expectedSeverity, int expectedScore)
    {
        var component = new DependencyComponent
        {
            Name = name,
            Version = "1.0.0",
            Ecosystem = "npm",
            SourceType = "Registry"
        };

        var finding = Assert.Single(Scan(component).Findings);

        Assert.Equal("builtin.historical.package", finding.RuleId);
        Assert.Equal("Malicious", finding.Category);
        Assert.Equal(expectedSeverity, finding.Severity);
        Assert.Equal(expectedScore, finding.Score);
    }

    [Theory]
    [InlineData("Git")]
    [InlineData("Url")]
    public void BuildResult_GitOrUrlSource_RaisesMediumSourceFinding(string sourceType)
    {
        var component = new DependencyComponent
        {
            Name = "internal-tool",
            Version = "1.4.2",
            Ecosystem = "npm",
            SourceType = sourceType
        };

        var finding = Assert.Single(Scan(component).Findings);

        Assert.Equal("builtin.untrusted.source", finding.RuleId);
        Assert.Equal("Source", finding.Category);
        Assert.Equal(SeverityLevel.Medium, finding.Severity);
        Assert.Equal(48, finding.Score);
    }

    [Theory]
    [InlineData("latest")]
    [InlineData("*")]
    [InlineData("unspecified")]
    public void BuildResult_UnpinnedVersion_RaisesIntegrityFinding(string version)
    {
        var component = new DependencyComponent
        {
            Name = "some-lib",
            Version = version,
            Ecosystem = "npm",
            SourceType = "Registry"
        };

        var finding = Assert.Single(Scan(component).Findings);

        Assert.Equal("builtin.unpinned.version", finding.RuleId);
        Assert.Equal("Integrity", finding.Category);
        Assert.Equal(SeverityLevel.Medium, finding.Severity);
        Assert.Equal(42, finding.Score);
    }

    [Theory]
    [InlineData("1.0.0-alpha")]
    [InlineData("2.1.0-beta.3")]
    [InlineData("3.0.0-rc.1")]
    public void BuildResult_PrereleaseVersion_RaisesLowStabilityFinding(string version)
    {
        var component = new DependencyComponent
        {
            Name = "preview-lib",
            Version = version,
            Ecosystem = "nuget",
            SourceType = "Registry"
        };

        var finding = Assert.Single(Scan(component).Findings);

        Assert.Equal("builtin.prerelease.version", finding.RuleId);
        Assert.Equal("Stability", finding.Category);
        Assert.Equal(SeverityLevel.Low, finding.Severity);
        Assert.Equal(18, finding.Score);
    }

    [Fact]
    public void BuildResult_CleanRegistryComponent_ProducesNoFindings()
    {
        var component = new DependencyComponent
        {
            Name = "lodash",
            Version = "4.17.21",
            Ecosystem = "npm",
            SourceType = "Registry"
        };

        var result = Scan(component);

        Assert.Empty(result.Findings);
        Assert.Equal(0d, result.Components[0].RiskScore);
        Assert.Equal(SeverityLevel.None, result.Components[0].Severity);
        Assert.Equal(0, result.Summary.OverallScore);
    }

    [Fact]
    public void BuildResult_MultipleFindings_BlendsComponentScore()
    {
        // Git source (48) plus prerelease (18): the strongest finding drives the
        // score, and 35% of the remaining findings is added on top.
        var component = new DependencyComponent
        {
            Name = "internal-preview",
            Version = "1.0.0-rc.1",
            Ecosystem = "npm",
            SourceType = "Git"
        };

        var result = Scan(component);

        Assert.Equal(2, result.Findings.Count);
        Assert.Equal(54.3, result.Components[0].RiskScore, 3);
        Assert.Equal(SeverityLevel.Medium, result.Components[0].Severity);
    }

    [Fact]
    public void BuildResult_BlendedScore_IsCappedAt100()
    {
        // Historical package (92) plus Git source (48) blends above 100 and clamps.
        var component = new DependencyComponent
        {
            Name = "event-stream",
            Version = "1.0.0",
            Ecosystem = "npm",
            SourceType = "Git"
        };

        var result = Scan(component);

        Assert.Equal(2, result.Findings.Count);
        Assert.Equal(100d, result.Components[0].RiskScore);
        Assert.Equal(SeverityLevel.Critical, result.Components[0].Severity);
    }

    [Fact]
    public void BuildResult_SummarizesSeverityCountsAcrossComponents()
    {
        var components = new[]
        {
            new DependencyComponent { Name = "event-stream", Version = "1.0.0", Ecosystem = "npm", SourceType = "Registry" },
            new DependencyComponent { Name = "internal-tool", Version = "1.4.2", Ecosystem = "npm", SourceType = "Git" },
            new DependencyComponent { Name = "lodash", Version = "4.17.21", Ecosystem = "npm", SourceType = "Registry" }
        };

        var summary = new RiskScoringService()
            .BuildResult("demo-project", components, Array.Empty<PluginDefinition>())
            .Summary;

        Assert.Equal(3, summary.TotalDependencies);
        Assert.Equal(2, summary.FindingsCount);
        Assert.Equal(1, summary.CriticalCount);
        Assert.Equal(1, summary.MediumCount);
        Assert.Equal(0, summary.HighCount);
        Assert.Equal(0, summary.LowCount);
        Assert.Equal(47, summary.OverallScore);
    }

    [Fact]
    public void BuildResult_EnabledPluginRule_ContributesFinding()
    {
        var component = new DependencyComponent
        {
            Name = "left-pad",
            Version = "1.0.0",
            Ecosystem = "npm",
            SourceType = "Registry"
        };

        var plugin = new PluginDefinition
        {
            PluginId = "custom",
            Enabled = true,
            Rules =
            {
                new PluginRule
                {
                    Id = "custom.leftpad",
                    Name = "Left pad policy",
                    MatchType = "ExactName",
                    Pattern = "left-pad",
                    Severity = "High",
                    Score = 60,
                    Category = "Custom"
                }
            }
        };

        var result = new RiskScoringService().BuildResult("demo-project", new[] { component }, new[] { plugin });

        var finding = Assert.Single(result.Findings);
        Assert.Equal("custom.leftpad", finding.RuleId);
        Assert.Equal("Custom", finding.Category);
        Assert.Equal(SeverityLevel.High, finding.Severity);
        Assert.Equal(60, finding.Score);
    }

    [Fact]
    public void BuildResult_DisabledPlugin_IsIgnored()
    {
        var component = new DependencyComponent
        {
            Name = "left-pad",
            Version = "1.0.0",
            Ecosystem = "npm",
            SourceType = "Registry"
        };

        var plugin = new PluginDefinition
        {
            PluginId = "custom",
            Enabled = false,
            Rules =
            {
                new PluginRule
                {
                    Id = "custom.leftpad",
                    Name = "Left pad policy",
                    MatchType = "ExactName",
                    Pattern = "left-pad",
                    Severity = "High",
                    Score = 60,
                    Category = "Custom"
                }
            }
        };

        var result = new RiskScoringService().BuildResult("demo-project", new[] { component }, new[] { plugin });

        Assert.Empty(result.Findings);
    }
}
