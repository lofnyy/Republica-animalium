using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Oyuncu buna değince sonraki chapter'a geçilir (son chapter'dan sonra ana menüye döner).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class LevelExit : MonoBehaviour
{
    bool _used;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_used || !other.CompareTag("Player")) return;
        _used = true;

        if (GameManager.Instance != null) GameManager.Instance.NextChapter();
        else SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); // test: aynı sahneyi yeniden üret
    }
}
