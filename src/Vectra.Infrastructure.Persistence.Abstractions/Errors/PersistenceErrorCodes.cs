using Vectra.BuildingBlocks.Errors;

namespace Vectra.Infrastructure.Persistence.Abstractions.Errors;

public class PersistenceErrorCodes
{
    public static readonly ErrorCode DatabaseSaveData = new(300_001, ErrorCategory.Persistence);
    public static readonly ErrorCode DatabaseModelCreating = new(300_002, ErrorCategory.Persistence);
    public static readonly ErrorCode DatabaseInitializer = new(300_003, ErrorCategory.Persistence);
}