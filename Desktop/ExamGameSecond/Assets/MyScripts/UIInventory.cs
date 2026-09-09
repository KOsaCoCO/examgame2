using UnityEngine;

// The inventory UI. Shown/hidden by pressing I (wired up by UInavigator).
public class UIInventory : MonoBehaviour
{
    public void Show() { gameObject.SetActive(true); }
    public void Hide() { gameObject.SetActive(false); }
    public void Toggle() { gameObject.SetActive(!gameObject.activeSelf); }
}
