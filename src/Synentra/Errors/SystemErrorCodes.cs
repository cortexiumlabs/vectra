using Synentra.BuildingBlocks.Errors;

namespace Synentra.Errors;

public class SystemErrorCodes
{
    public static readonly ErrorCode AuthenticationRequired = new(900_002, ErrorCategory.System);
}