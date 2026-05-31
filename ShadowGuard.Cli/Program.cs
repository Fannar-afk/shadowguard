using System.Text.Json;
using System.Text.Json.Serialization;
using ShadowGuard;

var options = ParseArgs(args);
if (options.ShowHelp || string.IsNullOrWhiteSpace(options.TargetPath))
{
    PrintHelp();
    return options.ShowHelp ? 0 : 1;
}

try
{
    var policy = new ScanPolicy
    {
        BlockScoreThreshold = options.BlockScoreThreshold,
        BlockOnMalicious = options.BlockOnMalicious,
        BlockOnLicenseRisk = options.BlockOnLicenseRisk,
        WarnOnUnknownSource = options.WarnOnUnknownSource
    };

    var result = new ShadowGuardEngine().Scan(options.TargetPath, policy, options.PluginDirectory);
    SbomValidationResult? sbomValidation = null;
    VulnerabilityScanResult? vulnerabilityScan = null;

    if (options.ValidateSbom)
    {
        sbomValidation = new CycloneDxValidator().Validate(result.Sbom);
        Console.Error.WriteLine(sbomValidation.IsValid
            ? "CycloneDX SBOM validation passed."
            : $"CycloneDX SBOM validation failed. Errors={sbomValidation.Errors.Count}; Warnings={sbomValidation.Warnings.Count}");
    }

    if (options.EnableVulnerabilityLookup)
    {
        IVulnerabilityProvider provider = options.VulnerabilityProvider.Equals("osv", StringComparison.OrdinalIgnoreCase)
            ? new OsvVulnerabilityProvider()
            : throw new ArgumentException("Unsupported vulnerability provider: " + options.VulnerabilityProvider);

        vulnerabilityScan = await provider.QueryAsync(result.Components);
        Console.Error.WriteLine($"Vulnerability lookup completed. Provider={vulnerabilityScan.Provider}; Vulnerabilities={vulnerabilityScan.Vulnerabilities.Count}; Errors={vulnerabilityScan.Errors.Count}");
    }

    object payload = BuildPayload(options, result, sbomValidation, vulnerabilityScan);
    var jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    if (!string.IsNullOrWhiteSpace(options.OutputPath))
    {
        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(options.OutputPath));
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        File.WriteAllText(options.OutputPath, JsonSerializer.Serialize(payload, jsonOptions));
    }
    else
    {
        Console.WriteLine(JsonSerializer.Serialize(payload, jsonOptions));
    }

    Console.Error.WriteLine($"ShadowGuard scan completed. Components={result.Summary.TotalDependencies}; Findings={result.Summary.FindingsCount}; Gate={result.GateDecision.Outcome}");

    if (options.FailOnInvalidSbom && sbomValidation is { IsValid: false })
    {
        return 4;
    }

    if (options.FailOnVulnerability && vulnerabilityScan is not null && vulnerabilityScan.Vulnerabilities.Count > 0)
    {
        return 5;
    }

    if (options.FailOnBlock && result.GateDecision.Outcome == GateOutcome.Block)
    {
        return 2;
    }

    if (options.FailOnWarn && result.GateDecision.Outcome is GateOutcome.Warn or GateOutcome.Block)
    {
        return 3;
    }

    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine("ShadowGuard scan failed: " + exception.Message);
    return 1;
}

static object BuildPayload(CliOptions options, ScanResult result, SbomValidationResult? sbomValidation, VulnerabilityScanResult? vulnerabilityScan)
{
    var format = options.Format.ToLowerInvariant();
    return format switch
    {
        "sbom" => result.Sbom,
        "validation" => sbomValidation ?? new CycloneDxValidator().Validate(result.Sbom),
        "vuln" or "vulnerabilities" => vulnerabilityScan ?? new VulnerabilityScanResult { Provider = options.VulnerabilityProvider },
        "sarif" => BuildSarif(result),
        _ => new ScanCliReport
        {
            Scan = result,
            SbomValidation = sbomValidation,
            VulnerabilityScan = vulnerabilityScan
        }
    };
}

