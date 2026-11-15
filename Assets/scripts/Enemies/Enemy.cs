using UnityEngine;

/// <summary>
/// Basit yürüyen düşman:
///   - Başladığı yerin etrafında sağa sola devriye gezer (duvar ya da uçurum görünce döner)
///   - Oyuncu yakındaysa kovalar
///   - Oyuncuya dokununca hasar verir (contactDamage)
///   - weaponAttack açıksa yaklaşınca silahını kaldırır (attack sprite), sonra indirip vurur
///   - Vurulunca geri savrulur ve beyaz parlar, canı bitince yok olur
/// Pivot ayaklardadır (transform.position = ayak noktası).
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(Health))]
public class Enemy : MonoBehaviour
{
    [Header("Hareket")]
    public float patrolSpeed = 2f;
    public float chaseSpeed = 3.5f;
    [Tooltip("Başlangıç noktasından sağa/sola en fazla bu kadar gider")]
    public float patrolDistance = 4f;
    [Tooltip("Oyuncu yatayda bu kadar yakındaysa kovalar")]
    public float chaseRange = 7f;
    [Tooltip("Oyuncu dikeyde bu kadar yakındaysa kovalar")]
    public float verticalChaseRange = 2.5f;
    public LayerMask groundMask;

    [Header("Saldırı")]
    public int contactDamage = 1;

    [Header("Silahla vuruş (isteğe bağlı)")]
    public bool weaponAttack;
    public int attackDamage = 1;
    [Tooltip("Oyuncu yatayda bu kadar yakınsa vurmaya başlar")]
    public float attackRange = 1.7f;
    [Tooltip("Silahı yukarıda tutma süresi (oyuncu kaçabilsin diye)")]
    public float attackWindup = 0.45f;
    [Tooltip("Vuruştan sonra bekleme")]
    public float attackRecover = 0.45f;
    public float attackCooldown = 1.1f;
    [Tooltip("Vuruş alanı (x baktığı yöne çevrilir, ayaklara göre)")]
    public Vector2 attackHitOffset = new Vector2(0.9f, 0.7f);
    public Vector2 attackHitSize = new Vector2(1.5f, 1.4f);
    [Tooltip("Vururken öne atılma hızı")]
    public float lungeSpeed = 5f;

    [Header("Görünüş (isteğe bağlı)")]
    [Tooltip("Sağa/sola bakarken duruş (silah aşağıda)")]
    public Sprite idleRight;
    public Sprite idleLeft;
    [Tooltip("Silahı yukarı kaldırmış hali")]
    public Sprite attackRight;
    public Sprite attackLeft;
    [Tooltip("Silahı indirip vurduğu an (boşsa duruş çizimi kullanılır)")]
    public Sprite strikeRight;
    public Sprite strikeLeft;

    [Header("Animasyon kareleri (isteğe bağlı)")]
    [Tooltip("Saldırı animasyonu: hazırlanırken baştan sona oynar, son kare vuruş anıdır (doluysa attack/strike sprite'larının yerine geçer)")]
    public Sprite[] attackFramesRight;
    public Sprite[] attackFramesLeft;
    [Tooltip("Yürüme animasyonu: hareket ederken döner (boşsa duruş çizimi)")]
    public Sprite[] walkFramesRight;
    public Sprite[] walkFramesLeft;
    public float walkFps = 8f;
    [Tooltip("Sola yürürken sağa bakan kareleri yatay çevir")]
    public bool mirrorWalkFramesLeft;

    [Header("Hasar alınca")]
    public Vector2 hurtKnockback = new Vector2(6f, 4f);
    public float hurtStunTime = 0.3f;
    public SpriteRenderer bodyRenderer;
    public Color hitFlashColor = Color.white;
    public float flashTime = 0.1f;

    Rigidbody2D _rb;
    Collider2D _col;
    Health _health;
    Transform _player;
    Health _playerHealth;
    Collider2D _playerCol;
    float _startX;
    int _dir = 1;
    float _stun;
    float _flash;
    Color _baseColor = Color.white;

