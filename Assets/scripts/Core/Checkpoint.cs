using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Kayıt noktası durumu. Oyuncu ölüp sahne yeniden yüklenince son kayıt noktasından başlar.
/// Menüden yeni bölüme girilince sıfırlanır (GameManager).
/// </summary>
public static class CheckpointState
{
    public static string SceneName;
    public static Vector3 Position;
    public static bool Has;

    public static void Set(Vector3 position)
    {
        SceneName = SceneManager.GetActiveScene().name;
        Position = position;
        Has = true;
    }

    public static void Clear() => Has = false;

    public static bool TryGet(out Vector3 position)
    {
        position = Position;
        return Has && SceneName == SceneManager.GetActiveScene().name;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init() => Has = false;
}

/// <summary>
/// Oyuncu bu tetikleyiciden geçince kayıt noktası buraya alınır.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Checkpoint : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        CheckpointState.Set(new Vector3(transform.position.x, other.transform.position.y + 0.1f, 0f));
    }
}
