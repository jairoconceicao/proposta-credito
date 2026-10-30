using Microsoft.Extensions.Logging.Abstractions;
using PropostaCredito.Worker;

namespace PropostaCredito.UnitTests;

public sealed class WorkerLifecycleTests
{
    [Fact]
    public async Task StopsCooperativelyWhenHostShutsDown()
    {
        using var worker = new WorkerLifecycle(NullLogger<WorkerLifecycle>.Instance);
        await worker.StartAsync(CancellationToken.None);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await worker.StopAsync(deadline.Token);
        Assert.NotNull(worker.ExecuteTask);
        // .NET 10 schedules ExecuteAsync on the pool; shutdown may cancel it before its first instruction.
        Assert.True(worker.ExecuteTask.IsCompletedSuccessfully || worker.ExecuteTask.IsCanceled);
    }
}
