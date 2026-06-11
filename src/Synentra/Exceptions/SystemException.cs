using Vectra.BuildingBlocks.Errors;
using Vectra.BuildingBlocks.Exceptions;

namespace Vectra.Exceptions;

public abstract class SystemException : BaseException
{
    protected SystemException(ErrorCode errorCode, string message, Exception? innerException = null) 
        : base(errorCode, message, innerException)
    {
    }
}