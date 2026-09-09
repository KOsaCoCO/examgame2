using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// Keeps the list of quests and writes them to a live text file on disk.
// The corner quest UI (UIQuest) and the full quest UI (UIQuestFullView)
// both read their text from this same file, so they always match.
[Serializable]
public class Quest
{
    public string questName;
    public bool isMainQuest = true;
    public bool isActive = true;
}

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Tooltip("Starting quests. Add or edit these in the Inspector.")]
    public List<Quest> quests = new List<Quest>
    {
        new Quest { questName = "Find your first land and try to buy it to be safe during the night", isMainQuest = true, isActive = true },
    };

    // Fired whenever a quest changes, so the UI scripts know to refresh.
    public event Action OnQuestsChanged;

    private const string MainHeader = "=== MAIN QUESTS ===";
    private const string SideHeader = "=== SIDE QUESTS ===";
    private const string TotalsHeader = "=== TOTALS ===";

    public static string FilePath => Path.Combine(Application.persistentDataPath, "QuestLog.txt");

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        SaveToFile();
    }

    public void SetQuestActive(string questName, bool isActive)
    {
        Quest quest = quests.Find(q => q.questName == questName);
        if (quest == null) return;

        quest.isActive = isActive;
        SaveToFile();
    }

    public void AddQuest(string questName, bool isMainQuest, bool startActive = true)
    {
        quests.Add(new Quest { questName = questName, isMainQuest = isMainQuest, isActive = startActive });
        SaveToFile();
    }

    public void RemoveQuest(string questName)
    {
        quests.RemoveAll(q => q.questName == questName);
        SaveToFile();
    }

    // Updates a quest's displayed name in place (e.g. "find the dog" -> "bring the dog
    // back" once it's picked up) without touching isMainQuest/isActive or its position in
    // the list. No-op if oldName isn't currently a quest.
    public void RenameQuest(string oldName, string newName)
    {
        Quest quest = quests.Find(q => q.questName == oldName);
        if (quest == null) return;

        quest.questName = newName;
        SaveToFile();
    }

    // Returns the "Main quests" section from the live text file.
    public string GetMainQuestText()
    {
        return ReadSection(MainHeader, SideHeader);
    }

    // Returns the "Side quests" section from the live text file.
    public string GetSideQuestText()
    {
        return ReadSection(SideHeader, TotalsHeader);
    }

    private void SaveToFile()
    {
        StringBuilder text = new StringBuilder();

        text.AppendLine(MainHeader);
        AppendQuestLines(text, isMainQuest: true);
        text.AppendLine();

        text.AppendLine(SideHeader);
        AppendQuestLines(text, isMainQuest: false);
        text.AppendLine();

        int activeCount = 0;
        foreach (Quest quest in quests)
        {
            if (quest.isActive) activeCount++;
        }

        text.AppendLine(TotalsHeader);
        text.AppendLine($"Total quests: {quests.Count}");
        text.AppendLine($"Active: {activeCount}");
        text.AppendLine($"Inactive: {quests.Count - activeCount}");

        File.WriteAllText(FilePath, text.ToString());

        OnQuestsChanged?.Invoke();
    }

    private void AppendQuestLines(StringBuilder text, bool isMainQuest)
    {
        bool foundAny = false;

        foreach (Quest quest in quests)
        {
            if (quest.isMainQuest != isMainQuest) continue;

            string status = quest.isActive ? "Active" : "Inactive";
            text.AppendLine($"[{status}] {quest.questName}");
            foundAny = true;
        }

        if (!foundAny)
        {
            text.AppendLine("(none)");
        }
    }

    private string ReadSection(string startHeader, string endHeader)
    {
        if (!File.Exists(FilePath)) return "";

        string fileText = File.ReadAllText(FilePath);

        int start = fileText.IndexOf(startHeader, StringComparison.Ordinal);
        if (start < 0) return "";
        start += startHeader.Length;

        int end = fileText.IndexOf(endHeader, start, StringComparison.Ordinal);
        string section = end >= 0 ? fileText.Substring(start, end - start) : fileText.Substring(start);

        return section.Trim();
    }
}
