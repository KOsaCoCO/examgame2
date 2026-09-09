using UnityEngine;

// Every resource-quantity movement across the economy (Inventory, Configure,
// Drain, Trade) logs through here, one line per event. Console-only for now
// (skeleton stage) - kept as a single choke point so it's easy to redirect to
// a real on-screen log later without touching every call site.
public static class ResourceEventLog
{
    public static void LogToInventory(string itemName, int quantity) => Log($"{quantity}x {itemName} -> Inventory");

    public static void LogToHand(string itemName, int quantity) => Log($"{quantity}x {itemName} -> Hand");

    public static void LogToDrain(string itemName, int quantity, string sinkName) => Log($"{quantity}x {itemName} -> Drain ({sinkName})");

    // Not wired to anything yet - Configure/Trade don't exist. Call these once those
    // pathways are built (see ResourceEconomyDesignNotes.txt).
    public static void LogToConfigure(string itemName, int quantity) => Log($"{quantity}x {itemName} -> Configure");
    public static void LogToTrade(string itemName, int quantity) => Log($"{quantity}x {itemName} -> Trade");

    private static void Log(string message) => Debug.Log($"[Resource] {message}");
}
