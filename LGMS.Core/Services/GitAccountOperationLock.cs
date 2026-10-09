namespace LGMS.Services;

public sealed class GitAccountOperationLock
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            return await action(cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ExecuteAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await action(cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
