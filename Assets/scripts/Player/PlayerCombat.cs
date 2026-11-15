using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Oyuncunun saldırısı, hasar alınca tepkisi ve ölümü.
///
/// Saldırı: J / sol tık / gamepad X (batı) — baktığı yöne yakın dövüş vuruşu.
/// Hasar alınca: geri savrulur, kısa süre yanıp söner ve hasar almaz.
/// Ölünce: bölüm baştan başlar.
/// </summary>
[RequireComponent(typeof(PlayerController), typeof(Health))]
public class PlayerCombat : MonoBehaviour
{
    [Header("Saldırı")]
    [Tooltip("1. seviyedeki vuruş hasarı")]
    public int attackDamage = 15;
    [Tooltip("Her seviye atlayışta hasara eklenen miktar")]
    public int damagePerLevel = 4;
    public float attackCooldown = 0.35f;
    [Tooltip("Vuruş alanının boyutu")]
    public Vector2 attackSize = new Vector2(1.4f, 1.2f);
    [Tooltip("Vuruş alanının ayaklara göre konumu (x baktığı yöne çevrilir)")]
    public Vector2 attackOffset = new Vector2(0.95f, 0.8f);
    public LayerMask enemyMask;
    [Tooltip("Vuruş anında kısa süre görünen pençe efekti")]
    public SpriteRenderer slashVisual;
    public Sprite slashRight;
    public Sprite slashLeft;
    public float slashShowTime = 0.08f;

    [Header("Hasar alınca")]
    public Vector2 hurtKnockback = new Vector2(7f, 6f);
    public float hurtStunTime = 0.25f;
    [Tooltip("Dokunulmazken yanıp sönecek sprite")]
    public SpriteRenderer bodyRenderer;
    public float blinkInterval = 0.08f;

    [Header("Ölüm")]
    public float restartDelay = 1.5f;

    [Header("Seviye özellikleri")]
    [Tooltip("'Güçlü vuruş' her seçilişte eklenen hasar")]
    public int damagePerk = 5;
    [Tooltip("'Dayanıklılık' her seçilişte en fazla canı bu oranda artırır (0.25 = 100 → 125)")]
    public float maxHealthPerStep = 0.25f;
    [Tooltip("'Çift vuruş'ta ikinci vuruşun gecikmesi")]
    public float doubleHitDelay = 0.15f;

    [Header("Özelliklerin bedelleri")]
    [Tooltip("'Güçlü vuruş' her seçilişte saldırıyı bu oranda yavaşlatır")]
    public float damagePerkSlowdown = 0.15f;
    [Tooltip("'Çift vuruş'ta her vuruşun hasar çarpanı")]
    public float doubleHitDamageFactor = 0.65f;
    [Tooltip("'Dayanıklılık' her seçilişte hareket hızını bu oranda düşürür")]
    public float maxHealthSpeedPenalty = 0.08f;
    [Tooltip("'Can yenile' sonrası bir sonraki seviyenin XP ihtiyacı çarpanı")]
    public float healXpPenalty = 1.3f;

    PlayerController _pc;
    Health _health;
    int _baseMaxHealth;
    Rigidbody2D _rb;
    float _cooldown;
    float _slashTimer;
    readonly HashSet<Health> _hitThisSwing = new HashSet<Health>();
    readonly List<PendingSecondHit> _pendingSecondHits = new List<PendingSecondHit>();

    sealed class PendingSecondHit
    {
        public Coroutine routine;
    }

    void Awake()
    {
        _pc = GetComponent<PlayerController>();
        _health = GetComponent<Health>();
        _rb = GetComponent<Rigidbody2D>();
        _health.Damaged += OnDamaged;
        _health.Died += OnDied;
        _baseMaxHealth = _health.maxHealth;
        if (slashVisual != null)
        {
            slashVisual.enabled = false;
            slashVisual.color = Color.white;
        }
    }

    void OnDisable()
    {
        _slashTimer = 0f;
        if (slashVisual != null) slashVisual.enabled = false;

        for (int i = 0; i < _pendingSecondHits.Count; i++)
            if (_pendingSecondHits[i].routine != null)
                StopCoroutine(_pendingSecondHits[i].routine);
        _pendingSecondHits.Clear();

        if (bodyRenderer != null && (_health == null || !_health.IsDead))
            bodyRenderer.enabled = true;
    }

    void Start()
    {
        // Daha önce alınmış "Dayanıklılık" özellikleri (bölüm başında can dolu başlar)
        if (PlayerProgress.MaxHealthSteps > 0)
            _health.SetMax(MaxHealthFor(PlayerProgress.MaxHealthSteps), int.MaxValue / 2);
    }