static object BuildSarif(ScanResult result)
{
    var rules = result.Findings
        .GroupBy(finding => string.IsNullOrWhiteSpace(finding.RuleId) ? "shadowguard.finding" : finding.RuleId)
        .Select(group =>
        {
            var sample = group.First();
            return new
            {
                id = group.Key,
                name = string.IsNullOrWhiteSpace(sample.RuleName) ? group.Key : sample.RuleName,
                shortDescription = new { text = string.IsNullOrWhiteSpace(sample.RuleName) ? group.Key : sample.RuleName },
                fullDescription = new { text = string.IsNullOrWhiteSpace(sample.Message) ? "ShadowGuard dependency risk finding." : sample.Message },
                properties = new
                {
                    category = sample.Category,
                    severity = sample.Severity.ToString(),
                    maxScore = group.Max(item => item.Score)
                }
            };
        })
        .ToArray();

    var findings = result.Findings
        .Select(finding => new
        {
            ruleId = string.IsNullOrWhiteSpace(finding.RuleId) ? "shadowguard.finding" : finding.RuleId,
            level = ToSarifLevel(finding.Severity),
            message = new { text = BuildSarifMessage(finding) },
            locations = new[]
            {
                new
                {
                    physicalLocation = new
                    {
                        artifactLocation = new
                        {
                            uri = ResolvePrimarySourceFile(result, finding)
                        },
                        region = new
                        {
                            startLine = 1
                        }
                    }
                }
            },
            properties = new
            {
                dependencyName = finding.DependencyName,
                ecosystem = finding.Ecosystem,
                category = finding.Category,
                severity = finding.Severity.ToString(),
                score = finding.Score,
                recommendation = finding.Recommendation,
                sourceFiles = finding.SourceFiles
            }
        })
        .ToArray();

    return new Dictionary<string, object>
    {
        ["version"] = "2.1.0",
        ["$schema"] = "https://json.schemastore.org/sarif-2.1.0.json",
        ["runs"] = new[]
        {
            new
            {
                tool = new
                {
                    driver = new
                    {
                        name = "ShadowGuard",
                        informationUri = "https://github.com/Fannar-afk/shadowguard",
                        rules
                    }
                },
                automationDetails = new
                {
                    id = "shadowguard/dependency-risk-scan"
                },
                results = findings
            }
        }
    };
}

static string BuildSarifMessage(Finding finding)
{
    var dependency = string.IsNullOrWhiteSpace(finding.DependencyName) ? "dependency" : finding.DependencyName;
    var message = string.IsNullOrWhiteSpace(finding.Message) ? finding.RuleName : finding.Message;
    var recommendation = string.IsNullOrWhiteSpace(finding.Recommendation) ? string.Empty : " Recommendation: " + finding.Recommendation;
    return $"{dependency}: {message}{recommendation}";
}

static string ResolvePrimarySourceFile(ScanResult result, Finding finding)
{
    var source = finding.SourceFiles
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .FirstOrDefault();

    if (string.IsNullOrWhiteSpace(source))
    {
        source = result.TargetPath;
    }

    return source.Replace('\\', '/');
}

static string ToSarifLevel(SeverityLevel severity)
{
    return severity switch
    {
        SeverityLevel.Critical or SeverityLevel.High => "error",
        SeverityLevel.Medium => "warning",
        SeverityLevel.Low => "note",
        _ => "none"
    };
}

