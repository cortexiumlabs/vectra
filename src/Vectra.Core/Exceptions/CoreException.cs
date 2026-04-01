using Vectra.BuildingBlocks.Errors;
using Vectra.BuildingBlocks.Exceptions;

namespace Vectra.Core.Exceptions;

public abstract class CoreException : BaseException
{
    protected CoreException(ErrorCode errorCode, string message) : base(errorCode, message)
    {
    }
}