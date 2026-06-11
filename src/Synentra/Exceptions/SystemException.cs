using Synentra.BuildingBlocks.Errors;
using Synentra.BuildingBlocks.Exceptions;

namespace Synentra.Exceptions;

public abstract class SystemException : BaseException
{
    protected SystemException(ErrorCode errorCode, string message, Exception? innerException = null) 
        : base(errorCode, message, innerException)
    {
    }
}