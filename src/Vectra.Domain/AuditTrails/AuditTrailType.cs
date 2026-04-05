namespace Vectra.Domain.AuditTrails;

public enum AuditTrailType : byte
{
    None = 0,
    Create = 1,
    Update = 2,
    Delete = 3
}