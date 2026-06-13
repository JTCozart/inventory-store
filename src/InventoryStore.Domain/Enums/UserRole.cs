namespace InventoryStore.Domain.Enums;

public enum UserRole
{
    Admin = 0,
    Manager = 1,
    Viewer = 2,
    // Terminal staff: can check out / check in and consume stock from the Terminal,
    // but cannot add, edit, restock, or manage anything else.
    Staff = 3
}
