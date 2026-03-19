namespace ShadowGuard;

public sealed class GateDecisionService
{
    public GateDecision Evaluate(ScanResult result, ScanPolicy policy)
    {
        var triggers = new List<string>();
        var maliciousHit = result.Findings.Any(finding => string.Equals(finding.Category, "Malicious", StringComparison.OrdinalIgnoreCase) && finding.Severity >= SeverityLevel.High);
        var licenseHit = result.Findings.Any(finding => string.Equals(finding.Category, "License", StringComparison.OrdinalIgnoreCase) && finding.Severity >= SeverityLevel.Medium);
        var sourceHit = result.Findings.Any(finding => string.Equals(finding.Category, "Source", StringComparison.OrdinalIgnoreCase));

        if (policy.BlockOnMalicious && maliciousHit)
        {
            triggers.Add("检测到恶意依赖或历史高风险投毒软件包信号，触发阻断策略。");
        }

        if (policy.BlockOnLicenseRisk && licenseHit)
        {
            triggers.Add("插件规则或启发式策略识别到许可证合规风险，触发阻断策略。");
        }

        if (result.Summary.OverallScore >= policy.BlockScoreThreshold)
        {
            triggers.Add($"项目综合风险分 {result.Summary.OverallScore} 已达到阻断阈值 {policy.BlockScoreThreshold}，触发阻断策略。");
        }

        if (triggers.Count > 0)
        {
            return new GateDecision
            {
                Outcome = GateOutcome.Block,
                Reason = "当前项目未通过安全闸门校验。",
                TriggeredPolicies = triggers
            };
        }

        if (policy.WarnOnUnknownSource && sourceHit)
        {
            return new GateDecision
            {
                Outcome = GateOutcome.Warn,
                Reason = "扫描完成，但存在来自外部源或非仓库源的依赖。",
                TriggeredPolicies = new List<string>
                {
                    "至少有一个依赖来自 Git、URL 或本地文件，请进行人工复核后再决定是否放行。"
                }
            };
        }

        if (result.Summary.HighCount > 0 || result.Summary.CriticalCount > 0)
        {
            return new GateDecision
            {
                Outcome = GateOutcome.Warn,
                Reason = "当前没有命中硬阻断策略，但仍存在需要复核的高风险问题。",
                TriggeredPolicies = new List<string>
                {
                    "请在发布前重点复核界面中标记的高风险依赖。"
                }
            };
        }

        return new GateDecision
        {
            Outcome = GateOutcome.Pass,
            Reason = "项目已通过当前配置的 ShadowGuard 安全闸门。",
            TriggeredPolicies = new List<string>
            {
                "本次扫描未命中阻断或告警策略。"
            }
        };
    }
}
