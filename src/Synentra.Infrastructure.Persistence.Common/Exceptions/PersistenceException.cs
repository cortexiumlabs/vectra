using Vectra.BuildingBlocks.Errors;
using Vectra.BuildingBlocks.Exceptions;

namespace Vectra.Infrastructure.Persistence.Common.Exceptions;

public abstract class PersistenceException : BaseException
{
    protected PersistenceException(ErrorCode errorCode, string message, Exception? innerException = null) 
        : base(errorCode, message, innerException)
    {
    }
}