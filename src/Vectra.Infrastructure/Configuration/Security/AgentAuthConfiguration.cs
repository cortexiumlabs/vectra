using Vectra.Application.Abstractions.Security;

namespace Vectra.Infrastructure.Configuration.Security;

public class AgentAuthConfiguration
{
    public AgentAuthScheme Scheme { get; set; } = AgentAuthScheme.None;
    public JwtConfiguration Jwt { get; set; } = new JwtConfiguration();
}