using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Yandan görünüm platformer hareketi: koşma, zıplama (coyote time + jump buffer + basılı tutma), dash.
///
/// Kontroller:
///   Klavye  : A/D veya ←/→ hareket, Space/W/↑ zıpla, Shift dash
///   Gamepad : sol çubuk hareket, A (güney) zıpla, B (doğu) dash
///
/// Oyuncunun pivot'u ayaklarıdır (transform.position = ayak noktası).
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Koşma")]
    public float moveSpeed = 8f;
    public float groundAcceleration = 90f;
    public float airAcceleration = 60f;

    [Header("Zıplama")]
    [Tooltip("Tuşa basılı tutunca kaç tile yükseğe çıkar")]
    public float jumpHeight = 3.5f;
    [Tooltip("Kenardan düştükten sonra hâlâ zıplanabilen süre")]
    public float coyoteTime = 0.1f;
    [Tooltip("Yere değmeden hemen önce basılan zıplama tuşunun hatırlanma süresi")]
    public float jumpBufferTime = 0.12f;
    [Tooltip("Tuş erken bırakılınca yükselişte uygulanan ekstra yerçekimi (kısa zıplama)")]
    public float lowJumpGravityMultiplier = 2.5f;
    [Tooltip("Düşerken ekstra yerçekimi (daha 'ağır' his)")]
    public float fallGravityMultiplier = 1.8f;
    public float maxFallSpeed = 22f;

    [Header("Dash")]
    public float dashSpeed = 20f;
    public float dashDuration = 0.15f;
    public float dashCooldown = 0.45f;
    [Tooltip("Dash bittikten sonra da bu kadar süre hasar alınmaz (düşmanın içinden çıkabilmek için)")]
    public float dashImmuneExtra = 0.15f;
    [Tooltip("Dash sırasında içine girilen düşmandan tamamen çıkana kadar hasar alınmaz (en fazla bu kadar saniye)")]
    public float passThroughMaxTime = 0.8f;
    [Tooltip("İçinden geçilebilen katmanlar (boşsa 'Enemy')")]
    public LayerMask passThroughMask;

    [Header("Zemin kontrolü")]
    public LayerMask groundMask;

    /// <summary>
    /// Cutscene'lerin karakteri yürütmesi için: değer verilirse klavye/gamepad yok sayılır
    /// ve karakter bu yönde yürür (-1 sol, 0 dur, 1 sağ). null = normal kontrol.
    /// </summary>
    public float? ForcedMoveInput { get; set; }

    public bool IsGrounded { get; private set; }
    public bool IsDashing => _dashTimer > 0f;
    public int Facing { get; private set; } = 1;
    /// <summary>"Dayanıklılık" bedeliyle düşmüş olabilen gerçek yürüme hızı.</summary>
    public float MoveSpeed => moveSpeed * PlayerProgress.MoveSpeedMultiplier;
    public Vector2 Velocity => _rb.linearVelocity;

    Rigidbody2D _rb;
    CapsuleCollider2D _col;
    float _baseGravity;

    float _moveInput;
    bool _jumpHeld;
    float _jumpBufferTimer;
    float _coyoteTimer;
    float _dashTimer;
    float _dashCooldownTimer;
    bool _airDashUsed;
    float _sinceJump = 1f;
    Vector2 _groundNormal = Vector2.up;
    float _knockbackTimer;
    float _dashImmuneTimer;
    bool _passingThrough;
    float _passThroughTimer;
    Health _health;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<CapsuleCollider2D>();
        _baseGravity = _rb.gravityScale;
        _health = GetComponent<Health>();
        if (passThroughMask.value == 0) passThroughMask = LayerMask.GetMask("Enemy");

        _rb.freezeRotation = true;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        // Duvara yapışmayı önlemek için sürtünmesiz
        _col.sharedMaterial = new PhysicsMaterial2D("PlayerNoFriction") { friction = 0f, bounciness = 0f };
    }

    void Update()
    {
        ReadInput();

        _jumpBufferTimer -= Time.deltaTime;
        _dashCooldownTimer -= Time.deltaTime;

        // Dash sırasında düşmanların içinden hasar almadan geçilir.
        // Dash bittiğinde hâlâ bir düşmanın (ör. hücum eden Gergedan) içindeysek,
        // ondan tamamen çıkana kadar koruma sürer: arkasına geçince hasar yok.
        bool overlapping = OverlappingEnemy();
        if (_dashImmuneTimer > 0f)
        {
            _dashImmuneTimer -= Time.deltaTime;
            if (overlapping) { _passingThrough = true; _passThroughTimer = 0f; }
        }
        else if (_passingThrough)
        {
            _passThroughTimer += Time.deltaTime;
            if (!overlapping || _passThroughTimer > passThroughMaxTime) _passingThrough = false;
        }
        if (_health != null) _health.Immune = _dashImmuneTimer > 0f || _passingThrough;
    }

    void ReadInput()
    {
        if (ForcedMoveInput.HasValue)
        {
            _moveInput = Mathf.Clamp(ForcedMoveInput.Value, -1f, 1f);
            _jumpHeld = false;
            if (_moveInput != 0f) Facing = _moveInput > 0 ? 1 : -1;
            return;
        }

        var kb = Keyboard.current;
        var gp = Gamepad.current;

        float x = 0f;
        bool jumpPressed = false, dashPressed = false;
        _jumpHeld = false;

        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
            jumpPressed |= kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame;
            _jumpHeld |= kb.spaceKey.isPressed || kb.wKey.isPressed || kb.upArrowKey.isPressed;
            dashPressed |= kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame;
        }
        if (gp != null)
        {
            float stick = gp.leftStick.x.ReadValue();
            if (Mathf.Abs(stick) > 0.3f) x = Mathf.Sign(stick);
            jumpPressed |= gp.buttonSouth.wasPressedThisFrame;
            _jumpHeld |= gp.buttonSouth.isPressed;
            dashPressed |= gp.buttonEast.wasPressedThisFrame;
        }
        if (MobileInput.Available)
        {
            if (Mathf.Abs(MobileInput.MoveX) > 0.01f) x = MobileInput.MoveX;
            jumpPressed |= MobileInput.JumpPressed;
            _jumpHeld |= MobileInput.JumpHeld;
            dashPressed |= MobileInput.DashPressed;
        }

        _moveInput = Mathf.Clamp(x, -1f, 1f);
        if (_moveInput != 0f && !IsDashing) Facing = _moveInput > 0 ? 1 : -1;

        if (jumpPressed) _jumpBufferTimer = jumpBufferTime;
        if (dashPressed) TryDash();
    }

    bool OverlappingEnemy()
    {
        if (_col == null) return false;
        var b = _col.bounds;
        return Physics2D.OverlapBox(b.center, b.size, 0f, passThroughMask) != null;
    }

    void TryDash()
    {
        if (_dashCooldownTimer > 0f || IsDashing) return;
        if (!IsGrounded && _airDashUsed) return;

        _dashTimer = dashDuration;
        _dashCooldownTimer = dashCooldown;
        _dashImmuneTimer = dashDuration + dashImmuneExtra;
        _passingThrough = false;
        if (!IsGrounded) _airDashUsed = true;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        _sinceJump += dt;
        IsGrounded = CheckGrounded();

        if (IsGrounded)
        {
            _coyoteTimer = coyoteTime;
            _airDashUsed = false;
        }
        else _coyoteTimer -= dt;

        // Geri savrulurken kontrol yok, sadece yerçekimi
        if (_knockbackTimer > 0f)
        {
            _knockbackTimer -= dt;
            _rb.gravityScale = _baseGravity;
            return;
        }

        // Dash sırasında yerçekimi yok, sabit hız
        if (IsDashing)
        {
            _dashTimer -= dt;
            _rb.gravityScale = 0f;
            _rb.linearVelocity = new Vector2(Facing * dashSpeed, 0f);
            if (_dashTimer <= 0f) _rb.linearVelocity = new Vector2(Facing * MoveSpeed, 0f);
            return;
        }

        var v = _rb.linearVelocity;

        // Yatay
        float accel = IsGrounded ? groundAcceleration : airAcceleration;
        v.x = Mathf.MoveTowards(v.x, _moveInput * MoveSpeed, accel * dt);

        // Zıplama
        if (_jumpBufferTimer > 0f && _coyoteTimer > 0f)
        {
            v.y = Mathf.Sqrt(2f * Mathf.Abs(Physics2D.gravity.y) * _baseGravity * jumpHeight);
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;
            _sinceJump = 0f;
            IsGrounded = false;
        }

        // Yerde (düz zemin, rampa, merdiven)
        if (IsGrounded)
        {
            // Hareketsizken yerinde dur: rampada aşağı kaymasın
            if (_moveInput == 0f && Mathf.Abs(v.x) < 0.01f)
            {
                _rb.gravityScale = 0f;
                _rb.linearVelocity = Vector2.zero;
                return;
            }
            // Eğimi takip et: yokuş çıkarken yavaşlamasın, inerken havaya sekmesin
            var n = _groundNormal;
            if (n.y > 0.3f && Mathf.Abs(n.x) > 0.01f)
                v.y = -n.x / n.y * v.x;
        }

        // Değişken yerçekimi
        if (v.y < 0f) _rb.gravityScale = _baseGravity * fallGravityMultiplier;
        else if (v.y > 0f && !_jumpHeld) _rb.gravityScale = _baseGravity * lowJumpGravityMultiplier;
        else _rb.gravityScale = _baseGravity;

        v.y = Mathf.Max(v.y, -maxFallSpeed);
        _rb.linearVelocity = v;
    }

    bool CheckGrounded()
    {
        _groundNormal = Vector2.up;
        // Zıpladıktan hemen sonra yerde sayma
        if (_sinceJump < 0.1f) return false;

        var b = _col.bounds;
        var center = new Vector2(b.center.x, b.min.y - 0.05f);
        var size = new Vector2(b.size.x * 0.9f, 0.1f);
        if (Physics2D.OverlapBox(center, size, 0f, groundMask) == null) return false;

        // Zeminin eğimi (rampa/merdiven için)
        var hit = Physics2D.Raycast(new Vector2(b.center.x, b.min.y + 0.05f), Vector2.down, 0.5f, groundMask);
        if (hit.collider != null) _groundNormal = hit.normal;
        return true;
    }

    /// <summary>Karakteri sağa (1) ya da sola (-1) döndürür.</summary>
    public void FaceDirection(int dir)
    {
        if (dir != 0) Facing = dir > 0 ? 1 : -1;
    }

    /// <summary>Oyuncuyu verilen hızla savurur; süre boyunca kontrol devre dışı.</summary>
    public void ApplyKnockback(Vector2 velocity, float duration)
    {
        _knockbackTimer = duration;
        _dashTimer = 0f;
        _sinceJump = 0f;        // hemen "yerde" sayılıp durdurulmasın
        IsGrounded = false;
        _rb.gravityScale = _baseGravity;
        _rb.linearVelocity = velocity;
    }

    /// <summary>Oyuncuyu bir noktaya ışınla (hızı sıfırlar).</summary>
    public void Teleport(Vector3 feetPosition)
    {
        if (_rb == null) _rb = GetComponent<Rigidbody2D>();
        transform.position = feetPosition;
        _rb.position = feetPosition;
        _rb.linearVelocity = Vector2.zero;
        _dashTimer = 0f;
    }

    void OnDrawGizmosSelected()
    {
        var c = GetComponent<CapsuleCollider2D>();
        if (c == null) return;
        var b = c.bounds;
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireCube(new Vector3(b.center.x, b.min.y - 0.05f), new Vector3(b.size.x * 0.9f, 0.1f));
    }
}
