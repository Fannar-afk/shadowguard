using System.IO;

namespace ShadowGuard;

public static class WorkspaceLocator
{
    public static string ResolveOrCreateDirectory(string folderName)
    {
        var directory = ResolveDirectory(folderName);
        Directory.CreateDirectory(directory);
        return directory;
    }

    public static string ResolveDirectory(string folderName)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, folderName);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, folderName);
    }

    public static string ResolvePath(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, Path.Combine(segments));
            if (File.Exists(candidate) || Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, Path.Combine(segments));
    }
}
