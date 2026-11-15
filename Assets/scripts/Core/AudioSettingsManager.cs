using UnityEngine;

/// <summary>
/// Genel ses seviyesini tutar ve kaydeder (PlayerPrefs).
/// Oyun açılır açılmaz kayıtlı ses seviyesi otomatik uygulanır; sahneye koymaya gerek yok.
/// </summary>
public static class AudioSettingsManager
{
    const string VolumeKey = "masterVolume";
    const float DefaultVolume = 0.8f;

    public static float Volume
    {
        get => PlayerPrefs.GetFloat(VolumeKey, DefaultVolume);
        set
        {
            float v = Mathf.Clamp01(value);
            AudioListener.volume = v;
            PlayerPrefs.SetFloat(VolumeKey, v);
            PlayerPrefs.Save();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplySavedVolume()
    {
        AudioListener.volume = Volume;
    }
}
