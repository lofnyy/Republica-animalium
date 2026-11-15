using UnityEngine;

/// <summary>
/// Yerde kayan şok dalgası. Oyuncuya değerse hasar verir (üstünden zıplanabilir).
/// Duvara çarpınca ya da menzili bitince yok olur.
/// </summary>
public class Shockwave : MonoBehaviour
{
    public float speed = 9f;
    public int direction = 1;
    public float maxDistance = 30f;
    public int damage = 1;
    public Vector2 size = new Vector2(0.7f, 1.1f);
    public LayerMask wallMask;

    float _travelled;
    Health _playerHp;
    Collider2D _playerCol;
    bool _hit;

    void Start()
    {
        var p = GameObject.FindWithTag("Player");
        if (p != null) { _playerHp = p.GetComponent<Health>(); _playerCol = p.GetComponent<Collider2D>(); }
    }

    void Update()
    {
        float step = speed * Time.deltaTime;
        var center = (Vector2)transform.position + Vector2.up * size.y * 0.5f;
        if (Physics2D.Raycast(center, Vector2.right * direction, step + size.x * 0.5f, wallMask))
        {
            Destroy(gameObject);
            return;
        }
        transform.position += Vector3.right * direction * step;
        _travelled += step;

        // Titreşen görünüm
        float s = 1f + Mathf.Sin(Time.time * 40f) * 0.12f;
        transform.localScale = new Vector3(1f, s, 1f);

        if (!_hit && _playerHp != null && _playerCol != null && !_playerHp.IsDead)
        {
            var b = new Bounds(center, size);
            if (b.Intersects(_playerCol.bounds))
            {
                _hit = true;
                _playerHp.TakeDamage(damage, transform.position - Vector3.right * direction);
            }
        }

        if (_travelled >= maxDistance) Destroy(gameObject);
    }
}
