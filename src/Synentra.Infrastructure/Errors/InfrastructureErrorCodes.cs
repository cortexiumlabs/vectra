using Synentra.BuildingBlocks.Errors;

namespace Synentra.Infrastructure.Errors;

public class InfrastructureErrorCodes
{
    public static readonly ErrorCode JsonSerializationInputRequired = new(0200004, ErrorCategory.Serialization);
}