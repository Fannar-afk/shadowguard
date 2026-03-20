using System.IO;

namespace ShadowGuard;

public sealed class RiskScoringService
{
    private static readonly string[] ApprovedRegistryHosts =
    {
        "registry.npmjs.org",
        "files.pythonhosted.org",
        "pypi.org",
        "repo.maven.apache.org",
        "api.nuget.org"
    };

    private static readonly Dictionary<string, (SeverityLevel Severity, int Score, string Message, string Recommendation)> HistoricalIncidentPackages =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["event-stream"] = (
                SeverityLevel.Critical,
                92,
                "依赖名称命中了历史上曾发生供应链投毒事件的软件包。",
                "请替换为已验证的安全版本，并核查所有下游使用方。"),
            ["flatmap-stream"] = (
                SeverityLevel.Critical,
                95,
                "依赖名称命中了历史上携带恶意载荷事件的软件包。",
                "在完成来源与包内容的人工核验前，不建议放行发布。"),
            ["ua-parser-js"] = (
                SeverityLevel.High,
                78,
                "依赖名称命中了曾发生真实凭据窃取事件的软件包。",
                "请确认当前锁定版本来自可信镜像源后再发布。"),
            ["node-ipc"] = (
                SeverityLevel.High,
                72,
                "依赖名称命中了曾出现破坏性 protestware 行为的软件包。",
                "请检查版本锁定策略，并评估是否迁移到更安全的替代组件。")
        };

    public ScanResult BuildResult(string targetPath, IEnumerable<DependencyComponent> components, IEnumerable<PluginDefinition> plugins)
    {
        var componentList = components.ToList();
        var ruleIndex = BuildRuleIndex(plugins);
        var findings = new List<Finding>();

        foreach (var component in componentList)
        {
            var componentFindings = AnalyzeComponent(component, ruleIndex);
            findings.AddRange(componentFindings);

            // Treat the strongest signal as the primary risk driver while still
            // letting multiple weaker findings raise the final component score.
            var maxScore = 0;
            var totalScore = 0;
            foreach (var finding in componentFindings)
            {
                totalScore += finding.Score;
                if (finding.Score > maxScore)
                {
                    maxScore = finding.Score;
                }
            }

            var additionalScore = totalScore - maxScore;
            component.RiskScore = Math.Min(100, maxScore + (additionalScore * 0.35));
            component.Severity = SeverityHelper.FromScore(component.RiskScore);
        }

        var summary = BuildSummary(componentList, findings);
        var sbom = BuildSbom(targetPath, componentList, summary);

        return new ScanResult
        {
            TargetPath = targetPath,
            ScannedAt = DateTime.Now,
            Components = componentList.OrderByDescending(component => component.RiskScore).ThenBy(component => component.Name).ToList(),
            Findings = findings.OrderByDescending(finding => finding.Score).ThenByDescending(finding => SeverityHelper.Rank(finding.Severity)).ToList(),
            Summary = summary,
            Sbom = sbom
        };
    }

    private static List<Finding> AnalyzeComponent(DependencyComponent component, PluginRuleIndex ruleIndex)
    {
        var findings = new List<Finding>();

        if (HistoricalIncidentPackages.TryGetValue(component.Name, out var historicalPackage))
        {
            findings.Add(CreateFinding(
                component,
                "builtin.historical.package",
                "历史供应链事件软件包",
                "Malicious",
                historicalPackage.Severity,
                historicalPackage.Score,
                historicalPackage.Message,
                historicalPackage.Recommendation));
        }

        if (component.SourceType is "Git" or "Url")
        {
            findings.Add(CreateFinding(
                component,
                "builtin.untrusted.source",
                "直接使用 Git 或 URL 依赖",
                "Source",
                SeverityLevel.Medium,
                48,
                "该依赖通过可变的 Git 或 URL 源引入，而不是来自可信且已固定版本的仓库制品。",
                "建议将该依赖镜像到内部制品库，并固定不可变版本或摘要。"));
        }

        if (component.SourceType == "Local")
        {
            findings.Add(CreateFinding(
                component,
                "builtin.local.reference",
                "本地文件依赖",
                "Source",
                SeverityLevel.Medium,
                38,
                "该依赖来自本地文件路径，可能绕过正常的来源校验与制品治理流程。",
                "建议在发布前将该依赖纳入受控的内部制品源。"));
        }

        if (component.Version.Contains("latest", StringComparison.OrdinalIgnoreCase) || component.Version == "*" || component.Version == "unspecified")
        {
            findings.Add(CreateFinding(
                component,
                "builtin.unpinned.version",
                "依赖版本未固定",
                "Integrity",
                SeverityLevel.Medium,
                42,
                "该依赖版本未固定到可重复构建的确定值。",
                "建议固定到经过评审的版本，并重新生成锁文件。"));
        }

        if (component.Version.Contains("-alpha", StringComparison.OrdinalIgnoreCase) || component.Version.Contains("-beta", StringComparison.OrdinalIgnoreCase) || component.Version.Contains("-rc", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(CreateFinding(
                component,
                "builtin.prerelease.version",
                "检测到预发布版本组件",
                "Stability",
                SeverityLevel.Low,
                18,
                "该依赖版本看起来是预发布构建，可能尚未经过完整的安全评审。",
                "建议优先使用稳定版本，或记录必要的例外审批。"));
        }

        if (!string.IsNullOrWhiteSpace(component.ResolvedLocation) && component.SourceType == "Url" && !IsApprovedRegistry(component.ResolvedLocation))
        {
            findings.Add(CreateFinding(
                component,
                "builtin.unapproved.registry",
                "未纳入白名单的制品来源",
                "Source",
                SeverityLevel.High,
                68,
                "该制品下载源不在当前允许的公共仓库白名单中。",
                "请核验发布者身份，并将制品镜像到可信的内部仓库。"));
        }

        foreach (var rule in ruleIndex.GetCandidateRules(component))
        {
            if (!rule.Matches(component))
            {
                continue;
            }

            findings.Add(CreateFinding(
                component,
                string.IsNullOrWhiteSpace(rule.Id) ? $"plugin.{rule.Name}" : rule.Id,
                string.IsNullOrWhiteSpace(rule.Name) ? "插件规则" : rule.Name,
                string.IsNullOrWhiteSpace(rule.Category) ? "Plugin" : rule.Category,
                SeverityHelper.Parse(rule.Severity),
                rule.Score,
                string.IsNullOrWhiteSpace(rule.Message) ? "该依赖命中了插件定义的风险规则。" : rule.Message,
                string.IsNullOrWhiteSpace(rule.Recommendation) ? "请结合插件规则说明，对该依赖进行人工核验。" : rule.Recommendation));
        }

        return findings;
    }

    private static bool IsApprovedRegistry(string location)
    {
        foreach (var host in ApprovedRegistryHosts)
        {
            if (location.Contains(host, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static Finding CreateFinding(DependencyComponent component, string ruleId, string ruleName, string category, SeverityLevel severity, int score, string message, string recommendation)
    {
        return new Finding
        {
            RuleId = ruleId,
            RuleName = ruleName,
            DependencyName = component.Name,
            Ecosystem = component.Ecosystem,
            Category = category,
            Severity = severity,
            Score = score,
            Message = message,
            Recommendation = recommendation,
            Evidence = component.ResolvedLocation,
            SourceFiles = component.EvidenceFilesDisplay
        };
    }

    private static ScanSummary BuildSummary(IReadOnlyCollection<DependencyComponent> components, IReadOnlyCollection<Finding> findings)
    {
        var directDependencies = components.Count(component => component.IsDirect);

        // Project-level risk favors the highest finding, then blends in the
        // overall finding density so one noisy component does not dominate alone.
        var totalScore = findings.Any()
            ? Math.Min(100, (int)Math.Round(findings.Max(finding => finding.Score) * 0.65 + findings.Sum(finding => finding.Score) / Math.Max(1.0, components.Count * 3.2)))
            : 0;

        return new ScanSummary
        {
            TotalDependencies = components.Count,
            DirectDependencies = directDependencies,
            TransitiveDependencies = Math.Max(0, components.Count - directDependencies),
            FindingsCount = findings.Count,
            CriticalCount = findings.Count(finding => finding.Severity == SeverityLevel.Critical),
            HighCount = findings.Count(finding => finding.Severity == SeverityLevel.High),
            MediumCount = findings.Count(finding => finding.Severity == SeverityLevel.Medium),
            LowCount = findings.Count(finding => finding.Severity == SeverityLevel.Low),
            OverallScore = totalScore,
            OverallSeverity = SeverityHelper.FromScore(totalScore)
        };
    }

    private static SbomDocument BuildSbom(string targetPath, IReadOnlyCollection<DependencyComponent> components, ScanSummary summary)
    {
        return new SbomDocument
        {
            SerialNumber = $"urn:uuid:{Guid.NewGuid()}",
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
                BomRef = component.BomReference,
                Purl = component.PackageUrl,
                Ecosystem = component.Ecosystem,
                IsDirect = component.IsDirect,
                License = component.License,
                SourceType = component.SourceType,
                Scope = component.IsDirect ? "required" : "transitive",
                RiskScore = (int)Math.Round(component.RiskScore),
                Severity = component.Severity.ToString(),
                EvidenceFiles = component.EvidenceFilesDisplay
            }).ToList()
        };
    }

    private static PluginRuleIndex BuildRuleIndex(IEnumerable<PluginDefinition> plugins)
    {
        var index = new PluginRuleIndex();

        foreach (var rule in plugins.Where(plugin => plugin.Enabled).SelectMany(plugin => plugin.Rules))
        {
            if (string.IsNullOrWhiteSpace(rule.Pattern))
            {
                continue;
            }

            switch (rule.NormalizedMatchType)
            {
                case "exactname":
                    index.ExactNameRules.Add(rule.Pattern, rule);
                    break;
                case "sourcetype":
                    index.SourceTypeRules.Add(rule.Pattern, rule);
                    break;
                case "ecosystem":
                    index.EcosystemRules.Add(rule.Pattern, rule);
                    break;
                default:
                    index.GeneralRules.Add(rule);
                    break;
            }
        }

        return index;
    }

    private sealed class PluginRuleIndex
    {
        public Lookup<string, PluginRule> ExactNameRules { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Lookup<string, PluginRule> SourceTypeRules { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Lookup<string, PluginRule> EcosystemRules { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<PluginRule> GeneralRules { get; } = new();

        public IEnumerable<PluginRule> GetCandidateRules(DependencyComponent component)
        {
            foreach (var rule in ExactNameRules.GetValues(component.Name))
            {
                yield return rule;
            }

            foreach (var rule in SourceTypeRules.GetValues(component.SourceType))
            {
                yield return rule;
            }

            foreach (var rule in EcosystemRules.GetValues(component.Ecosystem))
            {
                yield return rule;
            }

            foreach (var rule in GeneralRules)
            {
                yield return rule;
            }
        }
    }

    private sealed class Lookup<TKey, TValue> where TKey : notnull
    {
        private readonly Dictionary<TKey, List<TValue>> _entries;

        public Lookup(IEqualityComparer<TKey>? comparer = null)
        {
            _entries = new Dictionary<TKey, List<TValue>>(comparer);
        }

        public void Add(TKey key, TValue value)
        {
            if (!_entries.TryGetValue(key, out var values))
            {
                values = new List<TValue>();
                _entries[key] = values;
            }

            values.Add(value);
        }

        public IEnumerable<TValue> GetValues(TKey key)
        {
            return _entries.TryGetValue(key, out var values) ? values : Array.Empty<TValue>();
        }
    }
}
