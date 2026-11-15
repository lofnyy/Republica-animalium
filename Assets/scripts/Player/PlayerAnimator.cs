using UnityEngine;

/// <summary>
/// Oyuncunun sprite'ını yöne ve harekete göre değiştirir.
///   Koşarken : baktığı yönün koşma kareleri sırayla oynar
///   Dururken : baktığı yönün idle sprite'ı
///   Havada   : baktığı yönün ilk koşma karesi
/// Sola ve sağa ayrı çizim olduğu için sprite çevrilmez (flip yok).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerAnimator : MonoBehaviour
{
    public PlayerController player;

    [Header("Koşma kareleri")]
    public Sprite[] runRight;
    public Sprite[] runLeft;

    [Header("Durma (idle)")]
    public Sprite idleRight;
    public Sprite idleLeft;

    [Tooltip("Koşma animasyonunun hızı (kare/saniye)")]
    public float framesPerSecond = 8f;
    [Tooltip("Bu hızın üstünde koşuyor sayılır")]
    public float runThreshold = 0.1f;

    SpriteRenderer _sr;
    float _timer;
    int _frame;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        if (player == null) player = GetComponentInParent<PlayerController>();
    }

    void Update()
    {
        bool right = player.Facing >= 0;
        var frames = right ? runRight : runLeft;
        bool moving = Mathf.Abs(player.Velocity.x) > runThreshold;

        // Dururken: idle
        if (player.IsGrounded && !moving)
        {
            _frame = 0;
            _timer = 0f;
            var idle = right ? idleRight : idleLeft;
            if (idle != null) { _sr.sprite = idle; return; }
        }

        if (frames == null || frames.Length == 0) return;

        // Koşarken: kareleri oynat
        if (player.IsGrounded && moving)
        {
            _timer += Time.deltaTime;
            float frameTime = 1f / Mathf.Max(0.01f, framesPerSecond);
            while (_timer >= frameTime)
            {
                _timer -= frameTime;
                _frame = (_frame + 1) % frames.Length;
            }
        }
        else
        {
            // Havada: ilk kare
            _frame = 0;
            _timer = 0f;
        }

        _sr.sprite = frames[Mathf.Min(_frame, frames.Length - 1)];
    }
}
