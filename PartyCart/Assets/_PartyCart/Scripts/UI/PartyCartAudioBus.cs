using UnityEngine;

/// <summary>Assign Music for music sources; unassigned scene audio defaults to Effects.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(AudioSource))]
public sealed class PartyCartAudioBus : MonoBehaviour
{
    public enum Channel { Effects, Music }
    [SerializeField] private Channel channel;
    private AudioSource[] sources;
    private float[] authoredVolumes;
    private void Awake()
    {
        sources = GetComponents<AudioSource>();
        authoredVolumes = new float[sources.Length];
        for (int i = 0; i < sources.Length; i++) authoredVolumes[i] = sources[i].volume;
    }
    private void OnEnable() { PartyCartGameSettings.AudioChanged += Apply; Apply(); }
    private void OnDisable()
    {
        PartyCartGameSettings.AudioChanged -= Apply;
        if (sources != null) for (int i = 0; i < sources.Length; i++) if (sources[i] != null) sources[i].volume = authoredVolumes[i];
    }
    private void Apply()
    {
        var settings = PartyCartGameSettings.Current;
        if (sources == null || settings == null) return;
        for (int i = 0; i < sources.Length; i++)
            if (sources[i] != null) sources[i].volume = authoredVolumes[i] * (channel == Channel.Music ? settings.music : settings.effects);
    }
}
