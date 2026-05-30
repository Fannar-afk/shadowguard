using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Security.Cryptography;
using System.Text;

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
    public double RiskScore { get; set; }
    public SeverityLevel Severity { get; set; } = SeverityLevel.None;
    public string BomReference { get; set; } = string.Empty;
    public string PackageUrl { get; set; } = string.Empty;
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
    public string SourceFiles { get; set; } = string.Empty;
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
    public SeverityLevel OverallSeverity { get; set; }
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

public sealed class PluginRule
{
    private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromMilliseconds(200);
    private Regex? _cachedRegex;
    private string? _cachedPattern;

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
    public string NormalizedMatchType => MatchType.Trim().ToLowerInvariant();

    public bool Matches(DependencyComponent component)
    {
        if (string.IsNullOrWhiteSpace(Pattern))
        {
            return false;
        }

        try
        {
            return NormalizedMatchType switch
            {
                "exactname" => string.Equals(component.Name, Pattern, StringComparison.OrdinalIgnoreCase),
                "containsname" => component.Name.Contains(Pattern, StringComparison.OrdinalIgnoreCase),
                "regexname" => GetRegex().IsMatch(component.Name),
                "sourcetype" => string.Equals(component.SourceType, Pattern, StringComparison.OrdinalIgnoreCase),
                "versionpattern" => GetRegex().IsMatch(component.Version),
                "ecosystem" => string.Equals(component.Ecosystem, Pattern, StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private Regex GetRegex()
    {
        if (_cachedRegex is not null && _cachedPattern == Pattern)
        {
            return _cachedRegex;
        }

        _cachedPattern = Pattern;
        _cachedRegex = new Regex(Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexMatchTimeout);
        return _cachedRegex;
    }
}

public sealed class PluginDefinition
{
    public string PluginId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public List<PluginRule> Rules { get; set; } = new();
}

public sealed class PluginService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public List<PluginDefinition> LoadPlugins(string pluginDirectory)
    {
        if (!Directory.Exists(pluginDirectory))
        {
            return new List<PluginDefinition>();
        }

        var plugins = new List<PluginDefinition>();
        foreach (var file in Directory.EnumerateFiles(pluginDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using var stream = File.OpenRead(file);
                var plugin = JsonSerializer.Deserialize<PluginDefinition>(stream, JsonOptions);
                if (plugin is not null && plugin.Enabled && plugin.Rules.Count > 0)
                {
                    plugins.Add(plugin);
                }
            }
            catch
            {
            }
        }

        return plugins;
    }
}

public sealed class ProjectScanner
{
    private static readonly string[] IgnoredDirectoryNames = { ".git", "node_modules", "bin", "obj", "dist", "build", "target", ".venv", "venv" };
    private static readonly Regex Pep508DirectReferenceRegex = new("^(?<name>[A-Za-z0-9_.\\-]+)\\s*@\\s*(?<source>.+)$", RegexOptions.Compiled);
    private static readonly Regex RequirementRegex = new("^(?<name>[A-Za-z0-9_.\\-]+)\\s*(?<operator>==|>=|<=|~=|>|<)?\\s*(?<version>.*)$", RegexOptions.Compiled);

    public List<DependencyComponent> DiscoverComponents(string targetPath)
    {
        var components = new Dictionary<string, DependencyComponent>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in EnumerateManifestFiles(targetPath))
        {
            var name = Path.GetFileName(file);
            if (string.Equals(name, "package.json", StringComparison.OrdinalIgnoreCase))
            {
                ScanPackageJson(file, components);
            }
            else if (name.StartsWith("requirements", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                ScanRequirements(file, components);
            }
            else if (string.Equals(name, "go.mod", StringComparison.OrdinalIgnoreCase))
            {
                ScanGoMod(file, components);
            }
            else if (string.Equals(Path.GetExtension(file), ".csproj", StringComparison.OrdinalIgnoreCase))
            {
                ScanCsproj(file, components);
            }
        }

        return components.Values.OrderBy(c => c.Ecosystem).ThenBy(c => c.Name).ToList();
    }

    private static IEnumerable<string> EnumerateManifestFiles(string targetPath)
    {
        if (!Directory.Exists(targetPath))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(targetPath, "*.*", SearchOption.AllDirectories))
        {
            var directory = Path.GetDirectoryName(file) ?? string.Empty;
            if (IgnoredDirectoryNames.Any(part => directory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains(part, StringComparer.OrdinalIgnoreCase)))
            {
                continue;
            }

            var name = Path.GetFileName(file);
            if (name is "package.json" or "go.mod" || name.StartsWith("requirements", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(file).Equals(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                yield return file;
            }
        }
    }

    private static void ScanPackageJson(string file, IDictionary<string, DependencyComponent> components)
    {
        try
        {
            using var stream = File.OpenRead(file);
            using var document = JsonDocument.Parse(stream);
            foreach (var propertyName in new[] { "dependencies", "devDependencies", "optionalDependencies", "peerDependencies" })
            {
                if (!document.RootElement.TryGetProperty(propertyName, out var block) || block.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var dependency in block.EnumerateObject())
                {
                    AddOrUpdate(components, new DependencyComponent
                    {
                        Name = dependency.Name,
                        Version = dependency.Value.GetString() ?? "unknown",
                        Ecosystem = "npm",
                        IsDirect = true,
                        SourceType = InferSourceType(dependency.Value.GetString()),
                        EvidenceFiles = new List<string> { file }
                    });
                }
            }
        }
        catch
        {
        }
    }

    private static void ScanRequirements(string file, IDictionary<string, DependencyComponent> components)
    {
        foreach (var rawLine in File.ReadLines(file))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("-"))
            {
                continue;
            }

            // PEP 508 direct references ("name @ <url>") must be handled before the
            // operator-based form, otherwise the URL is captured as the version and the
            // Git/URL source type is lost.
            var directReference = Pep508DirectReferenceRegex.Match(line);
            if (directReference.Success)
            {
                var source = directReference.Groups["source"].Value.Trim();
                AddOrUpdate(components, new DependencyComponent
                {
                    Name = directReference.Groups["name"].Value,
                    Version = source,
                    Ecosystem = "pip",
                    IsDirect = true,
                    SourceType = InferSourceType(source),
                    EvidenceFiles = new List<string> { file }
                });
                continue;
            }

            var match = RequirementRegex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var version = string.IsNullOrWhiteSpace(match.Groups["version"].Value) ? "unspecified" : match.Groups["version"].Value;
            AddOrUpdate(components, new DependencyComponent
            {
                Name = match.Groups["name"].Value,
                Version = version,
                Ecosystem = "pip",
                IsDirect = true,
                SourceType = InferSourceType(version),
                EvidenceFiles = new List<string> { file }
            });
        }
    }

    private static void ScanGoMod(string file, IDictionary<string, DependencyComponent> components)
    {
        foreach (var rawLine in File.ReadLines(file))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//") || line is "require (" or ")")
            {
                continue;
            }

            if (line.StartsWith("require ", StringComparison.Ordinal))
            {
                line = line["require ".Length..].Trim();
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[0].Contains('/'))
            {
                AddOrUpdate(components, new DependencyComponent
                {
                    Name = parts[0],
                    Version = parts[1],
                    Ecosystem = "go",
                    IsDirect = !line.Contains("indirect", StringComparison.OrdinalIgnoreCase),
                    SourceType = "Registry",
                    EvidenceFiles = new List<string> { file }
                });
            }
        }
    }

    private static void ScanCsproj(string file, IDictionary<string, DependencyComponent> components)
    {
        try
        {
            var document = XDocument.Load(file);
            foreach (var package in document.Descendants().Where(e => e.Name.LocalName == "PackageReference"))
            {
                var name = package.Attribute("Include")?.Value ?? package.Attribute("Update")?.Value;
                var version = package.Attribute("Version")?.Value ?? package.Elements().FirstOrDefault(e => e.Name.LocalName == "Version")?.Value ?? "unknown";
                if (!string.IsNullOrWhiteSpace(name))
                {
                    AddOrUpdate(components, new DependencyComponent
                    {
                        Name = name,
                        Version = version,
                        Ecosystem = "nuget",
                        IsDirect = true,
                        SourceType = "Registry",
                        EvidenceFiles = new List<string> { file }
                    });
                }
            }
        }
        catch
        {
        }
    }

    private static void AddOrUpdate(IDictionary<string, DependencyComponent> components, DependencyComponent component)
    {
        var key = component.Ecosystem + ":" + component.Name;
        if (components.TryGetValue(key, out var existing))
        {
            foreach (var file in component.EvidenceFiles)
            {
                if (!existing.EvidenceFiles.Contains(file))
                {
                    existing.EvidenceFiles.Add(file);
                }
            }
            return;
        }

        components[key] = component;
    }

    private static string InferSourceType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Registry";
        }

