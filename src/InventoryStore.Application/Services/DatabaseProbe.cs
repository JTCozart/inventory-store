namespace InventoryStore.Application.Services;

/// <summary>Answers "can the database be reached" for the health endpoint without letting an exception escape.</summary>
public static class DatabaseProbe
{
    public static async Task<bool> CanReachAsync(Func<CancellationToken, Task<bool>> connect, CancellationToken cancel)
    {
        try
        {
            return await connect(cancel);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return false;
        }
    }
}
