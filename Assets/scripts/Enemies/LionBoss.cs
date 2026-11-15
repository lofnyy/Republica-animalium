using System.Collections;
using UnityEngine;

/// <summary>
/// Level 3 boss'u: Aslan (Kral). Kılıçla savaşır.
///   Kılıç darbesi : kılıcı kaldırır (yukarıda), öne adım atıp indirir (aşağıda) — önündeki dar alan
///   Yatay savuruş : kılıcı yana çeker, kısa hücumla yatay savurur (ortada) — geniş ama alçak alan
///   Yere vuruş    : kılıcı kaldırıp zıplar, inince iki yana şok dalgası (faz 2)
///   Kükreme       : tavandan parçalar düşer (faz 2)
/// Sadece saldırırken hasar verir, değince vermez. Canı bitince ölmez: dizlerinin üstüne çöker,
/// Defeated olayı tetiklenir (bitiş ara sahnesi hikâye yöneticisinde).
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(Health))]
public class LionBoss : BossBase
{
    [Header("Genel")]
    public LayerMask groundMask;
    public SpriteRenderer body;

    [Header("Çizimler")]
    public Sprite idleRight, idleLeft;
    [Tooltip("Kılıç yukarıda")]
    public Sprite raiseRight, raiseLeft;
    [Tooltip("Kılıç aşağıda (darbe anı)")]
    public Sprite downRight, downLeft;
    [Tooltip("Kılıç ortada (yatay savuruş)")]
    public Sprite midRight, midLeft;

    [Header("Oda")]
    public float arenaMinX = 613.5f;
    public float arenaMaxX = 659f;
    public float floorY = 0f;
    public float ceilingY = 14f;

    [Header("Yürüme")]
    public float walkSpeed = 2.4f;
    public float preferredDistance = 2.6f;

    [Header("Saldırı seçimi")]
    public float attackCooldown = 1.2f;
    public float recoverTime = 0.6f;

    [Header("Kılıç darbesi")]
    public float strikeWindup = 0.6f;
    public float strikeLunge = 5f;
    public Vector2 strikeHitOffset = new Vector2(2.0f, 1.4f);
    public Vector2 strikeHitSize = new Vector2(2.6f, 2.8f);
    public int strikeDamage = 1;

    [Header("Yatay savuruş")]
    public float sweepWindup = 0.5f;
    public float sweepDashSpeed = 10f;
    public float sweepDuration = 0.35f;
    public Vector2 sweepHitOffset = new Vector2(2.2f, 0.7f);
    public Vector2 sweepHitSize = new Vector2(3.4f, 1.3f);
    public int sweepDamage = 1;

    [Header("Yere vuruş")]
    public GameObject shockwavePrefab;
    public float slamWindup = 0.5f;
    public float slamJumpVelocity = 12f;
    public float shockwaveSpeed = 9f;

    [Header("Kükreme")]
    public GameObject rockPrefab;
    public float roarWindup = 0.5f;
    public float roarDuration = 1.6f;
    public int rocksPhase2 = 7;

    [Header("Faz 2")]
    [Range(0f, 1f)] public float phase2At = 0.5f;
    public float phase2SpeedMultiplier = 1.3f;
    public event System.Action Enraged;

    [Header("Renk")]
    public Color windupColor = new Color(1f, 0.75f, 0.6f);
    public Color hitColor = Color.white;

    public bool IsActive { get; private set; }

    enum State { Sleeping, Walk, Windup, Strike, Sweep, Slam, Roar, Recover, Kneel }
    enum Attack { Strike, Sweep, Slam, Roar }

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
    bool _hitDone;
    bool _enraged;
    float _flash;
    Color _baseColor = Color.white;
    Vector3 _baseScale = Vector3.one;

