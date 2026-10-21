using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>User preferences. Graphics profiles clone the pipeline; project assets are never edited.</summary>
public static class PartyCartGameSettings
{
    private const string Key = "PartyCart.Options.v1";
    [Serializable] public sealed class Values
    {
        public float master = .8f, music = .65f, effects = .9f;
        public int quality = 2, width = 1920, height = 1080;
        public bool fullscreen = true;
        public Values Copy() => (Values)MemberwiseClone();
    }

    public static Values Current { get; private set; }
    public static event Action AudioChanged;
    private static UniversalRenderPipelineAsset runtimePipeline;
    private static RenderPipelineAsset previousPipeline;
    private static float previousLodBias;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Current = null;
        AudioChanged = null;
    }

    public static void Initialize()
    {
        if (Current != null) return;
        var defaults = new Values { width = Screen.width, height = Screen.height, fullscreen = Screen.fullScreen };
        bool saved = PlayerPrefs.HasKey(Key);
        try { Current = saved ? JsonUtility.FromJson<Values>(PlayerPrefs.GetString(Key)) ?? defaults : defaults; }
        catch (ArgumentException) { Current = defaults; saved = false; }
        Sanitize(Current);
        ApplyAudio(Current);
        if (saved) Apply(Current, true);
    }

    public static void Sanitize(Values value)
    {
        value.master = SafeVolume(value.master);
        value.music = SafeVolume(value.music);
        value.effects = SafeVolume(value.effects);
        value.quality = Mathf.Clamp(value.quality, 0, 2);
        value.width = Mathf.Clamp(value.width, 640, Mathf.Max(1920, Display.main.systemWidth));
        value.height = Mathf.Clamp(value.height, 360, Mathf.Max(1080, Display.main.systemHeight));
    }

    private static float SafeVolume(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp01(value);

    public static void Apply(Values value, bool display)
    {
        Sanitize(value);
        Current = value.Copy();
        ApplyAudio(Current);
        if (runtimePipeline == null)
        {
            var original = (QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            if (original != null)
            {
                previousPipeline = QualitySettings.renderPipeline;
                previousLodBias = QualitySettings.lodBias;
                runtimePipeline = UnityEngine.Object.Instantiate(original);
                runtimePipeline.name = "Party Cart User Graphics (Runtime Only)";
                QualitySettings.renderPipeline = runtimePipeline;
            }
        }
        if (runtimePipeline != null)
        {
            runtimePipeline.renderScale = Current.quality == 0 ? .8f : Current.quality == 1 ? .9f : 1f;
            runtimePipeline.shadowDistance = Current.quality == 0 ? 20f : Current.quality == 1 ? 28f : 35f;
            runtimePipeline.shadowCascadeCount = Current.quality == 0 ? 1 : Current.quality == 1 ? 2 : 4;
            QualitySettings.lodBias = Current.quality == 0 ? .8f : Current.quality == 1 ? 1.25f : 2f;
        }
        // Game View resolution belongs to the editor, not this game setting.
        if (display && !Application.isEditor)
            Screen.SetResolution(Current.width, Current.height, Current.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
    }

    private static void ApplyAudio(Values value)
    {
        AudioListener.volume = value.master;
        AudioChanged?.Invoke();
    }

    public static void Save()
    {
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(Current));
        PlayerPrefs.Save();
    }

    public static void BindSceneAudio()
    {
        foreach (var source in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
            if (source.GetComponent<PartyCartAudioBus>() == null) source.gameObject.AddComponent<PartyCartAudioBus>();
    }

    public static void ReleaseRuntimeGraphics()
    {
        if (runtimePipeline == null) return;
        if (QualitySettings.renderPipeline == runtimePipeline) QualitySettings.renderPipeline = previousPipeline;
        QualitySettings.lodBias = previousLodBias;
        UnityEngine.Object.Destroy(runtimePipeline);
        runtimePipeline = null;
    }
}
