namespace Synentra.Application.Abstractions.Dispatchers;

public interface IOperation<TResult> : IAction<TResult> { }
public interface IOperation : IOperation<Void> { }