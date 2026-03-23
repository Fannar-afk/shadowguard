using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ShadowGuard;

public enum SeverityLevel
{
    None,
    Low,
    Medium,
    High,
    Critical
}

public enum GateOutcome
{
    Pass,
    Warn,
    Block
}

public sealed class DependencyComponent
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "unknown";
    public string Ecosystem { get; set; } = string.Empty;
    public bool IsDirect { get; set; }
    public string SourceType { get; set; } = "Registry";
    public string License { get; set; } = "Unknown";
    public string ResolvedLocation { get; set; } = string.Empty;
    public List<string> EvidenceFiles { get; set; } = new();
    public string EvidenceFilesDisplay { get; set; } = string.Empty;
    public double RiskScore { get; set; }
    public SeverityLevel Severity { get; set; } = SeverityLevel.None;
    public string BomReference { get; set; } = string.Empty;
    public string PackageUrl { get; set; } = string.Empty;
    public string SeverityText => LocalizationHelper.ToChineseSeverity(Severity);
    public string SourceTypeText => LocalizationHelper.ToChineseSourceType(SourceType);
    public string DependencyTypeText => IsDirect ? "直接依赖" : "传递依赖";
}

public sealed class Finding
{
    public string RuleId { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string DependencyName { get; set; } = string.Empty;
    public string Ecosystem { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public SeverityLevel Severity { get; set; } = SeverityLevel.None;
    public int Score { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
    public string SourceFiles { get; set; } = string.Empty;
    public string SeverityText => LocalizationHelper.ToChineseSeverity(Severity);
    public string CategoryText => LocalizationHelper.ToChineseCategory(Category);
}

public sealed class PluginRule
{
    private string? _normalizedMatchType;
    private Regex? _cachedRegex;
    private string? _cachedRegexPattern;

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MatchType { get; set; } = "ExactName";
    public string Pattern { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium";
    public int Score { get; set; } = 40;
    public string Category { get; set; } = "Plugin";
    public string Message { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;

    [JsonIgnore]
    public string NormalizedMatchType => _normalizedMatchType ??= MatchType.Trim().ToLowerInvariant();

    [JsonIgnore]
    public bool UsesRegex => NormalizedMatchType is "regexname" or "versionpattern";

    public bool Matches(DependencyComponent component)
    {
        if (string.IsNullOrWhiteSpace(Pattern))
        {
            return false;
        }

        return NormalizedMatchType switch
        {
            "exactname" => string.Equals(component.Name, Pattern, StringComparison.OrdinalIgnoreCase),
            "containsname" => component.Name.Contains(Pattern, StringComparison.OrdinalIgnoreCase),
            "regexname" => GetOrCreateRegex().IsMatch(component.Name),
            "sourcetype" => string.Equals(component.SourceType, Pattern, StringComparison.OrdinalIgnoreCase),
            "versionpattern" => GetOrCreateRegex().IsMatch(component.Version),
            "ecosystem" => string.Equals(component.Ecosystem, Pattern, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private Regex GetOrCreateRegex()
    {
        if (_cachedRegex is not null && string.Equals(_cachedRegexPattern, Pattern, StringComparison.Ordinal))
        {
            return _cachedRegex;
        }

        _cachedRegexPattern = Pattern;
        _cachedRegex = new Regex(Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        return _cachedRegex;
    }
}

public sealed class PluginDefinition : INotifyPropertyChanged
{
    private bool _enabled = true;

    public string PluginId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SourceFile { get; set; } = string.Empty;
    public List<PluginRule> Rules { get; set; } = new();

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
            {
                return;
            }

            _enabled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusLabel));
        }
    }

    public int RuleCount => Rules.Count;
    public string StatusLabel => Enabled ? "已启用" : "已停用";

    public PluginDefinition Clone()
    {
        return new PluginDefinition
        {
            PluginId = PluginId,
            DisplayName = DisplayName,
            Version = Version,
            Author = Author,
            Description = Description,
            SourceFile = SourceFile,
            Enabled = Enabled,
            Rules = Rules.Select(rule => new PluginRule
            {
                Id = rule.Id,
                Name = rule.Name,
                MatchType = rule.MatchType,
                Pattern = rule.Pattern,
                Severity = rule.Severity,
                Score = rule.Score,
                Category = rule.Category,
                Message = rule.Message,
                Recommendation = rule.Recommendation
            }).ToList()
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class ScanPolicy
{
    public int BlockScoreThreshold { get; set; } = 70;
    public bool BlockOnMalicious { get; set; } = true;
    public bool BlockOnLicenseRisk { get; set; } = true;
    public bool WarnOnUnknownSource { get; set; } = true;
}

public sealed class ScanSummary
{
    public int TotalDependencies { get; set; }
    public int DirectDependencies { get; set; }
    public int TransitiveDependencies { get; set; }
    public int FindingsCount { get; set; }
    public int CriticalCount { get; set; }
    public int HighCount { get; set; }
    public int MediumCount { get; set; }
    public int LowCount { get; set; }
    public int OverallScore { get; set; }
    public SeverityLevel OverallSeverity { get; set; } = SeverityLevel.None;
}

public sealed class GateDecision
{
    public GateOutcome Outcome { get; set; } = GateOutcome.Pass;
    public string Reason { get; set; } = string.Empty;
    public List<string> TriggeredPolicies { get; set; } = new();
}

public sealed class SbomDocument
{
    public string BomFormat { get; set; } = "CycloneDX";
    public string SpecVersion { get; set; } = "1.5";
    public int Version { get; set; } = 1;
    public string SerialNumber { get; set; } = string.Empty;
    public SbomMetadata Metadata { get; set; } = new();
    public List<SbomComponent> Components { get; set; } = new();
}

public sealed class SbomMetadata
{
    public string Timestamp { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public int OverallRiskScore { get; set; }
    public string OverallSeverity { get; set; } = string.Empty;
}

public sealed class SbomComponent
{
    public string Type { get; set; } = "library";
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string BomRef { get; set; } = string.Empty;
    public string Purl { get; set; } = string.Empty;
    public string Ecosystem { get; set; } = string.Empty;
    public bool IsDirect { get; set; }
    public string License { get; set; } = "Unknown";
    public string SourceType { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public int RiskScore { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string EvidenceFiles { get; set; } = string.Empty;
}

public sealed class ScanResult
{
    public string TargetPath { get; set; } = string.Empty;
    public DateTime ScannedAt { get; set; }
    public List<DependencyComponent> Components { get; set; } = new();
    public List<Finding> Findings { get; set; } = new();
    public ScanSummary Summary { get; set; } = new();
    public GateDecision GateDecision { get; set; } = new();
    public SbomDocument Sbom { get; set; } = new();
}
