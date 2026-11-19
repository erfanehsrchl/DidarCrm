namespace DidarCrm.Application.Abstractions.Persistence;

public interface ITransactionRunner
{
    Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken);
}
