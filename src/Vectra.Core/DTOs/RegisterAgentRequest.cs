namespace Vectra.Core.DTOs;

public record RegisterAgentRequest(string Name, string OwnerId, string ClientSecret);