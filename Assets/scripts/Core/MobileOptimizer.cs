using UnityEngine;

/// <summary>
/// Mobil platformlarda runtime performans ayarları.
/// Oyun başında otomatik çalışır.
/// </summary>
public class MobileOptimizer : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
#if UNITY_ANDROID || UNITY_IOS
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        QualitySettings.vSyncCount = 0;
        Input.multiTouchEnabled = true;
#endif
    }
}