        if (value.StartsWith("git+", StringComparison.OrdinalIgnoreCase) || value.StartsWith("git://", StringComparison.OrdinalIgnoreCase))
        {
            return "Git";
        }

        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return "Url";
        }

        if (value.StartsWith("file:", StringComparison.OrdinalIgnoreCase) || value.StartsWith("../", StringComparison.OrdinalIgnoreCase) || value.StartsWith("./", StringComparison.OrdinalIgnoreCase))
        {
            return "Local";
        }

        return "Registry";
    }
}

public sealed class RiskScoringService
{
    private static readonly Dictionary<string, (SeverityLevel Severity, int Score, string Message, string Recommendation)> HistoricalPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["event-stream"] = (SeverityLevel.Critical, 92, "依赖名称命中了历史供应链投毒事件软件包。", "请替换为已验证的安全版本。"),
        ["flatmap-stream"] = (SeverityLevel.Critical, 95, "依赖名称命中了历史恶意载荷软件包。", "在人工核验前不建议发布。"),
        ["ua-parser-js"] = (SeverityLevel.High, 78, "依赖名称命中了历史凭据窃取事件软件包。", "请确认版本来源可信。"),
        ["node-ipc"] = (SeverityLevel.High, 72, "依赖名称命中了历史破坏性 protestware 事件软件包。", "请检查版本锁定策略。")
    };

    public ScanResult BuildResult(string targetPath, IEnumerable<DependencyComponent> components, IEnumerable<PluginDefinition> plugins)
    {
        var componentList = components.ToList();
        var findings = new List<Finding>();
        var pluginRules = plugins.Where(p => p.Enabled).SelectMany(p => p.Rules).ToList();

        foreach (var component in componentList)
        {
            var componentFindings = AnalyzeComponent(component, pluginRules);
            findings.AddRange(componentFindings);
            component.RiskScore = componentFindings.Count == 0 ? 0 : Math.Min(100, componentFindings.Max(f => f.Score) + (componentFindings.Sum(f => f.Score) - componentFindings.Max(f => f.Score)) * 0.35);
            component.Severity = SeverityHelper.FromScore(component.RiskScore);
        }

        var summary = BuildSummary(componentList, findings);
        return new ScanResult
        {
            TargetPath = targetPath,
            ScannedAt = DateTime.Now,
            Components = componentList.OrderByDescending(c => c.RiskScore).ThenBy(c => c.Name).ToList(),
            Findings = findings.OrderByDescending(f => f.Score).ToList(),
            Summary = summary,
            Sbom = BuildSbom(targetPath, componentList, summary)
        };
    }

    private static List<Finding> AnalyzeComponent(DependencyComponent component, List<PluginRule> pluginRules)
    {
        var findings = new List<Finding>();
        if (HistoricalPackages.TryGetValue(component.Name, out var historical))
        {
            findings.Add(CreateFinding(component, "builtin.historical.package", "历史供应链事件软件包", "Malicious", historical.Severity, historical.Score, historical.Message, historical.Recommendation));
        }

        if (component.SourceType is "Git" or "Url")
        {
            findings.Add(CreateFinding(component, "builtin.untrusted.source", "直接使用 Git 或 URL 依赖", "Source", SeverityLevel.Medium, 48, "依赖来自 Git 或 URL 来源。", "建议固定不可变版本或使用受控制品源。"));
        }

        if (component.Version.Equals("latest", StringComparison.OrdinalIgnoreCase) || component.Version == "*" || component.Version == "unspecified")
        {
            findings.Add(CreateFinding(component, "builtin.unpinned.version", "依赖版本未固定", "Integrity", SeverityLevel.Medium, 42, "依赖版本未固定到确定值。", "建议固定依赖版本并重新生成锁文件。"));
        }

        if (component.Version.Contains("-alpha", StringComparison.OrdinalIgnoreCase) || component.Version.Contains("-beta", StringComparison.OrdinalIgnoreCase) || component.Version.Contains("-rc", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(CreateFinding(component, "builtin.prerelease.version", "检测到预发布版本组件", "Stability", SeverityLevel.Low, 18, "依赖版本看起来是预发布构建。", "建议优先使用稳定版本。"));
        }

        foreach (var rule in pluginRules.Where(r => r.Matches(component)))
        {
            findings.Add(CreateFinding(component, rule.Id, rule.Name, rule.Category, SeverityHelper.Parse(rule.Severity), rule.Score, rule.Message, rule.Recommendation));
        }

        return findings;
    }

    private static Finding CreateFinding(DependencyComponent component, string id, string name, string category, SeverityLevel severity, int score, string message, string recommendation)
    {
        return new Finding
        {
            RuleId = id,
            RuleName = name,
            DependencyName = component.Name,
            Ecosystem = component.Ecosystem,
            Category = category,
            Severity = severity,
            Score = score,
            Message = message,
            Recommendation = recommendation,
            SourceFiles = string.Join("; ", component.EvidenceFiles)
        };
    }

    private static ScanSummary BuildSummary(List<DependencyComponent> components, List<Finding> findings)
    {
        var score = components.Count == 0 ? 0 : (int)Math.Round(components.Average(c => c.RiskScore));
        return new ScanSummary
        {
            TotalDependencies = components.Count,
            DirectDependencies = components.Count(c => c.IsDirect),
            TransitiveDependencies = components.Count(c => !c.IsDirect),
            FindingsCount = findings.Count,
            CriticalCount = findings.Count(f => f.Severity == SeverityLevel.Critical),
            HighCount = findings.Count(f => f.Severity == SeverityLevel.High),
            MediumCount = findings.Count(f => f.Severity == SeverityLevel.Medium),
            LowCount = findings.Count(f => f.Severity == SeverityLevel.Low),
            OverallScore = score,
            OverallSeverity = SeverityHelper.FromScore(score)
        };
    }

    private static SbomDocument BuildSbom(string targetPath, List<DependencyComponent> components, ScanSummary summary)
    {
        return new SbomDocument
        {
            SerialNumber = "urn:uuid:" + Guid.NewGuid(),
            Metadata = new SbomMetadata
            {
                Timestamp = DateTime.UtcNow.ToString("O"),
                ProjectName = new DirectoryInfo(targetPath).Name,
                TargetPath = targetPath,
                OverallRiskScore = summary.OverallScore,
                OverallSeverity = summary.OverallSeverity.ToString()
            },
            Components = components.Select(component => new SbomComponent
            {
                Name = component.Name,
                Version = component.Version,
                BomRef = CreateBomReference(component.Name, component.Version, component.Ecosystem),
                Purl = CreatePurl(component),
                Ecosystem = component.Ecosystem,
                IsDirect = component.IsDirect,
                License = component.License,
                SourceType = component.SourceType,
                Scope = component.IsDirect ? "required" : "optional",
                RiskScore = (int)Math.Round(component.RiskScore),
                Severity = component.Severity.ToString(),
                EvidenceFiles = string.Join("; ", component.EvidenceFiles)
            }).ToList()
        };
    }

    private static string CreatePurl(DependencyComponent component)
    {
        var type = component.Ecosystem switch
        {
            "npm" => "npm",
            "pip" => "pypi",
            "nuget" => "nuget",
            "go" => "golang",
            _ => component.Ecosystem
        };
        return $"pkg:{type}/{component.Name}@{component.Version}";
    }

    private static string CreateBomReference(string name, string version, string ecosystem)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes($"{ecosystem}:{name}:{version}");
        return Convert.ToHexString(sha256.ComputeHash(bytes)[..12]).ToLowerInvariant();
    }
}

