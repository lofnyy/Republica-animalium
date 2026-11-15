using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Cutscene'lerin ortak durumu.
///   IsActive      : bir cutscene oynuyor mu (düşmanlar bu sırada donar)
///   SkipIntroOnce : bir sonraki sahne yüklemesinde giriş cutscene'ini atla (ölünce ayarlanır)
/// </summary>
public static class CutsceneState
{
    public static int ActiveCount;
    public static bool IsActive => ActiveCount > 0;
    public static bool SkipIntroOnce;

    public static void Begin() => ActiveCount++;
    public static void End() => ActiveCount = Mathf.Max(0, ActiveCount - 1);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        ActiveCount = 0;
        SkipIntroOnce = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // Sahne değişince yarım kalmış cutscene sayacı sıfırlansın
    static void OnSceneLoaded(Scene s, LoadSceneMode m) => ActiveCount = 0;
}
