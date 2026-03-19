using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ShadowGuard;

public sealed class ProjectScanner
{
    private static readonly HashSet<string> IgnoredDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".hg",
        ".svn",
        ".vs",
        ".idea",
        ".vscode",
        "node_modules",
        "vendor",
        "packages",
        "bin",
        "obj",
        "dist",
        "build",
        "target",
        "coverage",
        ".next",
        ".turbo",
        ".venv",
        "venv",
        "__pycache__"
    };

    public List<DependencyComponent> DiscoverComponents(string targetPath)
    {
        var components = new Dictionary<string, DependencyComponent>(StringComparer.OrdinalIgnoreCase);

        ScanPackageJson(targetPath, components);
        ScanPackageLock(targetPath, components);
        ScanYarnLockFiles(targetPath, components);
        ScanPnpmLockFiles(targetPath, components);
        ScanRequirements(targetPath, components);
        ScanGoModFiles(targetPath, components);
        ScanCargoTomlFiles(targetPath, components);
        ScanComposerJsonFiles(targetPath, components);
        ScanPomFiles(targetPath, components);
        ScanCsprojFiles(targetPath, components);

        return components.Values
            .OrderBy(component => component.Ecosystem, StringComparer.OrdinalIgnoreCase)
            .ThenBy(component => component.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void ScanPackageJson(string targetPath, IDictionary<string, DependencyComponent> components)
    {
        foreach (var file in EnumerateManifestFiles(targetPath, "package.json"))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(file));
                var root = document.RootElement;
                ParseNpmDependencyBlock(root, "dependencies", file, true, components);
                ParseNpmDependencyBlock(root, "devDependencies", file, true, components);
                ParseNpmDependencyBlock(root, "optionalDependencies", file, true, components);
                ParseNpmDependencyBlock(root, "peerDependencies", file, true, components);
            }
            catch
            {
            }
        }
    }

    private static void ParseNpmDependencyBlock(JsonElement root, string propertyName, string evidenceFile, bool isDirect, IDictionary<string, DependencyComponent> components)
    {
        if (!root.TryGetProperty(propertyName, out var block) || block.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var dependency in block.EnumerateObject())
        {
            AddOrUpdateComponent(
                components,
                new DependencyComponent
                {
                    Name = dependency.Name,
                    Version = dependency.Value.GetString() ?? "unknown",
                    Ecosystem = "npm",
                    IsDirect = isDirect,
                    SourceType = InferSourceType(dependency.Value.GetString()),
                    EvidenceFiles = new List<string> { evidenceFile }
                });
        }
    }

    private static void ScanPackageLock(string targetPath, IDictionary<string, DependencyComponent> components)
    {
        foreach (var file in EnumerateManifestFiles(targetPath, "package-lock.json"))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(file));
                var root = document.RootElement;

                if (root.TryGetProperty("packages", out var packages) && packages.ValueKind == JsonValueKind.Object)
                {
                    foreach (var package in packages.EnumerateObject())
                    {
                        if (string.IsNullOrWhiteSpace(package.Name))
                        {
                            continue;
                        }

                        var name = ExtractPackageName(package.Name);
                        var version = TryGetString(package.Value, "version");
                        if (string.IsNullOrWhiteSpace(version))
                        {
                            version = "unknown";
                        }

                        var resolved = TryGetString(package.Value, "resolved");
                        AddOrUpdateComponent(
                            components,
                            new DependencyComponent
                            {
                                Name = name,
                                Version = version,
                                Ecosystem = "npm",
                                IsDirect = IsTopLevelNodeModule(package.Name),
                                SourceType = InferSourceType(resolved),
                                ResolvedLocation = resolved,
                                EvidenceFiles = new List<string> { file }
                            });
                    }

                    continue;
                }

                if (root.TryGetProperty("dependencies", out var dependencies) && dependencies.ValueKind == JsonValueKind.Object)
                {
                    ParseLegacyPackageLockDependencies(dependencies, file, true, components);
                }
            }
            catch
            {
            }
        }
    }

    private static void ParseLegacyPackageLockDependencies(JsonElement dependencies, string evidenceFile, bool topLevel, IDictionary<string, DependencyComponent> components)
    {
        foreach (var dependency in dependencies.EnumerateObject())
        {
            var version = TryGetString(dependency.Value, "version");
            if (string.IsNullOrWhiteSpace(version))
            {
                version = "unknown";
            }

            var resolved = TryGetString(dependency.Value, "resolved");
            AddOrUpdateComponent(
                components,
                new DependencyComponent
                {
                    Name = dependency.Name,
                    Version = version,
                    Ecosystem = "npm",
                    IsDirect = topLevel,
                    SourceType = InferSourceType(resolved),
                    ResolvedLocation = resolved,
                    EvidenceFiles = new List<string> { evidenceFile }
                });

            if (dependency.Value.TryGetProperty("dependencies", out var nested) && nested.ValueKind == JsonValueKind.Object)
            {
                ParseLegacyPackageLockDependencies(nested, evidenceFile, false, components);
            }
        }
    }

    private static void ScanYarnLockFiles(string targetPath, IDictionary<string, DependencyComponent> components)
    {
        foreach (var file in EnumerateManifestFiles(targetPath, "yarn.lock"))
        {
            string? currentName = null;
            string version = string.Empty;
            string resolved = string.Empty;

            void CommitCurrent()
            {
                if (string.IsNullOrWhiteSpace(currentName))
                {
                    return;
                }

                AddOrUpdateComponent(
                    components,
                    new DependencyComponent
                    {
                        Name = currentName,
                        Version = string.IsNullOrWhiteSpace(version) ? "unknown" : version,
                        Ecosystem = "npm",
                        IsDirect = false,
                        SourceType = InferSourceType(string.IsNullOrWhiteSpace(resolved) ? version : resolved),
                        ResolvedLocation = resolved,
                        EvidenceFiles = new List<string> { file }
                    });
            }

            foreach (var rawLine in File.ReadAllLines(file))
            {
                var line = rawLine.TrimEnd();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                {
                    continue;
                }

                if (!char.IsWhiteSpace(rawLine[0]) && line.EndsWith(':'))
                {
                    CommitCurrent();
                    currentName = ExtractYarnPackageName(line.TrimEnd(':'));
                    version = string.Empty;
                    resolved = string.Empty;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(currentName))
                {
                    continue;
                }

                var trimmed = line.Trim();
                if (trimmed.StartsWith("version ", StringComparison.OrdinalIgnoreCase))
                {
                    version = ExtractQuotedValue(trimmed);
                }
                else if (trimmed.StartsWith("resolved ", StringComparison.OrdinalIgnoreCase))
                {
                    resolved = ExtractQuotedValue(trimmed);
                }
            }

            CommitCurrent();
        }
    }

    private static void ScanPnpmLockFiles(string targetPath, IDictionary<string, DependencyComponent> components)
    {
        foreach (var file in EnumerateManifestFiles(targetPath, "pnpm-lock.yaml"))
        {
            var inPackagesSection = false;

            foreach (var rawLine in File.ReadAllLines(file))
            {
                var line = rawLine.TrimEnd();
                var trimmed = line.Trim();

                if (string.Equals(trimmed, "packages:", StringComparison.OrdinalIgnoreCase))
                {
                    inPackagesSection = true;
                    continue;
                }

                if (inPackagesSection && !rawLine.StartsWith(" ", StringComparison.Ordinal) && trimmed.EndsWith(':'))
                {
                    inPackagesSection = false;
                }

                if (!inPackagesSection)
                {
                    continue;
                }

                var match = Regex.Match(rawLine, "^  (?<key>.+):\\s*$");
                if (!match.Success)
                {
                    continue;
                }

                var packageEntry = ParsePnpmPackageEntry(match.Groups["key"].Value);
                if (packageEntry is null)
                {
                    continue;
                }

                AddOrUpdateComponent(
                    components,
                    new DependencyComponent
                    {
                        Name = packageEntry.Value.Name,
                        Version = packageEntry.Value.Version,
                        Ecosystem = "npm",
                        IsDirect = false,
                        SourceType = InferSourceType(packageEntry.Value.Version),
                        EvidenceFiles = new List<string> { file }
                    });
            }
        }
    }

    private static void ScanRequirements(string targetPath, IDictionary<string, DependencyComponent> components)
    {
        foreach (var file in EnumerateManifestFiles(targetPath, "requirements*.txt"))
        {
            foreach (var rawLine in File.ReadAllLines(file))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("-"))
                {
                    continue;
                }

                var component = ParseRequirementLine(line, file);
                if (component is not null)
                {
                    AddOrUpdateComponent(components, component);
                }
            }
        }
    }

    private static DependencyComponent? ParseRequirementLine(string line, string evidenceFile)
    {
        var pep508 = Regex.Match(line, @"^(?<name>[A-Za-z0-9_.\-]+)\s*@\s*(?<source>.+)$");
        if (pep508.Success)
        {
            return new DependencyComponent
            {
                Name = pep508.Groups["name"].Value,
                Version = pep508.Groups["source"].Value,
                Ecosystem = "pip",
                IsDirect = true,
                SourceType = InferSourceType(pep508.Groups["source"].Value),
                EvidenceFiles = new List<string> { evidenceFile }
            };
        }

        var versionMatch = Regex.Match(line, @"^(?<name>[A-Za-z0-9_.\-]+)\s*(?<operator>==|>=|<=|~=|>|<)?\s*(?<version>.*)$");
        if (!versionMatch.Success)
        {
            return null;
        }

        var version = string.IsNullOrWhiteSpace(versionMatch.Groups["version"].Value)
            ? "unspecified"
            : versionMatch.Groups["version"].Value;

        return new DependencyComponent
        {
            Name = versionMatch.Groups["name"].Value,
            Version = version,
            Ecosystem = "pip",
            IsDirect = true,
            SourceType = InferSourceType(version),
            EvidenceFiles = new List<string> { evidenceFile }
        };
    }

    private static void ScanGoModFiles(string targetPath, IDictionary<string, DependencyComponent> components)
    {
        foreach (var file in EnumerateManifestFiles(targetPath, "go.mod"))
        {
            var inRequireBlock = false;
            foreach (var rawLine in File.ReadAllLines(file))
            {
                var trimmed = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(trimmed, "require (", StringComparison.Ordinal))
                {
                    inRequireBlock = true;
                    continue;
                }

                if (inRequireBlock && string.Equals(trimmed, ")", StringComparison.Ordinal))
                {
                    inRequireBlock = false;
                    continue;
                }

                if (trimmed.StartsWith("require ", StringComparison.Ordinal) && !string.Equals(trimmed, "require (", StringComparison.Ordinal))
                {
                    var component = ParseGoRequireLine(trimmed["require ".Length..], file, rawLine.Contains("indirect", StringComparison.OrdinalIgnoreCase));
                    if (component is not null)
                    {
                        AddOrUpdateComponent(components, component);
                    }

                    continue;
                }

                if (inRequireBlock)
                {
                    var component = ParseGoRequireLine(trimmed, file, rawLine.Contains("indirect", StringComparison.OrdinalIgnoreCase));
                    if (component is not null)
                    {
                        AddOrUpdateComponent(components, component);
                    }
                }
            }
        }
    }

    private static DependencyComponent? ParseGoRequireLine(string line, string evidenceFile, bool indirect)
    {
        var clean = line.Split("//", 2, StringSplitOptions.None)[0].Trim();
        var parts = Regex.Split(clean, @"\s+");
        if (parts.Length < 2)
        {
            return null;
        }

        return new DependencyComponent
        {
            Name = parts[0],
            Version = parts[1],
            Ecosystem = "golang",
            IsDirect = !indirect,
            SourceType = InferSourceType(parts[1]),
            EvidenceFiles = new List<string> { evidenceFile }
        };
    }

    private static void ScanCargoTomlFiles(string targetPath, IDictionary<string, DependencyComponent> components)
    {
        foreach (var file in EnumerateManifestFiles(targetPath, "Cargo.toml"))
        {
            string currentSection = string.Empty;
            foreach (var rawLine in File.ReadAllLines(file))
            {
                var trimmed = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                {
                    continue;
                }

                if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
                {
                    currentSection = trimmed;
                    continue;
                }

                if (!IsCargoDependencySection(currentSection) || !trimmed.Contains('=') || trimmed.StartsWith("version =", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var component = ParseCargoDependencyLine(trimmed, file);
                if (component is not null)
                {
                    AddOrUpdateComponent(components, component);
                }
            }
        }
    }

    private static DependencyComponent? ParseCargoDependencyLine(string line, string evidenceFile)
    {
        var parts = line.Split('=', 2);
        if (parts.Length != 2)
        {
            return null;
        }

        var name = parts[0].Trim();
        var value = parts[1].Split('#', 2)[0].Trim();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string version;
        string source = value;

        if (value.StartsWith('"'))
        {
            version = ExtractQuotedValue(value);
        }
        else
        {
            version = Regex.Match(value, "version\\s*=\\s*\"(?<version>[^\"]+)\"").Groups["version"].Value;
            var gitSource = Regex.Match(value, "git\\s*=\\s*\"(?<git>[^\"]+)\"").Groups["git"].Value;
            var pathSource = Regex.Match(value, "path\\s*=\\s*\"(?<path>[^\"]+)\"").Groups["path"].Value;
            source = !string.IsNullOrWhiteSpace(gitSource) ? gitSource : !string.IsNullOrWhiteSpace(pathSource) ? pathSource : value;
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            version = source;
        }

        return new DependencyComponent
        {
            Name = name,
            Version = version,
            Ecosystem = "cargo",
            IsDirect = true,
            SourceType = InferSourceType(source),
            ResolvedLocation = source,
            EvidenceFiles = new List<string> { evidenceFile }
        };
    }

    private static bool IsCargoDependencySection(string sectionHeader)
    {
        var normalized = sectionHeader.Trim().ToLowerInvariant();
        return normalized is "[dependencies]" or "[dev-dependencies]" or "[build-dependencies]"
            || normalized.EndsWith(".dependencies]", StringComparison.Ordinal)
            || normalized.EndsWith(".dev-dependencies]", StringComparison.Ordinal)
            || normalized.EndsWith(".build-dependencies]", StringComparison.Ordinal);
    }

    private static void ScanComposerJsonFiles(string targetPath, IDictionary<string, DependencyComponent> components)
    {
        foreach (var file in EnumerateManifestFiles(targetPath, "composer.json"))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(file));
                var root = document.RootElement;
                ParseComposerDependencyBlock(root, "require", file, components);
                ParseComposerDependencyBlock(root, "require-dev", file, components);
            }
            catch
            {
            }
        }
    }

    private static void ParseComposerDependencyBlock(JsonElement root, string propertyName, string evidenceFile, IDictionary<string, DependencyComponent> components)
    {
        if (!root.TryGetProperty(propertyName, out var block) || block.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var dependency in block.EnumerateObject())
        {
            if (ShouldIgnoreComposerPackage(dependency.Name))
            {
                continue;
            }

            AddOrUpdateComponent(
                components,
                new DependencyComponent
                {
                    Name = dependency.Name,
                    Version = dependency.Value.GetString() ?? "unknown",
                    Ecosystem = "composer",
                    IsDirect = true,
                    SourceType = InferSourceType(dependency.Value.GetString()),
                    EvidenceFiles = new List<string> { evidenceFile }
                });
        }
    }

    private static bool ShouldIgnoreComposerPackage(string packageName)
    {
        return string.Equals(packageName, "php", StringComparison.OrdinalIgnoreCase)
            || packageName.StartsWith("ext-", StringComparison.OrdinalIgnoreCase)
            || packageName.StartsWith("lib-", StringComparison.OrdinalIgnoreCase)
            || string.Equals(packageName, "composer-plugin-api", StringComparison.OrdinalIgnoreCase)
            || string.Equals(packageName, "composer-runtime-api", StringComparison.OrdinalIgnoreCase);
    }

    private static void ScanPomFiles(string targetPath, IDictionary<string, DependencyComponent> components)
    {
        foreach (var file in EnumerateManifestFiles(targetPath, "pom.xml"))
        {
            try
            {
                var document = XDocument.Load(file);
                var dependencies = document.Descendants().Where(node => node.Name.LocalName == "dependency");
                foreach (var dependency in dependencies)
                {
                    var groupId = dependency.Elements().FirstOrDefault(node => node.Name.LocalName == "groupId")?.Value ?? string.Empty;
                    var artifactId = dependency.Elements().FirstOrDefault(node => node.Name.LocalName == "artifactId")?.Value ?? string.Empty;
                    var version = dependency.Elements().FirstOrDefault(node => node.Name.LocalName == "version")?.Value ?? "unspecified";
                    if (string.IsNullOrWhiteSpace(artifactId))
                    {
                        continue;
                    }

                    AddOrUpdateComponent(
                        components,
                        new DependencyComponent
                        {
                            Name = string.IsNullOrWhiteSpace(groupId) ? artifactId : $"{groupId}:{artifactId}",
                            Version = version,
                            Ecosystem = "maven",
                            IsDirect = true,
                            EvidenceFiles = new List<string> { file }
                        });
                }
            }
            catch
            {
            }
        }
    }

    private static void ScanCsprojFiles(string targetPath, IDictionary<string, DependencyComponent> components)
    {
        foreach (var file in EnumerateManifestFiles(targetPath, "*.csproj"))
        {
            try
            {
                var document = XDocument.Load(file);
                var references = document.Descendants().Where(node => node.Name.LocalName == "PackageReference");
                foreach (var reference in references)
                {
                    var include = reference.Attribute("Include")?.Value ?? reference.Attribute("Update")?.Value ?? string.Empty;
                    var version = reference.Attribute("Version")?.Value ?? reference.Elements().FirstOrDefault(node => node.Name.LocalName == "Version")?.Value ?? "unspecified";
                    if (string.IsNullOrWhiteSpace(include))
                    {
                        continue;
                    }

                    AddOrUpdateComponent(
                        components,
                        new DependencyComponent
                        {
                            Name = include,
                            Version = version,
                            Ecosystem = "nuget",
                            IsDirect = true,
                            EvidenceFiles = new List<string> { file }
                        });
                }
            }
            catch
            {
            }
        }
    }

    private static void AddOrUpdateComponent(IDictionary<string, DependencyComponent> components, DependencyComponent candidate)
    {
        candidate.BomReference = HashUtility.CreateBomReference(candidate.Name, candidate.Version, candidate.Ecosystem);
        candidate.PackageUrl = BuildPackageUrl(candidate);

        var key = $"{candidate.Ecosystem}:{candidate.Name}:{candidate.Version}";
        if (!components.TryGetValue(key, out var existing))
        {
            components[key] = candidate;
            return;
        }

        existing.IsDirect = existing.IsDirect || candidate.IsDirect;
        if (string.Equals(existing.SourceType, "Registry", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(candidate.SourceType))
        {
            existing.SourceType = candidate.SourceType;
        }

        if (string.IsNullOrWhiteSpace(existing.ResolvedLocation))
        {
            existing.ResolvedLocation = candidate.ResolvedLocation;
        }

        foreach (var evidenceFile in candidate.EvidenceFiles)
        {
            if (!existing.EvidenceFiles.Contains(evidenceFile, StringComparer.OrdinalIgnoreCase))
            {
                existing.EvidenceFiles.Add(evidenceFile);
            }
        }
    }

    private static string ExtractPackageName(string packagePath)
    {
        var normalized = packagePath.Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var index = segments.Length - 1; index >= 0; index--)
        {
            if (!string.Equals(segments[index], "node_modules", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index + 1 >= segments.Length)
            {
                break;
            }

            if (segments[index + 1].StartsWith("@", StringComparison.OrdinalIgnoreCase) && index + 2 < segments.Length)
            {
                return $"{segments[index + 1]}/{segments[index + 2]}";
            }

            return segments[index + 1];
        }

        return packagePath;
    }

    private static string ExtractYarnPackageName(string descriptor)
    {
        var firstDescriptor = descriptor.Split(',', StringSplitOptions.RemoveEmptyEntries)[0].Trim().Trim('"', '\'');
        if (firstDescriptor.Contains("@npm:", StringComparison.OrdinalIgnoreCase))
        {
            return firstDescriptor[..firstDescriptor.IndexOf("@npm:", StringComparison.OrdinalIgnoreCase)];
        }

        var atIndex = firstDescriptor.LastIndexOf('@');
        if (atIndex > 0)
        {
            return firstDescriptor[..atIndex];
        }

        return firstDescriptor;
    }

    private static (string Name, string Version)? ParsePnpmPackageEntry(string entry)
    {
        var normalized = entry.Trim().Trim('"', '\'');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.StartsWith('/'))
        {
            normalized = normalized[1..];
            if (normalized.StartsWith('@') && normalized.Contains('/'))
            {
                var lastSlash = normalized.LastIndexOf('/');
                if (lastSlash > 0 && lastSlash < normalized.Length - 1)
                {
                    return (normalized[..lastSlash], StripPeerSuffix(normalized[(lastSlash + 1)..]));
                }
            }
        }

        var lastAt = normalized.LastIndexOf('@');
        if (lastAt <= 0 || lastAt >= normalized.Length - 1)
        {
            return null;
        }

        return (normalized[..lastAt], StripPeerSuffix(normalized[(lastAt + 1)..]));
    }

    private static string StripPeerSuffix(string version)
    {
        var parenIndex = version.IndexOf('(');
        return parenIndex >= 0 ? version[..parenIndex] : version;
    }

    private static string ExtractQuotedValue(string text)
    {
        var match = Regex.Match(text, "\"(?<value>[^\"]+)\"");
        return match.Success ? match.Groups["value"].Value : text;
    }

    private static bool IsTopLevelNodeModule(string packagePath)
    {
        var normalized = packagePath.Replace('\\', '/').Trim('/');
        if (!normalized.StartsWith("node_modules/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var remaining = normalized["node_modules/".Length..];
        return !remaining.Contains("/node_modules/", StringComparison.OrdinalIgnoreCase);
    }

    // Custom traversal keeps the scan focused on the user's project instead of
    // recursively reading downloaded dependencies or generated build output.
    private static IEnumerable<string> EnumerateManifestFiles(string targetPath, string searchPattern)
    {
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(targetPath);

        while (pendingDirectories.Count > 0)
        {
            var currentDirectory = pendingDirectories.Pop();
            IEnumerable<string> files;

            try
            {
                files = Directory.EnumerateFiles(currentDirectory, searchPattern, SearchOption.TopDirectoryOnly);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }

            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(currentDirectory, "*", SearchOption.TopDirectoryOnly);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var directory in directories)
            {
                if (ShouldSkipDirectory(directory))
                {
                    continue;
                }

                pendingDirectories.Push(directory);
            }
        }
    }

    private static bool ShouldSkipDirectory(string directoryPath)
    {
        var directoryName = Path.GetFileName(directoryPath);
        if (string.IsNullOrWhiteSpace(directoryName))
        {
            return false;
        }

        if (IgnoredDirectoryNames.Contains(directoryName))
        {
            return true;
        }

        try
        {
            return (File.GetAttributes(directoryPath) & FileAttributes.ReparsePoint) != 0;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static string TryGetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string InferSourceType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Registry";
        }

        if (value.Contains("git+", StringComparison.OrdinalIgnoreCase) || value.Contains("github.com", StringComparison.OrdinalIgnoreCase) || value.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            return "Git";
        }

        if (value.Contains("http://", StringComparison.OrdinalIgnoreCase) || value.Contains("https://", StringComparison.OrdinalIgnoreCase))
        {
            return "Url";
        }

        if (value.Contains("file:", StringComparison.OrdinalIgnoreCase) || value.Contains("../", StringComparison.OrdinalIgnoreCase) || value.Contains("./", StringComparison.OrdinalIgnoreCase))
        {
            return "Local";
        }

        return "Registry";
    }

    private static string BuildPackageUrl(DependencyComponent component)
    {
        return component.Ecosystem switch
        {
            "npm" => $"pkg:npm/{component.Name}@{component.Version}",
            "pip" => $"pkg:pypi/{component.Name}@{component.Version}",
            "maven" => BuildMavenPurl(component),
            "nuget" => $"pkg:nuget/{component.Name}@{component.Version}",
            "golang" => $"pkg:golang/{component.Name}@{component.Version}",
            "cargo" => $"pkg:cargo/{component.Name}@{component.Version}",
            "composer" => $"pkg:composer/{component.Name}@{component.Version}",
            _ => $"pkg:generic/{component.Name}@{component.Version}"
        };
    }

    private static string BuildMavenPurl(DependencyComponent component)
    {
        if (!component.Name.Contains(':', StringComparison.Ordinal))
        {
            return $"pkg:maven/{component.Name}@{component.Version}";
        }

        var parts = component.Name.Split(':', 2);
        return $"pkg:maven/{parts[0]}/{parts[1]}@{component.Version}";
    }
}

