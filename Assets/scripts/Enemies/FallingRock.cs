using UnityEngine;

/// <summary>
/// Tavandan düşen taş. Önce yerde kırmızı bir uyarı yanıp söner,
/// sonra taş tepeden düşer. Oyuncuya değerse hasar verir.
/// </summary>
public class FallingRock : MonoBehaviour
{
    [Tooltip("Yerdeki uyarı işareti")]
    public SpriteRenderer warning;
    [Tooltip("Düşen taş")]
    public Transform rock;
    public float warnTime = 0.9f;
    public float floorY = 0f;
    public float startY = 14f;
    public float gravity = 45f;
    public int damage = 1;
    public float radius = 0.6f;

    float _t;
    float _vy;
    bool _falling;
    Health _playerHp;
    Collider2D _playerCol;

    void Start()
    {
        var p = GameObject.FindWithTag("Player");
        if (p != null) { _playerHp = p.GetComponent<Health>(); _playerCol = p.GetComponent<Collider2D>(); }
        if (rock != null) { rock.position = new Vector3(transform.position.x, startY, 0f); rock.gameObject.SetActive(false); }
    }

    void Update()
    {
        _t += Time.deltaTime;
        if (!_falling)
        {
            if (warning != null)
            {
                var c = warning.color;
                c.a = 0.35f + 0.45f * Mathf.Abs(Mathf.Sin(_t * 14f));
                warning.color = c;
            }
            if (_t >= warnTime)
            {
                _falling = true;
                if (rock != null) rock.gameObject.SetActive(true);
            }
            return;
        }

        if (rock == null) { Destroy(gameObject); return; }
        _vy -= gravity * Time.deltaTime;
        var pos = rock.position;
        pos.y += _vy * Time.deltaTime;
        rock.position = pos;
        rock.Rotate(0f, 0f, 400f * Time.deltaTime);

        if (_playerHp != null && _playerCol != null && !_playerHp.IsDead)
        {
            var b = new Bounds(pos, Vector3.one * radius * 2f);
            if (b.Intersects(_playerCol.bounds))
            {
                _playerHp.TakeDamage(damage, pos);
                Destroy(gameObject);
                return;
            }
        }

        if (pos.y <= floorY + radius)
        {
            var cam = FindFirstObjectByType<CameraFollow>();
            if (cam != null) cam.Shake(0.08f, 0.15f);
            Destroy(gameObject);
        }
    }
}
