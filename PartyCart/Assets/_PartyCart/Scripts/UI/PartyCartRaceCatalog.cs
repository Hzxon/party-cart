using System;
using UnityEngine;

[Serializable]
public sealed class PartyCartMapOption
{
    public string id;
    public string displayName;
    [Tooltip("An enabled scene in Build Settings.")]
    public string sceneName;
    public Texture2D preview;
    [Tooltip("Optional X/Z centerline points for the route preview.")]
    public Vector2[] route = Array.Empty<Vector2>();
}

[Serializable]
public sealed class PartyCartCharacterOption
{
    public string id;
    public string displayName;
    public Texture2D preview;
    public Texture2D portrait;
}

/// <summary>Real menu content, separate from the illustrative design mockups.</summary>
[CreateAssetMenu(menuName = "Party Cart/Race Catalog", fileName = "RaceCatalog")]
public sealed class PartyCartRaceCatalog : ScriptableObject
{
    public PartyCartMapOption[] maps = Array.Empty<PartyCartMapOption>();
    // Additional character models must also be integrated with gameplay when authored.
    // The first real entry is the Pot Head racer already present in SampleScene.
    public PartyCartCharacterOption[] characters = Array.Empty<PartyCartCharacterOption>();
}

/// <summary>Confirmed choices survive scene changes/replay, but not a new play session.</summary>
public static class PartyCartRaceChoice
{
    public static string MapId { get; private set; } = string.Empty;
    public static string CharacterId { get; private set; } = string.Empty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        MapId = string.Empty;
        CharacterId = string.Empty;
    }

    public static void Confirm(PartyCartMapOption map, PartyCartCharacterOption character)
    {
        MapId = map.id;
        CharacterId = character.id;
    }
}
