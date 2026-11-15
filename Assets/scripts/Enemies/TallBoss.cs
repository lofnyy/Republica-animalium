using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Kilise boss'u (baykuş).
///
/// Davranış:
///   Yürü  → oyuncuya yaklaşır
///   Hazırlan (rengi açılır = uyarı) → iki saldırıdan biri:
///     Hücum : hızla oyuncuya doğru koşar (dokunursa 2 hasar)
///     Ezme  : oyuncuya doğru zıplar, yere çakılınca etrafına hasar verir, ekran sarsılır
///   Toparlan → tekrar yürü
/// Canı yarıya inince (faz 2) her şeyi daha hızlı yapar.
/// Activate() çağrılana kadar bekler (BossArena çağırır).
/// Pivot ayaklardadır.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(Health))]
public class TallBoss : BossBase
{
    [Header("Genel")]
    public LayerMask groundMask;
    public SpriteRenderer body;
    public int contactDamage = 1;

    [Header("Yürüme")]
    public float walkSpeed = 1.8f;
    [Tooltip("Oyuncuya bu kadar yaklaşınca yürümeyi bırakır")]
    public float preferredDistance = 2.5f;

    [Header("Saldırı seçimi")]
    [Tooltip("Oyuncu bu mesafedeyse saldırır")]
    public float attackRange = 8f;
    public float attackCooldown = 1.6f;
    public float recoverTime = 0.7f;
    [Range(0f, 1f)] public float chargeChance = 0.55f;

    [Header("Hücum")]
    public float chargeWindup = 0.6f;
    public float chargeSpeed = 13f;
    public float chargeDuration = 0.8f;
    public int chargeDamage = 2;

    [Header("Ezme")]
    public float slamWindup = 0.45f;
    public float slamJumpVelocity = 15f;
    public float slamMaxHorizontalSpeed = 8f;
    [Tooltip("Yere çakılınca bu yarıçaptaki yerdeki oyuncu hasar alır")]
    public float slamRadius = 3.2f;
    public int slamDamage = 1;

    [Header("Faz 2 (can bu oranın altına inince)")]
    [Range(0f, 1f)] public float phase2At = 0.5f;
    public float phase2SpeedMultiplier = 1.35f;

    [Header("Görsel")]
    [Tooltip("Oyuncu sağdayken kullanılan çizim (sağa bakan)")]
    public Sprite spriteRight;
    [Tooltip("Oyuncu soldayken kullanılan çizim (sola bakan)")]
    public Sprite spriteLeft;
    public Color windupColor = new Color(0.85f, 0.75f, 0.65f);
    public Color hitColor = Color.white;

    [Header("Ölüm")]
    [Tooltip("Açıksa ölünce yok olmaz, yere yığılıp öyle kalır")]
    public bool stayOnDeath;
    public Color deadColor = new Color(0.55f, 0.5f, 0.5f);

    public bool IsActive { get; private set; }

    enum State { Idle, Walk, Windup, Charging, Slamming, Recover, Dead }

    Rigidbody2D _rb;
    BoxCollider2D _col;
    Health _hp;
    Transform _player;
    Health _playerHp;
    Collider2D _playerCol;
    CameraFollow _cam;

    State _state = State.Idle;
    float _stateTime;
    float _cooldown;
    int _dir = -1;
    bool _nextIsCharge;
    float _flash;
    Color _baseColor;
    Vector3 _baseScale;
    Vector3 _baseLocalPos;

