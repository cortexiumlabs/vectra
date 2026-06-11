using System.Diagnostics.CodeAnalysis;
using Synentra.Infrastructure;
using Synentra.Infrastructure.SecretManagement;

namespace Synentra.Extensions;

[ExcludeFromCodeCoverage]
public static class SecretManagementExtensions
{
    public static WebApplicationBuilder AddSynentraSecretManagement(this WebApplicationBuilder builder)
    {
        builder.Services.AddSecretManagement();
        var secretManager = builder.Services.BuildServiceProvider().GetRequiredService<ISecretManagementService>();
        secretManager.Current?.Configure(builder.Configuration);

        return builder;
    }
}

