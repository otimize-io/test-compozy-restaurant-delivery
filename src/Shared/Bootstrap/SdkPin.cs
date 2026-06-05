using System.Text.Json;

namespace Bootstrap;

/// <summary>Reads the .NET SDK version pinned in <c>global.json</c>.</summary>
public sealed class SdkPin
{
    public string Version { get; }

    public int Major => int.Parse(Version.Split('.')[0]);

    private SdkPin(string version) => Version = version;

    public static SdkPin Read(string globalJsonPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(globalJsonPath));
        var version = doc.RootElement.GetProperty("sdk").GetProperty("version").GetString();
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new InvalidDataException("global.json is missing sdk.version");
        }
        return new SdkPin(version);
    }
}
