using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Son boss: dev Kral.
///
/// Uyanış : tahtından kalkar, kükrer (ekran sarsılır)
/// Yürü    : oyuncuya yaklaşır
/// Saldırılar (önce rengi kızarır = uyarı):
///   Hücum      : odanın içinde hızla koşar (dokunursa 2 hasar)
///   Yer sarsma : zıplayıp yere çakılır, iki yana şok dalgası yollar (üstünden zıpla)
///   Kükreme    : tavandan taşlar yağar (yerdeki kırmızı işaretlerden kaç)
/// Canı yarıya inince öfkelenir: daha hızlı, daha çok dalga ve taş.
/// Pivot ayaklardadır.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(Health))]
public class KingBoss : BossBase
{
    [Header("Genel")]
    public LayerMask groundMask;
    [Tooltip("Görsellerin kökü (yön değiştirirken çevrilir)")]
    public Transform visualRoot;
    [Tooltip("Asıl gövde çizimi (çizim gelince spriteRight/spriteLeft buraya konur)")]
    public SpriteRenderer body;
    public Sprite spriteRight;
    public Sprite spriteLeft;
    [Tooltip("Sağa yürüme kareleri (boşsa animasyon yok)")]
    public Sprite[] walkRight;
    [Tooltip("Sola yürüme kareleri")]
    public Sprite[] walkLeft;
    public float walkFps = 7f;
    public float chargeFps = 14f;
    public int contactDamage = 1;
    [Tooltip("Kapalıysa sadece saldırırken (hücum, yere çökme) değince hasar verir; yürürken/dururken vermez")]
    public bool touchDamageWhenIdle = true;

    [Header("Oda")]
    public float arenaMinX = 612f;
    public float arenaMaxX = 658f;
    public float floorY = 0f;
    public float ceilingY = 14f;

    [Header("Yürüme")]
    public float walkSpeed = 2.2f;
    public float preferredDistance = 3.5f;

    [Header("Saldırı seçimi")]
    public float attackCooldown = 1.4f;
    public float recoverTime = 0.8f;

    [Header("Hücum")]
    public float chargeWindup = 0.7f;
    public float chargeSpeed = 14f;
    public float chargeDuration = 1.1f;
    public int chargeDamage = 2;

    [Header("Yer sarsma")]
    public GameObject shockwavePrefab;
    public float stompWindup = 0.55f;
    public float stompJumpVelocity = 14f;
    public float stompMaxHorizontalSpeed = 7f;
    public float shockwaveSpeed = 9f;

    [Header("Kükreme (taş yağmuru)")]
    public GameObject rockPrefab;
    public float roarWindup = 0.6f;
    public float roarDuration = 1.6f;
    public int rocksPhase1 = 6;
    public int rocksPhase2 = 10;

    [Header("Faz 2")]
    [Range(0f, 1f)] public float phase2At = 0.5f;
    public float phase2SpeedMultiplier = 1.3f;

    [Header("Renkler")]
    public Color windupColor = new Color(1f, 0.35f, 0.3f);
    public Color hitColor = new Color(1f, 0.75f, 0.75f);

    public bool IsActive { get; private set; }
    /// <summary>Canı yarıya inip öfkelendiği an (faz 2) bir kez çağrılır.</summary>
    public event System.Action Enraged;

    enum State { Sleeping, Rising, Walk, Windup, Charging, Stomping, Roaring, Recover, Dead }
    enum Attack { Charge, Stomp, Roar }

    Rigidbody2D _rb;
    BoxCollider2D _col;
    Health _hp;
    Transform _player;
    Health _playerHp;
    Collider2D _playerCol;
    CameraFollow _cam;

    State _state = State.Sleeping;
    Attack _next;
    Attack _last = Attack.Roar;
    float _stateTime;
    float _cooldown;
    int _dir = -1;
    int _stompsLeft;
    float _flash;
    bool _enraged;

    SpriteRenderer[] _renderers;
    Color[] _baseColors;
    Vector3 _lastAnimPos;
    float _animTime;

