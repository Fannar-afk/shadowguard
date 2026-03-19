namespace ShadowGuard;

public static class SeverityHelper
{
    public static SeverityLevel Parse(string value)
    {
        return Enum.TryParse<SeverityLevel>(value, true, out var severity)
            ? severity
            : SeverityLevel.Medium;
    }

    public static int Rank(SeverityLevel severity)
    {
        return severity switch
        {
            SeverityLevel.Critical => 5,
            SeverityLevel.High => 4,
            SeverityLevel.Medium => 3,
            SeverityLevel.Low => 2,
            _ => 1
        };
    }

    public static SeverityLevel FromScore(double score)
    {
        if (score >= 85)
        {
            return SeverityLevel.Critical;
        }

        if (score >= 65)
        {
            return SeverityLevel.High;
        }

        if (score >= 40)
        {
            return SeverityLevel.Medium;
        }

        if (score >= 15)
        {
            return SeverityLevel.Low;
        }

        return SeverityLevel.None;
    }
}

public static class LocalizationHelper
{
    public static string ToChineseSeverity(SeverityLevel severity)
    {
        return severity switch
        {
            SeverityLevel.Critical => "严重",
            SeverityLevel.High => "高危",
            SeverityLevel.Medium => "中危",
            SeverityLevel.Low => "低危",
            _ => "无"
        };
    }

    public static string ToChineseGateOutcome(GateOutcome outcome)
    {
        return outcome switch
        {
            GateOutcome.Block => "阻断",
            GateOutcome.Warn => "告警",
            _ => "通过"
        };
    }

    public static string ToChineseCategory(string category)
    {
        return category.Trim().ToLowerInvariant() switch
        {
            "malicious" => "恶意风险",
            "source" => "来源风险",
            "integrity" => "完整性风险",
            "stability" => "稳定性风险",
            "license" => "许可证风险",
            "plugin" => "插件规则",
            _ => category
        };
    }

    public static string ToChineseSourceType(string sourceType)
    {
        return sourceType.Trim().ToLowerInvariant() switch
        {
            "registry" => "仓库",
            "git" => "Git 仓库",
            "url" => "URL 地址",
            "local" => "本地路径",
            _ => sourceType
        };
    }
}