    bool Phase2 => _hp.Current <= _hp.maxHealth * phase2At;
    float Speed => Phase2 ? phase2SpeedMultiplier : 1f;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<BoxCollider2D>();
        _hp = GetComponent<Health>();
        _rb.freezeRotation = true;
        _col.sharedMaterial = new PhysicsMaterial2D("BossNoFriction") { friction = 0f, bounciness = 0f };
        if (body != null) { _baseColor = body.color; _baseScale = body.transform.localScale; _baseLocalPos = body.transform.localPosition; }
        _hp.Damaged += _ => _flash = 0.08f;
        _hp.Died += OnDied;
    }

    void Start()
    {
        var p = GameObject.FindWithTag("Player");
        if (p != null)
        {
            _player = p.transform;
            _playerHp = p.GetComponent<Health>();
            _playerCol = p.GetComponent<Collider2D>();
        }
        _cam = FindFirstObjectByType<CameraFollow>();
    }

    public override void Activate()
    {
        IsActive = true;
        _cooldown = 1f;
        Enter(State.Walk);
    }

    void Enter(State s)
    {
        _state = s;
        _stateTime = 0f;
    }

    void Update()
    {
        if (body == null || _state == State.Dead) return;

        // Renk: vurulunca beyaz, hazırlanırken yanıp söner
        if (_flash > 0f) { _flash -= Time.deltaTime; body.color = hitColor; }
        else if (_state == State.Windup) body.color = Color.Lerp(_baseColor, windupColor, 0.5f + 0.5f * Mathf.Sin(Time.time * 30f));
        else body.color = _baseColor;

        // Oyuncu hangi taraftaysa o yöne bakar
        if (_player != null)
        {
            var face = _player.position.x >= transform.position.x ? spriteRight : spriteLeft;
            if (face != null) body.sprite = face;
        }

        // Ezmeye hazırlanırken çömelir (ayaklar yerde kalır)
        var s = _baseScale;
        float squash = 1f;
        if (_state == State.Windup && !_nextIsCharge) { s = new Vector3(s.x * 1.08f, s.y * 0.88f, s.z); squash = 0.88f; }
        body.transform.localScale = s;
        body.transform.localPosition = new Vector3(_baseLocalPos.x, _baseLocalPos.y * squash, _baseLocalPos.z);
    }

    void FixedUpdate()
    {
        if (_state == State.Dead) return;
        var v = _rb.linearVelocity;

        if (!IsActive || CutsceneState.IsActive || _player == null || _playerHp == null || _playerHp.IsDead)
        {
            _rb.linearVelocity = new Vector2(0f, v.y);
            return;
        }

        float dt = Time.fixedDeltaTime;
        _stateTime += dt;
        _cooldown -= dt;
        float dx = _player.position.x - transform.position.x;

        switch (_state)
        {
            case State.Walk:
                _dir = dx >= 0f ? 1 : -1;
                v.x = Mathf.Abs(dx) > preferredDistance ? _dir * walkSpeed * Speed : 0f;
                if (_cooldown <= 0f && Mathf.Abs(dx) < attackRange && Grounded())
                {
                    _nextIsCharge = UnityEngine.Random.value < chargeChance;
                    v.x = 0f;
                    Enter(State.Windup);
                }
                break;

            case State.Windup:
                v.x = 0f;
                _dir = dx >= 0f ? 1 : -1;
                float windup = (_nextIsCharge ? chargeWindup : slamWindup) / Speed;
                if (_stateTime >= windup)
                {
                    if (_nextIsCharge) Enter(State.Charging);
                    else
                    {
                        // oyuncunun bulunduğu yere düşecek şekilde zıpla
                        float airTime = 2f * slamJumpVelocity / (Mathf.Abs(Physics2D.gravity.y) * _rb.gravityScale);
                        v.x = Mathf.Clamp(dx / airTime, -slamMaxHorizontalSpeed, slamMaxHorizontalSpeed);
                        v.y = slamJumpVelocity;
                        Enter(State.Slamming);
                    }
                }
                break;

            case State.Charging:
                v.x = _dir * chargeSpeed * Speed;
                if (_stateTime >= chargeDuration || WallAhead(_dir))
                {
                    if (WallAhead(_dir) && _cam != null) _cam.Shake(0.25f, 0.25f);
                    Enter(State.Recover);
                }
                break;

            case State.Slamming:
                if (_stateTime > 0.15f && v.y <= 0.1f && Grounded())
                {
                    SlamImpact();
                    v.x = 0f;
                    Enter(State.Recover);
                }
                break;

            case State.Recover:
                v.x = Mathf.MoveTowards(v.x, 0f, 40f * dt);
                if (_stateTime >= recoverTime / Speed)
                {
                    _cooldown = attackCooldown / Speed;
                    Enter(State.Walk);
                }
                break;
        }

        _rb.linearVelocity = v;
        TouchDamage(_state == State.Charging ? chargeDamage : contactDamage);
    }

    void SlamImpact()
    {
        if (_cam != null) _cam.Shake(0.4f, 0.4f);
        if (_playerHp == null || _playerCol == null) return;
        float feetY = _col.bounds.min.y;
        bool closeX = Mathf.Abs(_player.position.x - transform.position.x) < slamRadius;
        bool nearGround = _playerCol.bounds.min.y - feetY < 1.2f;
        if (closeX && nearGround) _playerHp.TakeDamage(slamDamage, transform.position);
    }

    void TouchDamage(int damage)
    {
        if (_playerHp == null || _playerCol == null || _playerHp.IsDead) return;
        if (_col.bounds.Intersects(_playerCol.bounds))
            _playerHp.TakeDamage(damage, transform.position);
    }

    bool Grounded()
    {
        var b = _col.bounds;
        return Physics2D.OverlapBox(new Vector2(b.center.x, b.min.y - 0.05f), new Vector2(b.size.x * 0.9f, 0.1f), 0f, groundMask) != null;
    }

    bool WallAhead(int dir)
    {
        var b = _col.bounds;
        return Physics2D.Raycast(b.center, Vector2.right * dir, b.extents.x + 0.15f, groundMask);
    }

    void OnDied()
    {
        Enter(State.Dead);
        IsActive = false;
        StartCoroutine(DeathRoutine());
    }

    IEnumerator DeathRoutine()
    {
        _rb.linearVelocity = Vector2.zero;
        _rb.simulated = false;
        if (_cam != null) _cam.Shake(0.3f, 1.2f);

        if (stayOnDeath)
        {
            // Sendeler, yana devrilir ve yerde kalır
            float e = 0f, sway = 0.5f;
            var p0 = transform.position;
            while (e < sway)
            {
                e += Time.deltaTime;
                transform.position = p0 + new Vector3(Mathf.Sin(e * 40f) * 0.08f, 0f, 0f);
                yield return null;
            }
            transform.position = p0;
            float side = (_player != null && _player.position.x > p0.x) ? 1f : -1f; // oyuncudan öteye düşer
            float fall = 0.7f;
            e = 0f;
            var from = transform.rotation;
            var to = Quaternion.Euler(0f, 0f, 90f * side);
            var c0 = body != null ? body.color : Color.white;
            // Pivot ayakta: yatınca gövde yere gömülmesin diye yarı genişlik kadar yükselir
            float lift = _col.bounds.extents.x * 0.8f;
            while (e < fall)
            {
                e += Time.deltaTime;
                float k = Mathf.Clamp01(e / fall); k *= k;
                transform.rotation = Quaternion.Slerp(from, to, k);
                transform.position = p0 + new Vector3(0f, lift * k, 0f);
                if (body != null) body.color = Color.Lerp(c0, deadColor, k);
                yield return null;
            }
            transform.rotation = to;
            if (_cam != null) _cam.Shake(0.35f, 0.4f);
            _col.enabled = false;
            yield return new WaitForSeconds(0.4f);
            RaiseDefeated();
            yield break;
        }

        // Titreyerek yere gömülür ve solar
        float t = 0f, dur = 1.6f;
        var start = transform.position;
        var c = body != null ? body.color : Color.white;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = t / dur;
            transform.position = start + new Vector3(Mathf.Sin(t * 60f) * 0.06f, -k * k * 1.5f, 0f);
            if (body != null) body.color = new Color(c.r, c.g, c.b, 1f - k);
            yield return null;
        }
        RaiseDefeated();
        Destroy(gameObject);
    }
}