    void OnDestroy()
    {
        _health.Damaged -= OnDamaged;
        _health.Died -= OnDied;
    }

    /// <summary>Şu anki seviye ve özelliklere göre vuruş hasarı.</summary>
    public int CurrentDamage => attackDamage + (PlayerProgress.Level - 1) * damagePerLevel + PlayerProgress.BonusDamage;

    /// <summary>Tek bir vuruşun hasarı (çift vuruşta her vuruş biraz zayıf).</summary>
    public int HitDamage => PlayerProgress.DoubleHit
        ? Mathf.Max(1, Mathf.RoundToInt(CurrentDamage * doubleHitDamageFactor))
        : CurrentDamage;

    float CurrentCooldown => attackCooldown * (1f + PlayerProgress.AttackSlowdown);

    /// <summary>Özelliksiz en fazla can (sahnedeki değer).</summary>
    public int BaseMaxHealth => _baseMaxHealth;
    public Health Health => _health;

    int MaxHealthFor(int steps) => _baseMaxHealth + Mathf.RoundToInt(_baseMaxHealth * maxHealthPerStep * steps);

    // ---------------------------------------------------------------- özellikler

    public enum Perk { Damage, Heal, DoubleHit, MaxHealth }

    /// <summary>Seviye atlama ekranında seçilen özelliği uygular.</summary>
    public void ApplyPerk(Perk perk)
    {
        switch (perk)
        {
            case Perk.Damage:
                PlayerProgress.BonusDamage += damagePerk;
                PlayerProgress.AttackSlowdown += damagePerkSlowdown;
                break;
            case Perk.Heal:
                _health.Heal(_health.maxHealth);
                PlayerProgress.NextLevelXpMultiplier *= healXpPenalty;
                PlayerProgress.NotifyChanged();
                break;
            case Perk.DoubleHit:
                PlayerProgress.DoubleHit = true;
                break;
            case Perk.MaxHealth:
                PlayerProgress.MaxHealthSteps++;
                PlayerProgress.MoveSpeedMultiplier = Mathf.Max(0.5f, PlayerProgress.MoveSpeedMultiplier - maxHealthSpeedPenalty);
                int newMax = MaxHealthFor(PlayerProgress.MaxHealthSteps);
                _health.SetMax(newMax, newMax - _health.maxHealth); // eklenen kısım dolu gelir
                break;
        }
    }

    /// <summary>Özelliğin Localization kimlik öneki ("perk.damage" gibi).</summary>
    public static string PerkId(Perk perk)
    {
        switch (perk)
        {
            case Perk.Damage: return "perk.damage";
            case Perk.Heal: return "perk.heal";
            case Perk.DoubleHit: return "perk.double";
            default: return "perk.health";
        }
    }

    /// <summary>Seçim ekranında gösterilecek "şimdi → sonra" metni (seçili dilde).</summary>
    public string PerkPreview(Perk perk)
    {
        string id = PerkId(perk) + ".gain";
        switch (perk)
        {
            case Perk.Damage: return Localization.Get(id, CurrentDamage, CurrentDamage + damagePerk);
            case Perk.Heal: return Localization.Get(id, Mathf.RoundToInt(_health.Percent * 100f));
            case Perk.DoubleHit: return Localization.Get(id);
            case Perk.MaxHealth: return Localization.Get(id, Mathf.RoundToInt(100f * (1f + maxHealthPerStep)));
        }
        return "";
    }

    /// <summary>Seçim ekranında gösterilecek bedel (seçili dilde).</summary>
    public string PerkDrawback(Perk perk)
    {
        string id = PerkId(perk) + ".cost";
        switch (perk)
        {
            case Perk.Damage: return Localization.Get(id, Mathf.RoundToInt(damagePerkSlowdown * 100f));
            case Perk.Heal: return Localization.Get(id, Mathf.RoundToInt((healXpPenalty - 1f) * 100f));
            case Perk.DoubleHit: return Localization.Get(id, Mathf.RoundToInt((1f - doubleHitDamageFactor) * 100f));
            case Perk.MaxHealth: return Localization.Get(id, Mathf.RoundToInt(maxHealthSpeedPenalty * 100f));
        }
        return "";
    }

    /// <summary>Arayüzde gösterilen can (özelliksiz en fazla can = 100).</summary>
    public int ShownHealth(int hp) => Mathf.RoundToInt(hp * 100f / Mathf.Max(1, _baseMaxHealth));

    void Update()
    {
        if (_health.IsDead) return;

        _cooldown -= Time.deltaTime;
        if (_cooldown <= 0f && AttackPressed()) Attack();

        if (slashVisual != null && _slashTimer > 0f)
        {
            _slashTimer -= Time.deltaTime;
            if (_slashTimer <= 0f) slashVisual.enabled = false;
        }

        if (bodyRenderer != null)
            bodyRenderer.enabled = !_health.IsInvulnerable || ((int)(Time.time / blinkInterval)) % 2 == 0;
    }

