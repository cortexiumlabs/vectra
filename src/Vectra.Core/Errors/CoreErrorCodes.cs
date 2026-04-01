using Vectra.BuildingBlocks.Errors;

namespace Vectra.Core.Errors;

public static class CoreErrorCodes
{
    public static readonly ErrorCode AuditTrailNotFound = new(200_101, ErrorCategory.Core);
}