    enum AttackState { None, Windup, Strike, Recover }
    AttackState _atk;
    float _atkTimer;
    float _atkCooldown;
    bool _strikeHit;
    Vector3 _bodyScale = Vector3.one;
    float _walkTime;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<Collider2D>();
        _health = GetComponent<Health>();
        _rb.freezeRotation = true;
        _col.sharedMaterial = new PhysicsMaterial2D("EnemyNoFriction") { friction = 0f, bounciness = 0f };
        _startX = transform.position.x;
        if (bodyRenderer != null)
        {
            _baseColor = bodyRenderer.color;
            _bodyScale = bodyRenderer.transform.localScale;
        }

        _health.Damaged += OnDamaged;
        _health.Died += OnDied;

        var chapter = GameManager.Instance != null ? GameManager.Instance.CurrentChapter : null;
        if (chapter != null && chapter.enemyHealthMultiplier > 1f)
        {
            int scaledMax = Mathf.CeilToInt(_health.maxHealth * chapter.enemyHealthMultiplier);
            _health.SetMax(scaledMax, scaledMax);
        }
    }

    void OnDestroy()
    {
        _health.Damaged -= OnDamaged;
        _health.Died -= OnDied;
    }

    void Start()
    {
        var p = GameObject.FindWithTag("Player");
        if (p != null)
        {
            _player = p.transform;
            _playerHealth = p.GetComponent<Health>();
            _playerCol = p.GetComponent<Collider2D>();
        }
    }

    void Update()
    {
        if (bodyRenderer == null) return;
        if (_flash > 0f)
        {
            _flash -= Time.deltaTime;
            bodyRenderer.color = hitFlashColor;
        }
        else bodyRenderer.color = _baseColor;

        UpdateVisual();
    }

    void UpdateVisual()
    {
        bool right = _dir > 0;
        bool raised = _atk == AttackState.Windup;
        bool striking = _atk == AttackState.Strike || _atk == AttackState.Recover;
        var atkFrames = right ? attackFramesRight : attackFramesLeft;
        var walkFrames = right ? walkFramesRight : walkFramesLeft;
        bool mirroredWalk = false;
        Sprite s;
        if ((raised || striking) && atkFrames != null && atkFrames.Length > 0)
        {
            // hazırlanırken kareler sırayla ilerler, son kare vuruş anı
            int last = atkFrames.Length - 1;
            if (striking) s = atkFrames[last];
            else
            {
                float k = Mathf.Clamp01(1f - _atkTimer / Mathf.Max(0.01f, attackWindup));
                s = atkFrames[Mathf.Min(Mathf.Max(0, last - 1), Mathf.FloorToInt(k * last))];
            }
        }
        else if (raised) s = right ? attackRight : attackLeft;
        else if (striking && (right ? strikeRight : strikeLeft) != null) s = right ? strikeRight : strikeLeft;
        else if (walkFrames != null && walkFrames.Length > 0 && Mathf.Abs(_rb.linearVelocity.x) > 0.3f)
        {
            _walkTime += Time.deltaTime * walkFps;
            s = walkFrames[(int)_walkTime % walkFrames.Length];
            mirroredWalk = mirrorWalkFramesLeft && !right;
        }
        else s = right ? idleRight : idleLeft;
        bodyRenderer.flipX = mirroredWalk;
        if (s != null && bodyRenderer.sprite != s) bodyRenderer.sprite = s;

        // Kaldırırken titreme, indirirken ezilme
        var t = bodyRenderer.transform;
        if (_atk == AttackState.Windup)
        {
            float k = 1f - _atkTimer / attackWindup;
            t.localScale = new Vector3(_bodyScale.x * (1f - 0.05f * k), _bodyScale.y * (1f + 0.06f * k), _bodyScale.z);
        }
        else if (_atk == AttackState.Strike)
            t.localScale = new Vector3(_bodyScale.x * 1.06f, _bodyScale.y * 0.98f, _bodyScale.z); // hafif öne atılma
        else
            t.localScale = Vector3.Lerp(t.localScale, _bodyScale, 15f * Time.deltaTime);
    }

    void FixedUpdate()
    {
        if (_health.IsDead) return;

        // Cutscene sırasında dur, saldırma
        if (CutsceneState.IsActive)
        {
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            _atk = AttackState.None;
            return;
        }

        if (_stun > 0f) { _stun -= Time.fixedDeltaTime; return; }

        if (_atkCooldown > 0f) _atkCooldown -= Time.fixedDeltaTime;
        if (weaponAttack && UpdateAttack()) return;

        float speed = patrolSpeed;
        bool chasing = false;

        if (_player != null && _playerHealth != null && !_playerHealth.IsDead)
        {
            Vector2 d = _player.position - transform.position;
            if (Mathf.Abs(d.x) < chaseRange && Mathf.Abs(d.y) < verticalChaseRange)
            {
                chasing = true;
                _dir = d.x > 0f ? 1 : -1;
                speed = Mathf.Abs(d.x) < 0.2f ? 0f : chaseSpeed;
            }
        }

        if (!chasing)
        {
            if (transform.position.x > _startX + patrolDistance) _dir = -1;
            else if (transform.position.x < _startX - patrolDistance) _dir = 1;
        }

        if (Blocked(_dir))
        {
            if (chasing) speed = 0f;   // kovalarken uçurumdan atlama, bekle
            else _dir = -_dir;         // devriyede geri dön
        }

        _rb.linearVelocity = new Vector2(_dir * speed, _rb.linearVelocity.y);

        TouchDamage();
    }

    /// <summary>Saldırı sürüyorsa true döner (o karede yürüme yok).</summary>
    bool UpdateAttack()
    {
        if (_atk == AttackState.None)
        {
            if (_atkCooldown > 0f || _player == null || _playerHealth == null || _playerHealth.IsDead) return false;
            Vector2 d = _player.position - transform.position;
            if (Mathf.Abs(d.x) > attackRange || Mathf.Abs(d.y) > 1.5f) return false;
            _dir = d.x > 0f ? 1 : -1;
            _atk = AttackState.Windup;
            _atkTimer = attackWindup;
        }

        _atkTimer -= Time.fixedDeltaTime;
        switch (_atk)
        {
            case AttackState.Windup:
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                if (_atkTimer <= 0f)
                {
                    _atk = AttackState.Strike;
                    _atkTimer = 0.14f;
                    _strikeHit = false;
                }
                break;

            case AttackState.Strike:
                _rb.linearVelocity = new Vector2(_dir * lungeSpeed, _rb.linearVelocity.y);
                if (!_strikeHit) StrikeHit();
                if (_atkTimer <= 0f)
                {
                    _atk = AttackState.Recover;
                    _atkTimer = attackRecover;
                }
                break;

            case AttackState.Recover:
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                if (_atkTimer <= 0f)
                {
                    _atk = AttackState.None;
                    _atkCooldown = attackCooldown;
                }
                break;
        }
        return true;
    }

    void StrikeHit()
    {
        if (_playerHealth == null || _playerCol == null || _playerHealth.IsDead) return;
        var center = (Vector2)transform.position + new Vector2(attackHitOffset.x * _dir, attackHitOffset.y);
        var box = new Bounds(center, attackHitSize);
        if (box.Intersects(_playerCol.bounds))
        {
            _strikeHit = true;
            _playerHealth.TakeDamage(attackDamage, transform.position);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!weaponAttack) return;
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.6f);
        int d = Application.isPlaying ? _dir : 1;
        Gizmos.DrawWireCube(transform.position + new Vector3(attackHitOffset.x * d, attackHitOffset.y), attackHitSize);
    }

    bool Blocked(int dir)
    {
        var b = _col.bounds;
        bool wall = Physics2D.Raycast(b.center, Vector2.right * dir, b.extents.x + 0.1f, groundMask);
        var ahead = new Vector2(b.center.x + dir * (b.extents.x + 0.1f), b.min.y + 0.1f);
        bool ground = Physics2D.Raycast(ahead, Vector2.down, 0.6f, groundMask);
        return wall || !ground;
    }

    void TouchDamage()
    {
        if (_playerHealth == null || _playerCol == null || _playerHealth.IsDead) return;
        if (_col.bounds.Intersects(_playerCol.bounds))
            _playerHealth.TakeDamage(contactDamage, transform.position);
    }

    void OnDamaged(Vector2 source)
    {
        float dir = Mathf.Sign(transform.position.x - source.x);
        if (dir == 0f) dir = 1f;
        _rb.linearVelocity = new Vector2(dir * hurtKnockback.x, hurtKnockback.y);
        _stun = hurtStunTime;
        _flash = flashTime;
        // Vurulunca saldırısı bozulur
        if (_atk != AttackState.None)
        {
            _atk = AttackState.None;
            _atkCooldown = attackCooldown * 0.5f;
        }
    }

    void OnDied()
    {
        _col.enabled = false;
        Destroy(gameObject, flashTime);
    }
}
