using Synentra.BuildingBlocks.Errors;
using Synentra.BuildingBlocks.Exceptions;

namespace Synentra.Infrastructure.Exceptions;

public abstract class SerializationException : BaseException
{
    protected SerializationException(ErrorCode errorCode, string message, Exception? innerException = null) 
        : base(errorCode, message, innerException)
    {
    }
}