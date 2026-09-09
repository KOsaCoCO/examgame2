// Anything that holds a countable, spendable stock of items by name -
// InventoryManager and PlayerHandManager both implement this. Lets a cost
// check (Drain, and later Configure/Trade) draw from several containers as
// one combined pool via ResourcePool, instead of hardcoding "inventory only".
public interface IResourceContainer
{
    int CountItems(string itemName);
    bool HasItems(string itemName, int quantity);
    bool TryRemoveItems(string itemName, int quantity);
}
