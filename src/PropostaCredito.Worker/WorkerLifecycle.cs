namespace PropostaCredito.Worker;

public sealed class WorkerLifecycle(ILogger<WorkerLifecycle> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Worker iniciado; handlers de negocio entram nos proximos marcos.");
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown is expected; no business job exists in M0.
        }
        finally
        {
            logger.LogInformation("Worker encerrado com cancelamento cooperativo.");
        }
    }
}
