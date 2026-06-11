using Vectra.BuildingBlocks.Errors;

namespace Vectra.Infrastructure.Errors;

public class InfrastructureErrorCodes
{
    public static readonly ErrorCode JsonSerializationInputRequired = new(0200004, ErrorCategory.Serialization);
}