    bool Phase2 => _hp.Current <= _hp.maxHealth * phase2At;
    float Speed => Phase2 ? phase2SpeedMultiplier : 1f;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<BoxCollider2D>();
        _hp = GetComponent<Health>();
        _rb.freezeRotation = true;
        _col.sharedMaterial = new PhysicsMaterial2D("LionNoFriction") { friction = 0f, bounciness = 0f };
        if (body != null) { _baseColor = body.color; _baseScale = body.transform.localScale; }
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
        SetSprite(idleRight, idleLeft);
    }

    public override void Activate()
    {
        if (IsActive) return;
        IsActive = true;
        _cooldown = 0.8f;
        Enter(State.Walk);
    }

    /// <summary>Ara sahnelerde yön vermek için.</summary>
    public void Face(int dir)
    {
        _dir = dir >= 0 ? 1 : -1;
        if (_state == State.Sleeping || _state == State.Kneel) SetSprite(idleRight, idleLeft);
    }

    /// <summary>Ara sahnede kılıcı kaldırma pozu.</summary>
    public void ShowRaised(bool raised) => SetSprite(raised ? raiseRight : idleRight, raised ? raiseLeft : idleLeft);

    void Enter(State s)
    {
        _state = s;
        _stateTime = 0f;
        _hitDone = false;
    }

    void SetSprite(Sprite right, Sprite left)
    {
        if (body == null) return;
        var s = _dir > 0 ? right : left;
        if (s != null && body.sprite != s) body.sprite = s;
    }

    // ---------------------------------------------------------------- görünüş

    void Update()
    {
        if (body == null) return;
        if (_state == State.Kneel)
        {
            body.color = Color.Lerp(body.color, _baseColor, 10f * Time.deltaTime);
            return;
        }

        // yön: oyuncuya bakar (saldırının ortasında dönmez)
        if (IsActive && _player != null && (_state == State.Walk || _state == State.Recover))
            _dir = _player.position.x >= transform.position.x ? 1 : -1;

        switch (_state)
        {
            case State.Windup:
                if (_next == Attack.Sweep) SetSprite(midRight, midLeft);
                else if (_next == Attack.Roar) SetSprite(idleRight, idleLeft);
                else SetSprite(raiseRight, raiseLeft);
                break;
            case State.Strike: SetSprite(downRight, downLeft); break;
            case State.Sweep: SetSprite(midRight, midLeft); break;
            case State.Slam: SetSprite(_rb.linearVelocity.y > 0f ? raiseRight : downRight, _rb.linearVelocity.y > 0f ? raiseLeft : downLeft); break;
            case State.Recover: SetSprite(_hitDone || _stateTime < 0.25f ? downRight : idleRight, _hitDone || _stateTime < 0.25f ? downLeft : idleLeft); break;
            default: SetSprite(idleRight, idleLeft); break;
        }

        // renk: vurulunca beyaz, hazırlanırken yanıp söner, faz 2'de hafif kızıl
        Color c = _baseColor;
        if (_flash > 0f) { _flash -= Time.deltaTime; c = hitColor; }
        else if (_state == State.Windup) c = Color.Lerp(_baseColor, windupColor, 0.5f + 0.5f * Mathf.Sin(Time.time * 28f));
        else if (_enraged) c = Color.Lerp(_baseColor, windupColor, 0.15f);
        body.color = c;

        // yürürken hafif sallanma, hazırlanırken gerilme
        var t = body.transform;
        if (_state == State.Walk && Mathf.Abs(_rb.linearVelocity.x) > 0.2f)
            t.localScale = new Vector3(_baseScale.x, _baseScale.y * (1f + Mathf.Sin(Time.time * 10f) * 0.02f), _baseScale.z);
        else if (_state == State.Windup)
            t.localScale = new Vector3(_baseScale.x * 0.97f, _baseScale.y * 1.03f, _baseScale.z);
        else
            t.localScale = Vector3.Lerp(t.localScale, _baseScale, 12f * Time.deltaTime);
    }

    // ---------------------------------------------------------------- davranış

    void FixedUpdate()
    {
        if (_state == State.Kneel) { _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y); return; }
        var v = _rb.linearVelocity;
        if (!IsActive || CutsceneState.IsActive || _player == null || _playerHp == null || _playerHp.IsDead)
        {
            _rb.linearVelocity = new Vector2(0f, v.y);
            return;
        }

        if (!_enraged && Phase2)
        {
            _enraged = true;
            if (_cam != null) _cam.Shake(0.35f, 0.8f);
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
                    _dir = toPlayer;
                    v.x = 0f;
                    Enter(State.Windup);
                }
                break;

            case State.Windup:
                v.x = 0f;
                float w = (_next == Attack.Strike ? strikeWindup : _next == Attack.Sweep ? sweepWindup
                         : _next == Attack.Slam ? slamWindup : roarWindup) / Speed;
                if (_stateTime >= w)
                {
                    switch (_next)
                    {
                        case Attack.Strike: v.x = _dir * strikeLunge; Enter(State.Strike); break;
                        case Attack.Sweep: Enter(State.Sweep); break;
                        case Attack.Slam: v = new Vector2(Mathf.Clamp(dx * 0.8f, -6f, 6f), slamJumpVelocity); Enter(State.Slam); break;
                        case Attack.Roar: StartCoroutine(RockRain()); if (_cam != null) _cam.Shake(0.3f, roarDuration); Enter(State.Roar); break;
                    }
                }
                break;

            case State.Strike:
                v.x = Mathf.MoveTowards(v.x, 0f, 30f * dt);
                if (!_hitDone && _stateTime > 0.06f) { HitBox(strikeHitOffset, strikeHitSize, strikeDamage); if (_cam != null) _cam.Shake(0.2f, 0.15f); }
                if (_stateTime > 0.25f) Enter(State.Recover);
                break;

            case State.Sweep:
                v.x = _dir * sweepDashSpeed * Speed;
                if (!_hitDone) HitBox(sweepHitOffset, sweepHitSize, sweepDamage, keepTrying: true);
                if (_stateTime >= sweepDuration || WallAhead(_dir)) { v.x = 0f; Enter(State.Recover); }
                break;

            case State.Slam:
                if (_stateTime > 0.15f && v.y <= 0.1f && Grounded())
                {
                    v.x = 0f;
                    SlamImpact();
                    Enter(State.Recover);
                }
                break;

            case State.Roar:
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

        // odadan çıkmasın
        var p = _rb.position;
        if ((p.x <= arenaMinX + 1f && v.x < 0f) || (p.x >= arenaMaxX - 1f && v.x > 0f)) v.x = 0f;
        _rb.linearVelocity = v;
    }

    Attack PickAttack(float distance)
    {
        // faz 1: kılıç darbesi ve savuruş; faz 2: hepsi
        var options = new System.Collections.Generic.List<Attack> { Attack.Strike, Attack.Sweep };
        if (Phase2) { options.Add(Attack.Slam); options.Add(Attack.Roar); }
        if (options.Count > 2) options.Remove(_last);
        if (distance > 6f && options.Contains(Attack.Sweep) && Random.value < 0.6f) return Attack.Sweep;
        if (distance < 3.2f && options.Contains(Attack.Strike) && Random.value < 0.5f) return Attack.Strike;
        return options[Random.Range(0, options.Count)];
    }

    void HitBox(Vector2 offset, Vector2 size, int damage, bool keepTrying = false)
    {
        if (_playerHp == null || _playerCol == null || _playerHp.IsDead) { _hitDone = true; return; }
        var center = (Vector2)transform.position + new Vector2(offset.x * _dir, offset.y);
        var box = new Bounds(center, size);
        if (box.Intersects(_playerCol.bounds))
        {
            if (_playerHp.TakeDamage(damage, transform.position)) _hitDone = true;
            else if (!keepTrying) _hitDone = true; // dash ile kaçtı
        }
        else if (!keepTrying) _hitDone = true;
    }

    void SlamImpact()
    {
        if (_cam != null) _cam.Shake(0.5f, 0.4f);
        if (shockwavePrefab == null) return;
        var b = _col.bounds;
        foreach (int d in new[] { -1, 1 })
        {
            var go = Instantiate(shockwavePrefab, new Vector3(d > 0 ? b.max.x : b.min.x, floorY, 0f), Quaternion.identity);
            var sw = go.GetComponent<Shockwave>();
            if (sw != null) { sw.direction = d; sw.speed = shockwaveSpeed * Speed; sw.wallMask = groundMask; }
        }
    }

    IEnumerator RockRain()
    {
        if (rockPrefab == null) yield break;
        float interval = roarDuration / rocksPhase2;
        for (int i = 0; i < rocksPhase2 && _state != State.Kneel; i++)
        {
            float x = (i % 3 == 0 && _player != null) ? _player.position.x : Random.Range(arenaMinX + 1f, arenaMaxX - 1f);
            x = Mathf.Clamp(x, arenaMinX + 0.8f, arenaMaxX - 0.8f);
            var go = Instantiate(rockPrefab, new Vector3(x, floorY, 0f), Quaternion.identity);
            var fr = go.GetComponent<FallingRock>();
            if (fr != null) { fr.floorY = floorY; fr.startY = ceilingY; }
            yield return new WaitForSeconds(interval);
        }
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

    // ---------------------------------------------------------------- yenilgi

    void OnDied()
    {
        StopAllCoroutines();
        IsActive = false;
        Enter(State.Kneel);
        _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
        foreach (var s in FindObjectsByType<Shockwave>(FindObjectsSortMode.None)) Destroy(s.gameObject);
        foreach (var r in FindObjectsByType<FallingRock>(FindObjectsSortMode.None)) Destroy(r.gameObject);
        StartCoroutine(KneelRoutine());
    }

    IEnumerator KneelRoutine()
    {
        if (_cam != null) _cam.Shake(0.4f, 0.6f);
        if (_player != null) _dir = _player.position.x >= transform.position.x ? 1 : -1;
        SetSprite(idleRight, idleLeft);
        _rb.linearVelocity = Vector2.zero;
        _rb.simulated = false;
        var t = transform;
        var startPosition = t.position;
        var startRotation = t.rotation;
        float side = _player != null && _player.position.x > startPosition.x ? 1f : -1f;
        float lift = _col.bounds.extents.x * 0.8f;

        float shakeTime = 0f;
        while (shakeTime < 0.35f)
        {
            shakeTime += Time.deltaTime;
            t.position = startPosition + new Vector3(Mathf.Sin(shakeTime * 45f) * 0.05f, 0f, 0f);
            yield return null;
        }

        t.position = startPosition;
        float e = 0f;
        while (e < 0.65f)
        {
            e += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, e / 0.65f);
            t.rotation = Quaternion.Slerp(startRotation, Quaternion.Euler(0f, 0f, 90f * side), k);
            t.position = startPosition + Vector3.up * (lift * k);
            yield return null;
        }
        t.rotation = Quaternion.Euler(0f, 0f, 90f * side);
        t.position = startPosition + Vector3.up * lift;
        _col.enabled = false;
        if (_cam != null) _cam.Shake(0.35f, 0.4f);
        RaiseDefeated();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.6f);
        int d = Application.isPlaying ? _dir : -1;
        Gizmos.DrawWireCube(transform.position + new Vector3(strikeHitOffset.x * d, strikeHitOffset.y), strikeHitSize);
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
        Gizmos.DrawWireCube(transform.position + new Vector3(sweepHitOffset.x * d, sweepHitOffset.y), sweepHitSize);
    }
}
