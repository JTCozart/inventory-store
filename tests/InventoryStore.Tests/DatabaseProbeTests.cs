using InventoryStore.Application.Services;

namespace InventoryStore.Tests;

public class DatabaseProbeTests
{
    [Fact]
    public async Task ADatabaseThatAnswersIsReachable()
    {
        Assert.True(await DatabaseProbe.CanReachAsync(_ => Task.FromResult(true), CancellationToken.None));
    }

    [Fact]
    public async Task ADatabaseThatRefusesTheConnectionIsNotReachable()
    {
        Assert.False(await DatabaseProbe.CanReachAsync(_ => Task.FromResult(false), CancellationToken.None));
    }

    [Fact]
    public async Task ADatabaseThatThrowsIsNotReachableAndDoesNotCrashTheProbe()
    {
        Task<bool> Broken(CancellationToken _) => throw new InvalidOperationException("disk gone");

        Assert.False(await DatabaseProbe.CanReachAsync(Broken, CancellationToken.None));
    }

    [Fact]
    public async Task ACancelledProbeStaysCancelled()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        Task<bool> Cancelled(CancellationToken token) => Task.FromCanceled<bool>(token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DatabaseProbe.CanReachAsync(Cancelled, cancelled.Token));
    }
}
