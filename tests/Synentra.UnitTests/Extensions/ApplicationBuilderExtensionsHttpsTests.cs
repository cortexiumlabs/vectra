using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Synentra.BuildingBlocks.Configuration.System;
using Synentra.BuildingBlocks.Configuration.System.Server;
using Synentra.Extensions;

namespace Synentra.UnitTests.Extensions;

public class ApplicationBuilderExtensionsHttpsTests
{
    [Fact]
    public void UseSynentraHttps_HttpsDisabled_DoesNotRedirect()
    {
        var app = BuildAppWithHttpsEnabled(false);
        var act = () => app.UseSynentraHttps();
        act.Should().NotThrow();
    }

    [Fact]
    public void UseSynentraHttps_ReturnsSameBuilder()
    {
        var app = BuildAppWithHttpsEnabled(false);
        var result = app.UseSynentraHttps();
        result.Should().BeSameAs(app);
    }

    private static WebApplication BuildAppWithHttpsEnabled(bool httpsEnabled)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();

        var systemConfig = new SystemConfiguration
        {
            Server = new ServerConfiguration
            {
                Https = new HttpsServerConfiguration { Enabled = httpsEnabled }
            }
        };
        builder.Services.Configure<SystemConfiguration>(opt =>
        {
            opt.Server = systemConfig.Server;
        });

        return builder.Build();
    }
}
