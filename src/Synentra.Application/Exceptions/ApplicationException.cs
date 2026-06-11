using Synentra.BuildingBlocks.Errors;
using Synentra.BuildingBlocks.Exceptions;

namespace Synentra.Application.Exceptions;

public abstract class ApplicationException : BaseException
{
    protected ApplicationException(ErrorCode errorCode, string message) : base(errorCode, message)
    {
    }
}