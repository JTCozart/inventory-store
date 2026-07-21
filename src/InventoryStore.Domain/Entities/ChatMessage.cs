using InventoryStore.Domain.Enums;

namespace InventoryStore.Domain.Entities;

public class ChatMessage
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public ChatRole Role { get; set; }
    public string Content { get; set; } = string.Empty;

    // JSON-serialized list of "tool(args)" strings describing which ChatTools methods
    // grounded this answer — kept for diagnosing the model's behavior, not shown in the UI.
    public string? ToolTrace { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
