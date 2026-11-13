using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The items the player is carrying, by id ("screwdriver"). Shown in the Tab inventory bar.
/// Resets when the room restarts (same scene reloaded, e.g. out of time) or the main menu loads,
/// and carries over into a different scene (e.g. Demo -> Vent).
/// </summary>
public static class PlayerInventory
{
    public readonly struct Item
    {
        public readonly string Id;
        public readonly string DisplayName;

        public Item(string id, string displayName)
        {
            Id = id;
            DisplayName = displayName;
        }
    }

    private static readonly List<Item> items = new List<Item>();
    private static string lastSceneName;

    public static event Action Changed;
    public static IReadOnlyList<Item> Items => items;

    public static bool Has(string id) => items.Exists(item => item.Id == id);

    public static bool Add(string id, string displayName)
    {
        if (string.IsNullOrEmpty(id) || Has(id))
            return false;
        items.Add(new Item(id, string.IsNullOrEmpty(displayName) ? id : displayName));
        Changed?.Invoke();
        return true;
    }

    public static bool Remove(string id)
    {
        if (items.RemoveAll(item => item.Id == id) == 0)
            return false;
        Changed?.Invoke();
        return true;
    }

    public static void Clear()
    {
        if (items.Count == 0)
            return;
        items.Clear();
        Changed?.Invoke();
    }

    // Runs at the start of Play mode, even with domain reload turned off.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        items.Clear();
        Changed = null;
        lastSceneName = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;

        if (scene.name == lastSceneName || scene.buildIndex == 0)
            Clear();
        lastSceneName = scene.name;
    }
}