public sealed class GateDecisionService
{
    public GateDecision Evaluate(ScanResult result, ScanPolicy policy)
    {
        var triggers = new List<string>();
        var maliciousHit = result.Findings.Any(f => string.Equals(f.Category, "Malicious", StringComparison.OrdinalIgnoreCase) && f.Severity >= SeverityLevel.High);
        var licenseHit = result.Findings.Any(f => string.Equals(f.Category, "License", StringComparison.OrdinalIgnoreCase) && f.Severity >= SeverityLevel.Medium);
        var sourceHit = result.Findings.Any(f => string.Equals(f.Category, "Source", StringComparison.OrdinalIgnoreCase));

        if (policy.BlockOnMalicious && maliciousHit) triggers.Add("检测到恶意依赖或历史高风险投毒软件包信号，触发阻断策略。");
        if (policy.BlockOnLicenseRisk && licenseHit) triggers.Add("识别到许可证合规风险，触发阻断策略。");
        if (result.Summary.OverallScore >= policy.BlockScoreThreshold) triggers.Add($"项目综合风险分 {result.Summary.OverallScore} 已达到阻断阈值 {policy.BlockScoreThreshold}。");

        if (triggers.Count > 0)
        {
            return new GateDecision { Outcome = GateOutcome.Block, Reason = "当前项目未通过安全闸门校验。", TriggeredPolicies = triggers };
        }

        if (policy.WarnOnUnknownSource && sourceHit)
        {
            return new GateDecision { Outcome = GateOutcome.Warn, Reason = "扫描完成，但存在来自外部源或非仓库源的依赖。", TriggeredPolicies = new List<string> { "至少有一个依赖来自 Git、URL 或本地文件，请进行人工复核。" } };
        }

        return new GateDecision { Outcome = GateOutcome.Pass, Reason = "项目已通过当前配置的 ShadowGuard 安全闸门。", TriggeredPolicies = new List<string> { "本次扫描未命中阻断或告警策略。" } };
    }
}

