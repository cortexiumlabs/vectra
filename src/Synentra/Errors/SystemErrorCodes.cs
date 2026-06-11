using Vectra.BuildingBlocks.Errors;

namespace Vectra.Errors;

public class SystemErrorCodes
{
    public static readonly ErrorCode AuthenticationRequired = new(900_002, ErrorCategory.System);
}