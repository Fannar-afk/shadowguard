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
    object payload = options.Format.Equals("sbom", StringComparison.OrdinalIgnoreCase) ? result.Sbom : result;
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
    Console.WriteLine("  shadowguard --path <project-dir> [--plugins <plugin-dir>] [--out <file>] [--format report|sbom]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  -p, --path <dir>          Project directory to scan.");
    Console.WriteLine("      --plugins <dir>      Optional plugin rule directory.");
    Console.WriteLine("  -o, --out <file>         Write JSON output to a file. Defaults to stdout.");
    Console.WriteLine("      --format <format>    Output format: report or sbom. Default: report.");
    Console.WriteLine("      --block-threshold N  Risk score threshold for Block. Default: 70.");
    Console.WriteLine("      --fail-on-block      Return non-zero exit code when the gate is Block.");
    Console.WriteLine("      --fail-on-warn       Return non-zero exit code when the gate is Warn or Block.");
    Console.WriteLine("      --no-block-malicious Disable blocking on malicious or historical high-risk package findings.");
    Console.WriteLine("      --no-block-license   Disable blocking on license risk findings.");
    Console.WriteLine("      --no-warn-source     Disable warning on Git, URL, or local source findings.");
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
    public bool ShowHelp { get; set; }
}
