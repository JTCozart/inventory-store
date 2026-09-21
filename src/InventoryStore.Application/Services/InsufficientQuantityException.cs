namespace InventoryStore.Application.Services;

// Thrown when a consume asks for more than is in stock. Derives from InvalidOperationException
// so existing callers that catch that (and read the message) keep working; the Public API reads
// Requested / Available to build a structured error.
public class InsufficientQuantityException : InvalidOperationException
{
    public int Requested { get; }
    public int Available { get; }

    public InsufficientQuantityException(int requested, int available)
        : base($"Only {available} in stock. Cannot consume {requested}.")
    {
        Requested = requested;
        Available = available;
    }
}
