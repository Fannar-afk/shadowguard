using ShadowGuard;

var failures = new List<string>();

CheckSeverityMapping(failures);
CheckGateDecisionPass(failures);
CheckGateDecisionBlockOnScore(failures);
CheckGateDecisionWarnOnSource(failures);
CheckPluginRuleMatching(failures);

if (failures.Count > 0)
{
    Console.Error.WriteLine("ShadowGuard lightweight verification failed:");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine("- " + failure);
    }

    Environment.Exit(1);
}

Console.WriteLine("ShadowGuard lightweight verification passed.");

static void CheckSeverityMapping(List<string> failures)
{
    Expect(SeverityHelper.FromScore(0) == SeverityLevel.None, "score 0 should map to None", failures);
    Expect(SeverityHelper.FromScore(18) == SeverityLevel.Low, "score 18 should map to Low", failures);
    Expect(SeverityHelper.FromScore(42) == SeverityLevel.Medium, "score 42 should map to Medium", failures);
    Expect(SeverityHelper.FromScore(78) == SeverityLevel.High, "score 78 should map to High", failures);
    Expect(SeverityHelper.FromScore(92) == SeverityLevel.Critical, "score 92 should map to Critical", failures);
}

static void CheckGateDecisionPass(List<string> failures)
{
    var result = new ScanResult
    {
        Summary = new ScanSummary { OverallScore = 0, HighCount = 0, CriticalCount = 0 },
        Findings = new List<Finding>()
    };

    var decision = new GateDecisionService().Evaluate(result, new ScanPolicy());
    Expect(decision.Outcome == GateOutcome.Pass, "clean scan should pass", failures);
}

static void CheckGateDecisionBlockOnScore(List<string> failures)
{
    var result = new ScanResult
    {
        Summary = new ScanSummary { OverallScore = 80 },
        Findings = new List<Finding>()
    };

    var decision = new GateDecisionService().Evaluate(result, new ScanPolicy { BlockScoreThreshold = 70 });
    Expect(decision.Outcome == GateOutcome.Block, "score above threshold should block", failures);
}

static void CheckGateDecisionWarnOnSource(List<string> failures)
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
    Expect(decision.Outcome == GateOutcome.Warn, "source risk should warn when enabled", failures);
}

static void CheckPluginRuleMatching(List<string> failures)
{
    var component = new DependencyComponent
    {
        Name = "event-stream",
        Version = "1.0.0",
        SourceType = "Registry",
        Ecosystem = "npm"
    };

    var exact = new PluginRule { MatchType = "ExactName", Pattern = "event-stream" };
    var contains = new PluginRule { MatchType = "ContainsName", Pattern = "stream" };
    var ecosystem = new PluginRule { MatchType = "Ecosystem", Pattern = "npm" };

    Expect(exact.Matches(component), "ExactName rule should match component name", failures);
    Expect(contains.Matches(component), "ContainsName rule should match component name", failures);
    Expect(ecosystem.Matches(component), "Ecosystem rule should match component ecosystem", failures);
}

static void Expect(bool condition, string message, List<string> failures)
{
    if (!condition)
    {
        failures.Add(message);
    }
}
