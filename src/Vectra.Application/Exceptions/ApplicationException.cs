using Vectra.BuildingBlocks.Errors;
using Vectra.BuildingBlocks.Exceptions;

namespace Vectra.Application.Exceptions;

public abstract class ApplicationException : BaseException
{
    protected ApplicationException(ErrorCode errorCode, string message) : base(errorCode, message)
    {
    }
}