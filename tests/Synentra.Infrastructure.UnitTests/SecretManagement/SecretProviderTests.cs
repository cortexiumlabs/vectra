using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Security.KeyVault.Secrets;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Synentra.BuildingBlocks.Configuration.SecretManagement;
using Synentra.Infrastructure.SecretManagement;
using Synentra.Infrastructure.SecretManagement.Providers;
using System.Reflection;

namespace Synentra.Infrastructure.UnitTests.SecretManagement;

public class EnvironmentVariablesSecretProviderTests
{
    // EnvironmentVariablesSecretProvider is internal sealed — test via its behavior through reflection
    private static ISecretProvider? CreateEnvProvider(EnvironmentVariablesSecretConfiguration config)
    {
        var type = typeof(SecretProviderFactory).Assembly
            .GetType("Synentra.Infrastructure.SecretManagement.Providers.EnvironmentVariablesSecretProvider");
        if (type == null) return null;
        return (ISecretProvider?)Activator.CreateInstance(
            type,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public,
            null,
            [config],
            null);
    }

    [Fact]
    public void Configure_NullPrefix_AddsEnvironmentVariablesWithoutPrefix()
    {
        var config = new EnvironmentVariablesSecretConfiguration { Prefix = null };
        var provider = CreateEnvProvider(config);
        provider.Should().NotBeNull();
        var builder = new ConfigurationBuilder();

        var act = () => provider!.Configure(builder);

        act.Should().NotThrow();
    }

    [Fact]
    public void Configure_WhitespacePrefix_AddsEnvironmentVariablesWithoutPrefix()
    {
        var config = new EnvironmentVariablesSecretConfiguration { Prefix = "   " };
        var provider = CreateEnvProvider(config);
        provider.Should().NotBeNull();
        var builder = new ConfigurationBuilder();

        var act = () => provider!.Configure(builder);

        act.Should().NotThrow();
    }

    [Fact]
    public void Configure_ValidPrefix_AddsEnvironmentVariablesWithPrefix()
    {
        var config = new EnvironmentVariablesSecretConfiguration { Prefix = "MYAPP_" };
        var provider = CreateEnvProvider(config);
        provider.Should().NotBeNull();
        var builder = new ConfigurationBuilder();

        var act = () => provider!.Configure(builder);

        act.Should().NotThrow();
    }
}

public class UserSecretsSecretProviderTests
{
    private static ISecretProvider? CreateUserSecretsProvider()
    {
        var type = typeof(SecretProviderFactory).Assembly
            .GetType("Synentra.Infrastructure.SecretManagement.Providers.UserSecretsSecretProvider");
        if (type == null) return null;
        return (ISecretProvider?)Activator.CreateInstance(
            type,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public,
            null,
            [],
            null);
    }

    [Fact]
    public void Configure_DoesNotThrow()
    {
        var provider = CreateUserSecretsProvider();
        provider.Should().NotBeNull();
        var builder = new ConfigurationBuilder();

        var act = () => provider!.Configure(builder);

        act.Should().NotThrow();
    }
}

