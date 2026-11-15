using System.Collections;
using UnityEngine;

/// <summary>
/// Bölüm müziği: iki AudioSource arasında yumuşak geçişle çalar (boss müziği, mağara müziği…).
/// Her sahnede ayrı oluşur; sahne yeniden yüklenince (ör. ölünce) müzik de sıfırlanır.
/// Duraklatma menüsü AudioListener.pause ile müziği de durdurur.
///   MusicPlayer.Play(clip)  — yeni parçaya geçer (aynı parça zaten çalıyorsa bir şey yapmaz)
///   MusicPlayer.Stop()      — yavaşça susar
/// </summary>
public class MusicPlayer : MonoBehaviour
{
    [Range(0f, 1f)] public float volume = 0.7f;

    static MusicPlayer _instance;
    AudioSource _a, _b;   // _a şu an çalan
    Coroutine _fade;

    static MusicPlayer Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("Music");
                _instance = go.AddComponent<MusicPlayer>();
            }
            return _instance;
        }
    }

    void Awake()
    {
        _instance = this;
        _a = MakeSource();
        _b = MakeSource();
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    AudioSource MakeSource()
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.loop = true;
        s.playOnAwake = false;
        s.volume = 0f;
        s.spatialBlend = 0f;
        return s;
    }

    public static void Play(AudioClip clip, float fadeTime = 1.5f)
    {
        if (clip == null) return;
        Instance.PlayInternal(clip, fadeTime);
    }

    public static void Stop(float fadeTime = 1.5f)
    {
        if (_instance != null) _instance.PlayInternal(null, fadeTime);
    }

    void PlayInternal(AudioClip clip, float fadeTime)
    {
        if (clip != null && _a.clip == clip && _a.isPlaying) return;
        // eskisi _b'ye geçer ve söner, yenisi _a'da açılır
        var tmp = _a; _a = _b; _b = tmp;
        _a.clip = clip;
        _a.volume = 0f;
        if (clip != null) _a.Play();
        if (_fade != null) StopCoroutine(_fade);
        _fade = StartCoroutine(Crossfade(fadeTime));
    }

    IEnumerator Crossfade(float time)
    {
        float startB = _b.volume;
        float t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, time));
            if (_a.clip != null) _a.volume = Mathf.Lerp(0f, volume, k);
            _b.volume = Mathf.Lerp(startB, 0f, k);
            yield return null;
        }
        if (_a.clip != null) _a.volume = volume;
        _b.volume = 0f;
        _b.Stop();
        _b.clip = null;
        _fade = null;
    }
}
