using Synentra.BuildingBlocks.Errors;
using Synentra.BuildingBlocks.Exceptions;

namespace Synentra.Infrastructure.Persistence.Common.Exceptions;

public abstract class PersistenceException : BaseException
{
    protected PersistenceException(ErrorCode errorCode, string message, Exception? innerException = null) 
        : base(errorCode, message, innerException)
    {
    }
}