public class AzureKeyVaultSecretProviderTests
{
    // ---------- VaultUri validation ----------
    [Fact]
    public void Configure_ShouldThrow_WhenVaultUriMissing()
    {
        var config = new AzureKeyVaultSecretConfiguration { VaultUri = "" };
        var provider = new AzureKeyVaultSecretProvider(config);

        Action act = () => provider.Configure(new ConfigurationBuilder());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*VaultUri must be set*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Configure_ShouldThrow_WhenVaultUriInvalid(string uri)
    {
        var config = new AzureKeyVaultSecretConfiguration { VaultUri = uri };
        var provider = new AzureKeyVaultSecretProvider(config);

        Action act = () => provider.Configure(new ConfigurationBuilder());

        act.Should().Throw<InvalidOperationException>();
    }

    // ---------- Valid configuration with spy ----------
    [Fact]
    public void Configure_WithValidUri_NoPrefix_UsesDefaultKeyVaultSecretManager()
    {
        var config = new AzureKeyVaultSecretConfiguration
        {
            VaultUri = "https://fakevault.vault.azure.net/"
        };
        var provider = new AzureKeyVaultSecretProvider(config);
        var spy = new SpyConfigurationBuilder();

        provider.Configure(spy);

        var source = spy.AddedSources.OfType<AzureKeyVaultConfigurationSource>().Single();
        var options = GetAzureKeyVaultOptions(source);
        options.Manager.Should().BeOfType<KeyVaultSecretManager>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Configure_WhenPrefixIsEmptyOrWhitespace_UsesDefaultManager(string? prefix)
    {
        var config = new AzureKeyVaultSecretConfiguration
        {
            VaultUri = "https://fakevault.vault.azure.net/",
            SecretPrefix = prefix
        };
        var provider = new AzureKeyVaultSecretProvider(config);
        var spy = new SpyConfigurationBuilder();

        provider.Configure(spy);

        var options = GetAzureKeyVaultOptions(
            spy.AddedSources.OfType<AzureKeyVaultConfigurationSource>().Single());
        options.Manager.Should().BeOfType<KeyVaultSecretManager>();
    }

    [Fact]
    public void Configure_WithPrefix_UsesPrefixManager()
    {
        var config = new AzureKeyVaultSecretConfiguration
        {
            VaultUri = "https://fakevault.vault.azure.net/",
            SecretPrefix = "MyApp"
        };
        var provider = new AzureKeyVaultSecretProvider(config);
        var spy = new SpyConfigurationBuilder();

        provider.Configure(spy);

        var options = GetAzureKeyVaultOptions(
            spy.AddedSources.OfType<AzureKeyVaultConfigurationSource>().Single());
        options.Manager.GetType().Name.Should().Be("PrefixKeyVaultSecretManager");
    }

    [Fact]
    public void Configure_WhenReloadOnChangeTrue_SetsReloadInterval()
    {
        var reload = TimeSpan.FromMinutes(5);
        var config = new AzureKeyVaultSecretConfiguration
        {
            VaultUri = "https://fakevault.vault.azure.net/",
            ReloadOnChange = true,
            ReloadInterval = reload
        };
        var provider = new AzureKeyVaultSecretProvider(config);
        var spy = new SpyConfigurationBuilder();

        provider.Configure(spy);

        var options = GetAzureKeyVaultOptions(
            spy.AddedSources.OfType<AzureKeyVaultConfigurationSource>().Single());
        options.ReloadInterval.Should().Be(reload);
    }

    [Fact]
    public void Configure_WhenOptionalTrue_AndAddThrows_DoesNotRethrow()
    {
        var config = new AzureKeyVaultSecretConfiguration
        {
            VaultUri = "https://fakevault.vault.azure.net/",
            Optional = true
        };
        var provider = new AzureKeyVaultSecretProvider(config);
        var spy = new SpyConfigurationBuilder(throwOnAdd: true);

        Action act = () => provider.Configure(spy);

        act.Should().NotThrow();
    }

    [Fact]
    public void Configure_WhenOptionalFalse_AndAddThrows_Rethrows()
    {
        var config = new AzureKeyVaultSecretConfiguration
        {
            VaultUri = "https://fakevault.vault.azure.net/",
            Optional = false
        };
        var provider = new AzureKeyVaultSecretProvider(config);
        var spy = new SpyConfigurationBuilder(throwOnAdd: true);

        Action act = () => provider.Configure(spy);

        act.Should().Throw<Exception>();
    }

    // ---------- Robust reflection helper ----------
    private static AzureKeyVaultConfigurationOptions GetAzureKeyVaultOptions(
        AzureKeyVaultConfigurationSource source)
    {
        var type = typeof(AzureKeyVaultConfigurationSource);

        // 1) Try public/internal property "Options"
        var prop = type.GetProperty("Options",
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
        if (prop != null)
            return (AzureKeyVaultConfigurationOptions)prop.GetValue(source)!;

        // 2) Try private field "_options" (most common)
        var field = type.GetField("_options",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
            return (AzureKeyVaultConfigurationOptions)field.GetValue(source)!;

        // 3) Try field "options" (no underscore)
        var field2 = type.GetField("options",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field2 != null)
            return (AzureKeyVaultConfigurationOptions)field2.GetValue(source)!;

        // 4) Search any member of the correct type
        var member = type
            .GetMembers(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(m =>
                (m.MemberType == MemberTypes.Field || m.MemberType == MemberTypes.Property)
                && (m is FieldInfo f && f.FieldType == typeof(AzureKeyVaultConfigurationOptions))
                || (m is PropertyInfo p && p.PropertyType == typeof(AzureKeyVaultConfigurationOptions)));

        if (member != null)
        {
            return member switch
            {
                FieldInfo f => (AzureKeyVaultConfigurationOptions)f.GetValue(source)!,
                PropertyInfo p => (AzureKeyVaultConfigurationOptions)p.GetValue(source)!,
                _ => null
            };
        }

        // If everything fails, dump members for diagnosis
        var allMembers = string.Join(", ",
            type.GetMembers(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
                .Select(m => $"{m.MemberType} {m.Name}"));
        throw new InvalidOperationException(
            $"Could not find Options in AzureKeyVaultConfigurationSource. Members: [{allMembers}]");
    }

    // ---------- Spy builder ----------
    private sealed class SpyConfigurationBuilder : IConfigurationBuilder
    {
        private readonly ConfigurationBuilder _inner = new();
        private readonly bool _throwOnAdd;

        public SpyConfigurationBuilder(bool throwOnAdd = false) => _throwOnAdd = throwOnAdd;

        public IList<IConfigurationSource> AddedSources { get; } = new List<IConfigurationSource>();

        public IDictionary<string, object> Properties => _inner.Properties;
        public IList<IConfigurationSource> Sources => AddedSources;

        public IConfigurationBuilder Add(IConfigurationSource source)
        {
            if (_throwOnAdd)
                throw new Exception("Simulated exception");
            AddedSources.Add(source);
            return this;
        }

        public IConfigurationRoot Build() => _inner.Build();
    }
}

// ---------- Nested PrefixKeyVaultSecretManager tests ----------
public class PrefixKeyVaultSecretManagerTests
{
    private static object CreateManager(string prefix)
    {
        var nested = typeof(AzureKeyVaultSecretProvider)
            .GetNestedType("PrefixKeyVaultSecretManager", BindingFlags.NonPublic);
        return Activator.CreateInstance(nested!, prefix)!;
    }

    [Fact]
    public void Load_ShouldReturnTrue_ForMatchingPrefix()
    {
        var manager = CreateManager("app");
        var load = manager.GetType().GetMethod("Load")!;
        var result = (bool)load.Invoke(manager, new object[] { new SecretProperties("app--Database--Password") })!;
        result.Should().BeTrue();
    }

    [Fact]
    public void Load_ShouldReturnFalse_ForDifferentPrefix()
    {
        var manager = CreateManager("app");
        var load = manager.GetType().GetMethod("Load")!;
        var result = (bool)load.Invoke(manager, new object[] { new SecretProperties("other--Password") })!;
        result.Should().BeFalse();
    }

    [Fact]
    public void Load_IsCaseInsensitive()
    {
        var manager = CreateManager("app");
        var load = manager.GetType().GetMethod("Load")!;
        var result = (bool)load.Invoke(manager, new object[] { new SecretProperties("APP--Database") })!;
        result.Should().BeTrue();
    }

    [Fact]
    public void Load_RequiresExactPrefixWithDoubleDash()
    {
        var manager = CreateManager("app");
        var load = manager.GetType().GetMethod("Load")!;
        var result = (bool)load.Invoke(manager, new object[] { new SecretProperties("app-Database") })!;
        result.Should().BeFalse();
    }

    [Fact]
    public void GetKey_ShouldConvertDoubleDashToConfigurationDelimiter()
    {
        var manager = CreateManager("app");
        var getKey = manager.GetType().GetMethod("GetKey")!;
        var secret = SecretModelFactory.KeyVaultSecret(new SecretProperties("app--Database--Password"), "secret");
        var key = (string)getKey.Invoke(manager, new object[] { secret })!;
        key.Should().Be("Database:Password");
    }

    [Fact]
    public void GetKey_WhenNoDoubleDashAfterPrefix_ReturnsRemainderAsIs()
    {
        var manager = CreateManager("app");
        var getKey = manager.GetType().GetMethod("GetKey")!;
        var secret = SecretModelFactory.KeyVaultSecret(new SecretProperties("app--simple"), "secret");
        var key = (string)getKey.Invoke(manager, new object[] { secret })!;
        key.Should().Be("simple");
    }

    [Fact]
    public void GetKey_WithMultipleDoubleDash_ConvertsAll()
    {
        var manager = CreateManager("app");
        var getKey = manager.GetType().GetMethod("GetKey")!;
        var secret = SecretModelFactory.KeyVaultSecret(new SecretProperties("app--Level1--Level2--Key"), "secret");
        var key = (string)getKey.Invoke(manager, new object[] { secret })!;
        key.Should().Be("Level1:Level2:Key");
    }

    [Fact]
    public void GetKey_SingleDashWithinKey_IsPreserved()
    {
        var manager = CreateManager("app");
        var getKey = manager.GetType().GetMethod("GetKey")!;
        var secret = SecretModelFactory.KeyVaultSecret(new SecretProperties("app--Database-Password"), "secret");
        var key = (string)getKey.Invoke(manager, new object[] { secret })!;
        key.Should().Be("Database-Password");
    }

    [Fact]
    public void Constructor_ShouldTrimTrailingDash()
    {
        var manager = CreateManager("app-");
        var load = manager.GetType().GetMethod("Load")!;
        var result = (bool)load.Invoke(manager, new object[] { new SecretProperties("app--Secret") })!;
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("app--", "app--Key")]      // nothing to trim
    [InlineData("app---", "app---Key")]    // trims one dash → app--Key matches
    [InlineData("app-", "app--Key")]       // trims dash → app--Key
    public void Constructor_TrimsAndAppendsDoubleDash(string input, string matchingSecretName)
    {
        var manager = CreateManager(input);
        var load = manager.GetType().GetMethod("Load")!;
        var secret = new SecretProperties(matchingSecretName);
        ((bool)load.Invoke(manager, new object[] { secret })!).Should().BeTrue();
        var nonMatching = new SecretProperties("wrong");
        ((bool)load.Invoke(manager, new object[] { nonMatching })!).Should().BeFalse();
    }
}