public sealed class ShadowGuardEngine
{
    private readonly ProjectScanner _scanner = new();
    private readonly PluginService _pluginService = new();
    private readonly RiskScoringService _riskScoringService = new();
    private readonly GateDecisionService _gateDecisionService = new();

    public ScanResult Scan(string targetPath, ScanPolicy? policy = null, string? pluginDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(targetPath) || !Directory.Exists(targetPath))
        {
            throw new DirectoryNotFoundException($"Target path does not exist: {targetPath}");
        }

        var plugins = string.IsNullOrWhiteSpace(pluginDirectory) ? new List<PluginDefinition>() : _pluginService.LoadPlugins(pluginDirectory);
        var result = _riskScoringService.BuildResult(targetPath, _scanner.DiscoverComponents(targetPath), plugins);
        result.GateDecision = _gateDecisionService.Evaluate(result, policy ?? new ScanPolicy());
        return result;
    }
}

public static class SeverityHelper
{
    public static SeverityLevel Parse(string value)
    {
        return Enum.TryParse<SeverityLevel>(value, true, out var severity) ? severity : SeverityLevel.Medium;
    }

    public static SeverityLevel FromScore(double score)
    {
        if (score >= 85) return SeverityLevel.Critical;
        if (score >= 65) return SeverityLevel.High;
        if (score >= 40) return SeverityLevel.Medium;
        if (score >= 15) return SeverityLevel.Low;
        return SeverityLevel.None;
    }
}
