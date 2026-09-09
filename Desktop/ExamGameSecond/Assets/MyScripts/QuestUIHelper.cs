using UnityEngine;
using TMPro;

// Small shared helper so UIQuest and UIQuestFullView don't duplicate the
// "find this existing text field by name" search logic.
public static class QuestUIHelper
{
    public static TMP_Text FindSubText(Transform root, string fieldName)
    {
        Transform found = FindDeepChild(root, fieldName);
        if (found == null)
        {
            Debug.LogWarning($"Could not find a text field named '{fieldName}' under '{root.name}'.");
            return null;
        }

        return found.GetComponent<TMP_Text>();
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName) return child;

            Transform result = FindDeepChild(child, childName);
            if (result != null) return result;
        }

        return null;
    }
}
