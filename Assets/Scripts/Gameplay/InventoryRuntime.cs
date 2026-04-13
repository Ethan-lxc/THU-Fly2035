using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>运行时线索背包（轻量 id + 展示字段）。</summary>
public class InventoryRuntime : MonoBehaviour
{
    public static InventoryRuntime Instance { get; private set; }

    readonly List<ClueEntry> _clues = new List<ClueEntry>();

    public IReadOnlyList<ClueEntry> Clues => _clues;

    public event Action OnChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool HasClue(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;
        foreach (var c in _clues)
        {
            if (c.id == id)
                return true;
        }

        return false;
    }

    public void AddClue(string id, string title, Sprite icon)
    {
        if (string.IsNullOrEmpty(id) || HasClue(id))
            return;
        _clues.Add(new ClueEntry { id = id, title = title, icon = icon });
        OnChanged?.Invoke();
    }

    /// <summary>整表替换（如互动检查点回退）；会触发 <see cref="OnChanged"/>。</summary>
    public void ReplaceClues(IReadOnlyList<ClueEntry> newClues)
    {
        _clues.Clear();
        if (newClues != null)
        {
            for (var i = 0; i < newClues.Count; i++)
                _clues.Add(newClues[i]);
        }

        OnChanged?.Invoke();
    }
}

[Serializable]
public struct ClueEntry
{
    public string id;
    public string title;
    public Sprite icon;
}
