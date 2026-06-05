namespace Bootstrap.Tests;

/// <summary>Locates the repository root by walking up to the directory containing global.json.</summary>
internal static class RepoRoot
{
    public static string Path()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(System.IO.Path.Combine(dir.FullName, "global.json")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName
            ?? throw new DirectoryNotFoundException("Repository root (global.json) was not found.");
    }
}