    static bool AttackPressed()
    {
        if (MobileInput.AttackPressed) return true;
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        var gp = Gamepad.current;
        return (kb != null && kb.jKey.wasPressedThisFrame)
            || (mouse != null && mouse.leftButton.wasPressedThisFrame)
            || (gp != null && gp.buttonWest.wasPressedThisFrame);
    }

    Vector2 AttackCenter =>
        (Vector2)transform.position + new Vector2(attackOffset.x * _pc.Facing, attackOffset.y);

    void Attack()
    {
        _cooldown = CurrentCooldown;
        _hitThisSwing.Clear();

        var center = AttackCenter;
        foreach (var hit in Physics2D.OverlapBoxAll(center, attackSize, 0f, enemyMask))
        {
            var h = hit.GetComponentInParent<Health>();
            if (h == null || h == _health || !_hitThisSwing.Add(h)) continue;
            int dmg = CheatCodes.OneHitKill ? Mathf.Max(HitDamage, h.Current) : HitDamage;
            if (h.TakeDamage(dmg, transform.position))
                FloatingText.Spawn(dmg.ToString(), hit.bounds.center + Vector3.up * (hit.bounds.extents.y + 0.3f),
                                   new Color(1f, 0.95f, 0.85f), 3.5f, 0.7f);
        }

        if (PlayerProgress.DoubleHit && _hitThisSwing.Count > 0)
        {
            var pending = new PendingSecondHit();
            _pendingSecondHits.Add(pending);
            pending.routine = StartCoroutine(SecondHit(new List<Health>(_hitThisSwing), pending));
        }

        ShowSlash(center);
    }

    void ShowSlash(Vector2 center)
    {
        if (slashVisual == null) return;

        var sprite = _pc.Facing >= 0 ? slashRight : slashLeft;
        if (sprite != null) slashVisual.sprite = sprite;
        slashVisual.transform.position = center;
        if (slashVisual.sprite != null)
        {
            float width = Mathf.Max(0.01f, slashVisual.sprite.bounds.size.x);
            float scale = attackSize.x / width;
            slashVisual.transform.localScale = Vector3.one * scale;
        }
        slashVisual.enabled = true;
        _slashTimer = slashShowTime;
    }

    IEnumerator SecondHit(List<Health> targets, PendingSecondHit pending)
    {
        float delay = doubleHitDelay;
        foreach (var h in targets) if (h != null) delay = Mathf.Max(delay, h.invulnerableTime + 0.02f);
        yield return new WaitForSeconds(delay);
        bool hit = false;
        foreach (var h in targets)
        {
            if (h == null || h.IsDead) continue;
            int dmg = CheatCodes.OneHitKill ? Mathf.Max(HitDamage, h.Current) : HitDamage;
            if (h.TakeDamage(dmg, transform.position))
            {
                hit = true;
                var col = h.GetComponent<Collider2D>();
                var top = col != null ? col.bounds.center + Vector3.up * (col.bounds.extents.y + 0.6f) : h.transform.position + Vector3.up * 2f;
                FloatingText.Spawn(dmg.ToString(), top, new Color(1f, 0.75f, 0.35f), 3.5f, 0.7f);
            }
        }
        if (hit) ShowSlash(AttackCenter);
        _pendingSecondHits.Remove(pending);
    }

    void OnDamaged(Vector2 source)
    {
        float dir = Mathf.Sign(transform.position.x - source.x);
        if (dir == 0f) dir = -_pc.Facing;
        _pc.ApplyKnockback(new Vector2(dir * hurtKnockback.x, hurtKnockback.y), hurtStunTime);
    }

    void OnDied()
    {
        _pc.enabled = false;
        _rb.gravityScale = 3f;
        _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
        if (bodyRenderer != null)
        {
            bodyRenderer.enabled = true;
            bodyRenderer.color = new Color(0.5f, 0.3f, 0.3f);
        }
        StartCoroutine(RestartAfterDelay());
    }

    IEnumerator RestartAfterDelay()
    {
        yield return new WaitForSeconds(restartDelay);
        CutsceneState.SkipIntroOnce = true; // ölünce giriş cutscene'i tekrar oynamasın
        CheckpointState.Clear();            // ölünce bölümün en başından başla
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void OnDrawGizmosSelected()
    {
        var pc = GetComponent<PlayerController>();
        int facing = Application.isPlaying && pc != null ? pc.Facing : 1;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position + new Vector3(attackOffset.x * facing, attackOffset.y), attackSize);
    }
}
