using System.Text.RegularExpressions;

namespace ShadowGuard;

public sealed class SbomValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public sealed class CycloneDxValidator
{
    private static readonly Regex UrnUuidPattern = new("^urn:uuid:[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", RegexOptions.Compiled);
    private static readonly HashSet<string> SupportedComponentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application",
        "framework",
        "library",
        "container",
        "operating-system",
        "device",
        "firmware",
        "file",
        "machine-learning-model",
        "data"
    };

    private static readonly HashSet<string> SupportedScopes = new(StringComparer.OrdinalIgnoreCase)
    {
        "required",
        "optional",
        "excluded"
    };

    public SbomValidationResult Validate(SbomDocument document)
    {
        var result = new SbomValidationResult();

        if (!string.Equals(document.BomFormat, "CycloneDX", StringComparison.Ordinal))
        {
            result.Errors.Add("bomFormat must be CycloneDX.");
        }

        if (!string.Equals(document.SpecVersion, "1.5", StringComparison.Ordinal))
        {
            result.Errors.Add("specVersion must be 1.5.");
        }

        if (document.Version < 1)
        {
            result.Errors.Add("version must be greater than or equal to 1.");
        }

        if (string.IsNullOrWhiteSpace(document.SerialNumber))
        {
            result.Warnings.Add("serialNumber is missing. CycloneDX recommends a BOM serial number.");
        }
        else if (!UrnUuidPattern.IsMatch(document.SerialNumber))
        {
            result.Warnings.Add("serialNumber should use the urn:uuid format recommended by CycloneDX.");
        }

        ValidateMetadata(document.Metadata, result);
        ValidateComponents(document.Components, result);

        return result;
    }

    private static void ValidateMetadata(SbomMetadata metadata, SbomValidationResult result)
    {
        if (metadata is null)
        {
            result.Warnings.Add("metadata is missing.");
            return;
        }

        if (string.IsNullOrWhiteSpace(metadata.Timestamp))
        {
            result.Warnings.Add("metadata.timestamp is missing.");
        }
        else if (!DateTimeOffset.TryParse(metadata.Timestamp, out _))
        {
            result.Warnings.Add("metadata.timestamp should be an ISO 8601 timestamp.");
        }

        if (string.IsNullOrWhiteSpace(metadata.ProjectName))
        {
            result.Warnings.Add("metadata.projectName is missing.");
        }
    }

    private static void ValidateComponents(IReadOnlyCollection<SbomComponent> components, SbomValidationResult result)
    {
        if (components is null)
        {
            result.Errors.Add("components must not be null.");
            return;
        }

        var bomRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var component in components)
        {
            ValidateComponent(component, result, bomRefs);
        }
    }

    private static void ValidateComponent(SbomComponent component, SbomValidationResult result, HashSet<string> bomRefs)
    {
        if (component is null)
        {
            result.Errors.Add("components must not contain null entries.");
            return;
        }

        var label = string.IsNullOrWhiteSpace(component.Name) ? "<unnamed>" : component.Name;

        if (string.IsNullOrWhiteSpace(component.Type))
        {
            result.Errors.Add($"component {label}: type is required.");
        }
        else if (!SupportedComponentTypes.Contains(component.Type))
        {
            result.Errors.Add($"component {label}: unsupported type '{component.Type}'.");
        }

        if (string.IsNullOrWhiteSpace(component.Name))
        {
            result.Errors.Add("component name is required.");
        }

        if (string.IsNullOrWhiteSpace(component.Version))
        {
            result.Warnings.Add($"component {label}: version is missing.");
        }

        if (string.IsNullOrWhiteSpace(component.BomRef))
        {
            result.Warnings.Add($"component {label}: bom-ref is missing.");
        }
        else if (!bomRefs.Add(component.BomRef))
        {
            result.Errors.Add($"component {label}: duplicate bom-ref '{component.BomRef}'.");
        }

        if (!string.IsNullOrWhiteSpace(component.Purl) && !component.Purl.StartsWith("pkg:", StringComparison.OrdinalIgnoreCase))
        {
            result.Warnings.Add($"component {label}: purl should start with 'pkg:'.");
        }

        if (!string.IsNullOrWhiteSpace(component.Scope) && !SupportedScopes.Contains(component.Scope))
        {
            result.Errors.Add($"component {label}: unsupported scope '{component.Scope}'.");
        }
    }
}
