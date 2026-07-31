using FluentAssertions;
using Synentra.Infrastructure.Semantic.Providers.InternalBert;
using System.Reflection;

namespace Synentra.Infrastructure.UnitTests.Semantic;

public class ModelPathResolverTests
{
    [Fact]
    public void GetFullPackagePath_NullPath_UsesDefault()
    {
        string result = ModelPathResolver.GetFullPackagePath(null);

        result.Should().NotBeNullOrWhiteSpace();
        result.Should().EndWith("community-model.zip");
        Path.IsPathRooted(result).Should().BeTrue();
        (result.Contains("Synentra") || result.Contains(".synentra")).Should().BeTrue();
    }

    [Fact]
    public void GetFullPackagePath_EmptyString_UsesDefault()
    {
        string result = ModelPathResolver.GetFullPackagePath(string.Empty);
        result.Should().EndWith("community-model.zip");
        Path.IsPathRooted(result).Should().BeTrue();
    }

    [Fact]
    public void GetFullPackagePath_WhiteSpace_UsesDefault()
    {
        string result = ModelPathResolver.GetFullPackagePath("   ");
        result.Should().EndWith("community-model.zip");
        Path.IsPathRooted(result).Should().BeTrue();
    }

    [Fact]
    public void GetFullPackagePath_AbsolutePath_ReturnsUnchanged()
    {
        string input = Path.Combine(Path.GetTempPath(), "my-model.zip");
        string result = ModelPathResolver.GetFullPackagePath(input);
        result.Should().Be(input);
    }

    [Fact]
    public void GetFullPackagePath_RelativePath_CombinesWithBaseDirectory()
    {
        string input = "models" + Path.DirectorySeparatorChar + "my-model.zip";
        string result = ModelPathResolver.GetFullPackagePath(input);

        result.Should().StartWith(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        result.Should().EndWith(input);
        Path.IsPathRooted(result).Should().BeTrue();
    }

    [Fact]
    public void GetFullPackagePath_PathWithEnvironmentVariable_ExpandsAndMakesAbsolute()
    {
        string input = "%TEMP%/subdir/model.zip";
        string result = ModelPathResolver.GetFullPackagePath(input);

        string expectedRoot = Environment.GetEnvironmentVariable("TEMP")!;
        result.Should().StartWith(expectedRoot);
        result.Should().EndWith("model.zip");
        Path.IsPathRooted(result).Should().BeTrue();
    }

    [Fact]
    public void GetFullPackagePath_ExpandedPathAlreadyAbsolute_ReturnsAsIs()
    {
        string input = "%TEMP%" + Path.DirectorySeparatorChar + "model.zip";
        string result = ModelPathResolver.GetFullPackagePath(input);

        string expected = Path.Combine(Environment.GetEnvironmentVariable("TEMP")!, "model.zip");
        result.Should().Be(expected);
    }

    [Fact]
    public void GetDefaultPackagePath_ReturnsPathEndingWithDefaultFileName()
    {
        var method = typeof(ModelPathResolver).GetMethod("GetDefaultPackagePath",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        string result = (string)method!.Invoke(null, null)!;

        result.Should().EndWith("community-model.zip");
        Path.IsPathRooted(result).Should().BeTrue();
    }

    [Fact]
    public void GetDefaultPackagePath_UsesLocalApplicationDataIfAvailable()
    {
        string? localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.Create);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            var method = typeof(ModelPathResolver).GetMethod("GetDefaultPackagePath",
                BindingFlags.NonPublic | BindingFlags.Static);
            string result = (string)method!.Invoke(null, null)!;

            result.Should().StartWith(localAppData);
        }
    }
}