using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class QuestTargetEntry
{
    public string questId;
    public Transform target;
}

public class QuestTargetRegistry : MonoBehaviour
{
    [SerializeField] private Transform defaultTarget;
    [SerializeField] private List<QuestTargetEntry> questTargets = new List<QuestTargetEntry>();

    private readonly Dictionary<string, Transform> lookup = new Dictionary<string, Transform>();

    private void Awake()
    {
        BuildLookup();
    }

    private void OnValidate()
    {
        BuildLookup();
    }

    public Transform GetTargetForQuest(MushroomQuest quest)
    {
        if (quest == null)
            return defaultTarget;

        if (!string.IsNullOrWhiteSpace(quest.questId) && lookup.TryGetValue(quest.questId, out Transform mapped))
            return mapped;

        return defaultTarget;
    }

    private void BuildLookup()
    {
        lookup.Clear();

        for (int i = 0; i < questTargets.Count; i++)
        {
            QuestTargetEntry entry = questTargets[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.questId) || entry.target == null)
                continue;

            lookup[entry.questId] = entry.target;
        }
    }
}
