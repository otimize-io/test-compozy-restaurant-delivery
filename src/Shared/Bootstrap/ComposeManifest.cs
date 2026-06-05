namespace Bootstrap;

/// <summary>
/// Lightweight reader that extracts the top-level service names declared under the
/// <c>services:</c> block of a docker-compose file. Intentionally dependency-free.
/// </summary>
public sealed class ComposeManifest
{
    public IReadOnlyList<string> ServiceNames { get; }

    private ComposeManifest(IReadOnlyList<string> serviceNames) => ServiceNames = serviceNames;

    public static ComposeManifest Load(string path)
    {
        var names = new List<string>();
        var inServices = false;

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.TrimEnd();
            var trimmed = line.TrimStart();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var indent = line.Length - trimmed.Length;

            if (!inServices)
            {
                if (indent == 0 && trimmed.StartsWith("services:"))
                {
                    inServices = true;
                }
                continue;
            }

            // A new top-level key ends the services block.
            if (indent == 0)
            {
                break;
            }

            // Service names are the keys indented exactly one level (two spaces).
            if (indent == 2 && trimmed.EndsWith(':'))
            {
                names.Add(trimmed[..^1].Trim());
            }
        }

        return new ComposeManifest(names);
    }
}