static CliOptions ParseArgs(string[] args)
{
    var options = new CliOptions();
    for (var index = 0; index < args.Length; index++)
    {
        var arg = args[index];
        switch (arg)
        {
            case "--help":
            case "-h":
                options.ShowHelp = true;
                break;
            case "--path":
            case "-p":
                options.TargetPath = ReadValue(args, ref index, arg);
                break;
            case "--plugins":
                options.PluginDirectory = ReadValue(args, ref index, arg);
                break;
            case "--out":
            case "-o":
                options.OutputPath = ReadValue(args, ref index, arg);
                break;
            case "--format":
                options.Format = ReadValue(args, ref index, arg);
                break;
            case "--validate-sbom":
                options.ValidateSbom = true;
                break;
            case "--fail-on-invalid-sbom":
                options.ValidateSbom = true;
                options.FailOnInvalidSbom = true;
                break;
            case "--vuln":
            case "--vulnerability":
                options.EnableVulnerabilityLookup = true;
                break;
            case "--vuln-provider":
                options.VulnerabilityProvider = ReadValue(args, ref index, arg);
                break;
            case "--fail-on-vulnerability":
                options.EnableVulnerabilityLookup = true;
                options.FailOnVulnerability = true;
                break;
            case "--block-threshold":
                if (int.TryParse(ReadValue(args, ref index, arg), out var threshold))
                {
                    options.BlockScoreThreshold = threshold;
                }
                break;
            case "--fail-on-block":
                options.FailOnBlock = true;
                break;
            case "--fail-on-warn":
                options.FailOnWarn = true;
                break;
            case "--no-block-malicious":
                options.BlockOnMalicious = false;
                break;
            case "--no-block-license":
                options.BlockOnLicenseRisk = false;
                break;
            case "--no-warn-source":
                options.WarnOnUnknownSource = false;
                break;
            default:
                if (string.IsNullOrWhiteSpace(options.TargetPath))
                {
                    options.TargetPath = arg;
                }
                break;
        }
    }

    return options;
}

static string ReadValue(string[] args, ref int index, string optionName)
{
    if (index + 1 >= args.Length)
    {
        throw new ArgumentException($"Missing value for {optionName}");
    }
    index++;
    return args[index];
}

static void PrintHelp()
{
    Console.WriteLine("ShadowGuard CLI");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  shadowguard-cli --path <project-dir> [--plugins <plugin-dir>] [--out <file>] [--format report|sbom|validation|vuln|sarif]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  -p, --path <dir>             Project directory to scan.");
    Console.WriteLine("      --plugins <dir>         Optional plugin rule directory.");
    Console.WriteLine("  -o, --out <file>            Write JSON output to a file. Defaults to stdout.");
    Console.WriteLine("      --format <format>       Output format: report, sbom, validation, vuln, or sarif. Default: report.");
    Console.WriteLine("      --validate-sbom         Validate generated CycloneDX SBOM structure and required fields.");
    Console.WriteLine("      --fail-on-invalid-sbom  Return non-zero exit code when SBOM validation fails.");
    Console.WriteLine("      --vuln                  Query vulnerability data. Currently supports OSV.");
    Console.WriteLine("      --vuln-provider <name>  Vulnerability provider. Default: osv.");
    Console.WriteLine("      --fail-on-vulnerability Return non-zero exit code when vulnerabilities are found.");
    Console.WriteLine("      --block-threshold N     Risk score threshold for Block. Default: 70.");
    Console.WriteLine("      --fail-on-block         Return non-zero exit code when the gate is Block.");
    Console.WriteLine("      --fail-on-warn          Return non-zero exit code when the gate is Warn or Block.");
    Console.WriteLine("      --no-block-malicious    Disable blocking on malicious or historical high-risk package findings.");
    Console.WriteLine("      --no-block-license      Disable blocking on license risk findings.");
    Console.WriteLine("      --no-warn-source        Disable warning on Git, URL, or local source findings.");
}

sealed class CliOptions
{
    public string TargetPath { get; set; } = string.Empty;
    public string PluginDirectory { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public string Format { get; set; } = "report";
    public int BlockScoreThreshold { get; set; } = 70;
    public bool BlockOnMalicious { get; set; } = true;
    public bool BlockOnLicenseRisk { get; set; } = true;
    public bool WarnOnUnknownSource { get; set; } = true;
    public bool FailOnBlock { get; set; }
    public bool FailOnWarn { get; set; }
    public bool ValidateSbom { get; set; }
    public bool FailOnInvalidSbom { get; set; }
    public bool EnableVulnerabilityLookup { get; set; }
    public string VulnerabilityProvider { get; set; } = "osv";
    public bool FailOnVulnerability { get; set; }
    public bool ShowHelp { get; set; }
}

sealed class ScanCliReport
{
    public ScanResult Scan { get; set; } = new();
    public SbomValidationResult? SbomValidation { get; set; }
    public VulnerabilityScanResult? VulnerabilityScan { get; set; }
}
