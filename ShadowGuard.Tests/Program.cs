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
