using Vectra.BuildingBlocks.Errors;

namespace Vectra.BuildingBlocks.Exceptions;

public abstract class BaseException : Exception
{
    public ErrorCode ErrorCode { get; }

    protected BaseException(ErrorCode errorCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    public override string ToString() => $"{ErrorCode}: {Message}";
}