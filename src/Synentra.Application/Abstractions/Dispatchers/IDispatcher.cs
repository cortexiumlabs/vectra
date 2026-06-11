namespace Vectra.Application.Abstractions.Dispatchers;

public interface IDispatcher
{
    Task<TResult> Dispatch<TResult>(IAction<TResult> action, CancellationToken cancellationToken = default);
}