    bool Phase2 => _hp.Current <= _hp.maxHealth * phase2At;
    float Speed => Phase2 ? phase2SpeedMultiplier : 1f;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<BoxCollider2D>();
        _hp = GetComponent<Health>();
        _rb.freezeRotation = true;
        _col.sharedMaterial = new PhysicsMaterial2D("KingNoFriction") { friction = 0f, bounciness = 0f };
        var root = visualRoot != null ? visualRoot : transform;
        _renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        _baseColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++) _baseColors[i] = _renderers[i].color;
        _hp.Damaged += _ => { if (_state != State.Sleeping) _flash = 0.08f; };
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
        Face(_dir);
    }

    public override void Activate()
    {
        if (IsActive) return;
        IsActive = true;
        StartCoroutine(Rise());
    }

    IEnumerator Rise()
    {
        Enter(State.Rising);
        // ayağa kalkar
        float t = 0f;
        var root = visualRoot != null ? visualRoot : transform;
        var baseScale = root.localScale;
        while (t < 0.8f)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0.85f, 1f, t / 0.8f);
            root.localScale = new Vector3(baseScale.x, baseScale.y * k, baseScale.z);
            yield return null;
        }
        root.localScale = baseScale;
        // kükrer
        if (_cam != null) _cam.Shake(0.45f, 1.2f);
        yield return new WaitForSeconds(1.1f);
        _cooldown = 0.6f;
        Enter(State.Walk);
    }

    void Enter(State s)
    {
        _state = s;
        _stateTime = 0f;
    }

    void Update()
    {
        if (_state == State.Dead) return;

        // Yön: oyuncuya bakar (hücum ederken koştuğu yöne)
        if (_player != null && _state != State.Charging && _state != State.Sleeping)
            Face(_player.position.x >= transform.position.x ? 1 : -1);

        // Renk
        bool windupBlink = _state == State.Windup || (_state == State.Roaring && _stateTime < 0.3f);
        for (int i = 0; i < _renderers.Length; i++)
        {
            var r = _renderers[i];
            if (r == null) continue;
            var c = _baseColors[i];
            if (_flash > 0f) c = Color.Lerp(c, hitColor, 0.75f);
            else if (windupBlink) c = Color.Lerp(c, windupColor, 0.45f + 0.35f * Mathf.Sin(Time.time * 28f));
            else if (_enraged) c = Color.Lerp(c, windupColor, 0.12f);
            c.a = _baseColors[i].a;
            r.color = c;
        }
        if (_flash > 0f) _flash -= Time.deltaTime;

        // Yer sarsmaya hazırlanırken çömelir
        if (visualRoot != null && _state != State.Rising)
        {
            float sy = (_state == State.Windup && _next == Attack.Stomp) ? 0.9f : 1f;
            var ls = visualRoot.localScale;
            visualRoot.localScale = new Vector3(ls.x, Mathf.Abs(sy), ls.z);
        }
    }

    void Face(int dir)
    {
        _dir = dir;
        if (body != null && (spriteRight != null || spriteLeft != null))
        {
            var s = dir > 0 ? spriteRight : spriteLeft;
            if (s != null) body.sprite = s;
        }
        else if (visualRoot != null)
        {
            var ls = visualRoot.localScale;
            visualRoot.localScale = new Vector3(Mathf.Abs(ls.x) * (dir > 0 ? 1f : -1f), ls.y, ls.z);
        }
    }

    /// <summary>
    /// Yürüme animasyonu: hareket ederken walkRight/walkLeft kareleri döner, dururken spriteRight/spriteLeft.
    /// Hız konum değişiminden ölçülür (ara sahnelerde konumla yürütüldüğünde de çalışsın).
    /// </summary>
    void LateUpdate()
    {
        if (body == null || _state == State.Dead) return;
        var frames = _dir > 0 ? walkRight : walkLeft;
        if (frames == null || frames.Length == 0) return;

        float dt = Time.deltaTime;
        float speed = dt > 0f ? Mathf.Abs(transform.position.x - _lastAnimPos.x) / dt : 0f;
        _lastAnimPos = transform.position;
        if (dt <= 0f) return;

        if (_state == State.Stomping)
        {
            body.sprite = frames[Mathf.Min(1, frames.Length - 1)]; // havada bacaklar açık
        }
        else if (speed > 0.4f)
        {
            _animTime += dt * (_state == State.Charging ? chargeFps : walkFps);
            body.sprite = frames[(int)_animTime % frames.Length];
        }
        else
        {
            _animTime = 0f;
            var idle = _dir > 0 ? spriteRight : spriteLeft;
            if (idle != null) body.sprite = idle;
        }
    }

    void FixedUpdate()
    {
        if (_state == State.Dead) return;
        var v = _rb.linearVelocity;

        if (!IsActive || _state == State.Rising || CutsceneState.IsActive || _player == null || _playerHp == null || _playerHp.IsDead)
        {
            _rb.linearVelocity = new Vector2(0f, v.y);
            return;
        }

        if (!_enraged && Phase2)
        {
            _enraged = true;
            if (_cam != null) _cam.Shake(0.4f, 0.8f);
            Enraged?.Invoke();
        }

        float dt = Time.fixedDeltaTime;
        _stateTime += dt;
        _cooldown -= dt;
        float dx = _player.position.x - transform.position.x;
        int toPlayer = dx >= 0f ? 1 : -1;

        switch (_state)
        {
            case State.Walk:
                v.x = Mathf.Abs(dx) > preferredDistance ? toPlayer * walkSpeed * Speed : 0f;
                if (_cooldown <= 0f && Grounded())
                {
                    _next = PickAttack(Mathf.Abs(dx));
                    _last = _next;
                    v.x = 0f;
                    _stompsLeft = Phase2 ? 2 : 1;
                    Enter(State.Windup);
                }
                break;

            case State.Windup:
                v.x = 0f;
                float windup = (_next == Attack.Charge ? chargeWindup : _next == Attack.Stomp ? stompWindup : roarWindup) / Speed;
                if (_stateTime >= windup)
                {
                    if (_next == Attack.Charge) { _dir = toPlayer; Face(_dir); Enter(State.Charging); }
                    else if (_next == Attack.Stomp) { v = StompJump(dx); Enter(State.Stomping); }
                    else { StartCoroutine(RockRain()); Enter(State.Roaring); }
                }
                break;

            case State.Charging:
                v.x = _dir * chargeSpeed * Speed;
                if (_stateTime >= chargeDuration || WallAhead(_dir))
                {
                    if (WallAhead(_dir) && _cam != null) _cam.Shake(0.35f, 0.35f);
                    Enter(State.Recover);
                }
                break;

            case State.Stomping:
                if (_stateTime > 0.15f && v.y <= 0.1f && Grounded())
                {
                    StompImpact();
                    v.x = 0f;
                    _stompsLeft--;
                    if (_stompsLeft > 0) { _next = Attack.Stomp; Enter(State.Windup); }
                    else Enter(State.Recover);
                }
                break;

            case State.Roaring:
                v.x = 0f;
                if (_stateTime >= roarDuration) Enter(State.Recover);
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
        int touch = _state == State.Charging ? chargeDamage
                  : (_state == State.Stomping || touchDamageWhenIdle) ? contactDamage : 0;
        TouchDamage(touch);
    }

    Attack PickAttack(float distance)
    {
        // Aynı saldırıyı üst üste yapmamaya çalışır
        var options = new List<Attack> { Attack.Charge, Attack.Stomp, Attack.Roar };
        options.Remove(_last);
        if (distance > 10f && options.Contains(Attack.Charge) && Random.value < 0.6f) return Attack.Charge;
        return options[Random.Range(0, options.Count)];
    }

    Vector2 StompJump(float dx)
    {
        float g = Mathf.Abs(Physics2D.gravity.y) * _rb.gravityScale;
        float airTime = 2f * stompJumpVelocity / g;
        float vx = Mathf.Clamp(dx / airTime, -stompMaxHorizontalSpeed, stompMaxHorizontalSpeed);
        return new Vector2(vx, stompJumpVelocity);
    }

    void StompImpact()
    {
        if (_cam != null) _cam.Shake(0.5f, 0.45f);
        if (shockwavePrefab == null) return;
        var b = _col.bounds;
        foreach (int d in new[] { -1, 1 })
        {
            var pos = new Vector3(d > 0 ? b.max.x : b.min.x, floorY, 0f);
            var go = Instantiate(shockwavePrefab, pos, Quaternion.identity);
            var sw = go.GetComponent<Shockwave>();
            if (sw != null)
            {
                sw.direction = d;
                sw.speed = shockwaveSpeed * Speed;
                sw.wallMask = groundMask;
            }
        }
    }

    IEnumerator RockRain()
    {
        if (_cam != null) _cam.Shake(0.3f, roarDuration);
        if (rockPrefab == null) yield break;
        int count = Phase2 ? rocksPhase2 : rocksPhase1;
        float interval = roarDuration / count;
        for (int i = 0; i < count && _state != State.Dead; i++)
        {
            // İlki ve her üçüncüsü oyuncunun üstüne, diğerleri rastgele
            float x = (i % 3 == 0 && _player != null) ? _player.position.x : Random.Range(arenaMinX + 1f, arenaMaxX - 1f);
            x = Mathf.Clamp(x, arenaMinX + 0.8f, arenaMaxX - 0.8f);
            var go = Instantiate(rockPrefab, new Vector3(x, floorY, 0f), Quaternion.identity);
            var fr = go.GetComponent<FallingRock>();
            if (fr != null) { fr.floorY = floorY; fr.startY = ceilingY; }
            yield return new WaitForSeconds(interval);
        }
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
        return Physics2D.Raycast(b.center, Vector2.right * dir, b.extents.x + 0.2f, groundMask);
    }

    void OnDied()
    {
        StopAllCoroutines();
        Enter(State.Dead);
        IsActive = false;
        StartCoroutine(DeathRoutine());
    }

    IEnumerator DeathRoutine()
    {
        _rb.linearVelocity = Vector2.zero;
        _rb.simulated = false;
        if (_cam != null) _cam.Shake(0.5f, 2.2f);

        // Dizlerinin üstüne çöker, titreyerek kararır ve solar
        float t = 0f, dur = 2.4f;
        var start = transform.position;
        var root = visualRoot != null ? visualRoot : transform;
        var baseScale = root.localScale;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = t / dur;
            transform.position = start + new Vector3(Mathf.Sin(t * 55f) * 0.08f, 0f, 0f);
            root.localScale = new Vector3(baseScale.x, baseScale.y * Mathf.Lerp(1f, 0.55f, k * k), baseScale.z);
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                var c = Color.Lerp(_baseColors[i], Color.black, k * 0.6f);
                c.a = _baseColors[i].a * (1f - k * k);
                _renderers[i].color = c;
            }
            yield return null;
        }
        RaiseDefeated();
        Destroy(gameObject);
    }
}
