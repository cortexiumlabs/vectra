using Vectra.BuildingBlocks.Errors;
using Vectra.BuildingBlocks.Exceptions;

namespace Vectra.Infrastructure.Exceptions;

public abstract class SerializationException : BaseException
{
    protected SerializationException(ErrorCode errorCode, string message, Exception? innerException = null) 
        : base(errorCode, message, innerException)
    {
    }
}