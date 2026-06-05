using Bootstrap;

namespace Bootstrap.Tests;

public class SdkPinTests
{
    [Fact]
    public void Read_pins_the_dotnet_sdk_version()
    {
        var pin = SdkPin.Read(Path.Combine(RepoRoot.Path(), "global.json"));

        Assert.Equal("10.0.300", pin.Version);
        Assert.Equal(10, pin.Major);
    }
}
