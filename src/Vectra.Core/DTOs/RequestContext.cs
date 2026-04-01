namespace Vectra.Core.DTOs;

public class RequestContext
{
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? Body { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public Guid AgentId { get; set; }
    public double TrustScore { get; set; }
}