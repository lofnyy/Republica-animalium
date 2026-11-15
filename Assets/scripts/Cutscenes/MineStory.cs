using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Level 1'in hikâyesi (bkz. "Kirpi: Maden İsyanı — Cutscene Dokümanı").
///   Sahne 1–4 : açılış (orman → maden → Porsuk → isyan). Esc ile atlanır, ölünce tekrar oynamaz.
///   Sahne 5   : maden çıkışında Gergedan (StoryTrigger id=1). Zemin çöker, alt kattaki arenada boss.
///   Sahne 6   : faz 2 repliği + boss ölünce lamba düşer, yangın ve çökme.
///   Sahne 7–8 : Köstebek ile merdivenden kaçış, çıkış çöker; Kirpi yalnız köye yürür → sonraki bölüm.
/// Animasyonu olmayan karakterler kaydırma, zıplatma, döndürme ile canlandırılır.
/// </summary>
public class MineStory : MonoBehaviour, IStoryTriggerTarget
{
    [Header("Referanslar")]
    public PlayerController player;
    public PlayerCombat playerCombat;
    public CameraFollow cameraFollow;
    public CinemaBars bars;
    public DialogueBox dialogue;
    [Tooltip("Tam ekran siyah görsel")]
    public Image fade;
    public GameObject hudToHide;
    public BossHealthUI bossUI;

    [Header("Karakterler")]
    public SpriteRenderer kostebek;
    public SpriteRenderer porsuk;
    public Sprite porsukRight;
    public Sprite porsukLeft;
    public Sprite[] porsukWalkRight;
    public Sprite[] porsukWalkLeft;
    public float porsukWalkFps = 8f;
    [Tooltip("Arka plandaki işçi siluetleri (ilki Kunduz)")]
    public SpriteRenderer[] workers;

    [Header("Sahne 1 – orman")]
    public Vector3 forestCamStart = new Vector3(116f, 25f, -10f);
    public Vector3 forestCamEnd = new Vector3(124f, 25f, -10f);
    public Vector3 undergroundCam = new Vector3(124f, 6f, -10f);

    [Header("Sahne 2–4 – konumlar (x)")]
    public float kirpiX = 9.6f;
    public float kostebekX = 7.4f;
    public float porsukStartX = -13f;
    public float porsukStopX = 6.2f;

    [Header("Sahne 5 – Gergedan")]
    public KingBoss boss;
    public float bossEnterFromX = 63f;
    public float bossStopX = 57.5f;
    public float kirpiStopX = 52f;
    public Rect arenaBounds = new Rect(298f, -42f, 44f, 17.5f);
    public Vector3 arenaPlayerSpot = new Vector3(304f, -39.9f, 0f);
    public Vector3 arenaBossSpot = new Vector3(330f, -39.9f, 0f);

    [Header("Sahne 6 – final")]
    [Tooltip("Düşecek gaz lambası (HangingLamp)")]
    public HangingLamp fallingLamp;
    public float arenaFloorY = -40f;
    public Sprite debrisSprite;
    public Sprite glowSprite;
    public Material unlitMaterial;
    [Tooltip("Çıkış çöküşünde köstebeğin üzerine düşen taş kareleri")]
    public Sprite[] moleFallRockSprites;

    [Header("Sahne 7 – kaçış")]
    public Vector3 escapeStart = new Vector3(50f, 0.1f, 0f);
    public float escapeTalkX = 72f;
    public float collapseX = 86.5f;
    [Tooltip("Çöküşten sonra merdiven ağzını kapatan enkaz (başta kapalı)")]
    public GameObject rubble;
    public LayerMask groundMask;

    [Header("Sahne 8")]
    public float finalWalkTime = 6f;
    public string fallbackNextScene = "LEVEL2";

    [Header("Müzik")]
    [Tooltip("Madende çalan fon müziği")]
    public AudioClip mineMusic;
    [Tooltip("Gergedan savaşı")]
    public AudioClip bossMusic;

    const string Narrator = "speaker.narrator";

    Coroutine _intro;
    Coroutine _carry;
    bool _introRunning;
    bool _escapeFollow;
    Vector3 _kostebekBase;

    // ------------------------------------------------------------------ başlangıç

    void Start()
    {
        if (boss != null)
        {
            // Gergedan baştan merdivenin dibinde bekler (uyur, hasar almaz); Kirpi gelince konuşma başlar
            boss.gameObject.SetActive(true);
            boss.transform.position = new Vector3(bossStopX, 0.05f, 0f);
            var bossHp = boss.GetComponent<Health>();
            if (bossHp != null) bossHp.Immune = true;
            boss.Enraged += () => StartCoroutine(PhaseTwo());
            var bh = boss.GetComponent<Health>();
            if (bh != null) bh.Died += () => StartCoroutine(Finale());
        }
        if (rubble != null) rubble.SetActive(false);
        SetFade(0f);
        MusicPlayer.Play(mineMusic, 3f);

        if (CutsceneState.SkipIntroOnce)
        {
            CutsceneState.SkipIntroOnce = false;
            IntroEndState();
            return;
        }
        _intro = StartCoroutine(Intro());
    }

    void Update()
    {
        if (_introRunning)
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) SkipIntro();
        }
        if (_escapeFollow) FollowKirpi();
    }

    // ------------------------------------------------------------------ Sahne 1–4

    IEnumerator Intro()
    {
        _introRunning = true;
        Lock(true);
        bars.Set(1f);
        SetFade(1f);

        // oyuncu ve karakterler yerinde
        player.Teleport(new Vector3(kirpiX, 0.05f, 0f));
        player.FaceDirection(-1);
        PlaceKostebek(kostebekX, false);
        porsuk.gameObject.SetActive(false);
        foreach (var w in workers) SetAlpha(w, 0f);

        // --- Sahne 1: orman, kamera yerin altına iner
        CameraManual(true);
        cameraFollow.transform.position = forestCamStart;
        yield return Fade(1f, 0f, 1.2f);
        StartCoroutine(MoveCamera(forestCamStart, forestCamEnd, 7f));
        yield return Say(Narrator, "act1.intro.narrator_01");
        yield return MoveCamera(cameraFollow.transform.position, undergroundCam, 2.5f);
        yield return Fade(0f, 1f, 0.8f);

        // --- Sahne 2: maden
        CameraManual(false);
        cameraFollow.SnapToTarget();
        yield return Fade(1f, 0f, 1f);
        if (workers.Length > 0) _carry = StartCoroutine(CarryLoop(workers[0], kirpiX + 3f, kirpiX + 8f));
        yield return Dig(2.2f);
        yield return Say("speaker.hedgehog", "act1.intro.hedgehog_01");
        yield return Tilt(kostebek.transform, -8f, 0.3f);
        yield return Say("speaker.mole", "act1.intro.mole_01");
        yield return Tilt(kostebek.transform, 0f, 0.2f);

        // donma + anlatıcı
        yield return Fade(0f, 0.45f, 0.3f);
        yield return Say(Narrator, "act1.intro.narrator_02");
        yield return Fade(0.45f, 0f, 0.3f);
        cameraFollow.Shake(0.05f, 0.2f);
        yield return Say("speaker.hedgehog", "act1.intro.hedgehog_02");

        // --- Sahne 3: Porsuk
        porsuk.gameObject.SetActive(true);
        porsuk.sprite = porsukRight;
        porsuk.transform.position = new Vector3(porsukStartX, 0f, 0f);
        porsuk.transform.rotation = Quaternion.identity;
        yield return Walk(porsuk.transform, porsukStopX, 2.4f, 0.12f, 0.45f,
            porsuk, porsukWalkRight, porsukWalkLeft, porsukRight, porsukLeft, porsukWalkFps);
        PlaceKostebek(kostebek.transform.position.x, true); // Porsuk'a döner
        yield return Say("speaker.badger", "act1.intro.badger_01");
        yield return Lunge(porsuk.transform, 0.5f, 0.12f);
        Flash(Color.white, 0.12f);
        cameraFollow.Shake(0.35f, 0.3f);
        yield return Fall(kostebek.transform, -90f, 0.25f, 0.35f);
        yield return Wait(1.2f);

        // --- Sahne 4: isyan
        cameraFollow.Shake(0.06f, 1f);
        yield return Wait(0.9f);
        player.ForcedMoveInput = -1f;
        float guard = 0f;
        while (player.transform.position.x > porsuk.transform.position.x + 1.2f && guard < 2f) { guard += Time.deltaTime; yield return null; }
        player.ForcedMoveInput = 0f;
        Flash(Color.white, 0.1f);
        cameraFollow.Shake(0.4f, 0.35f);
        porsuk.sprite = porsukRight;
        yield return Knockdown(porsuk.transform, -1.4f, 90f, 0.4f);
        yield return Wait(0.6f);
        player.FaceDirection(1);
        yield return Fall(kostebek.transform, 0f, 0f, 0.6f);   // doğrulur
        PlaceKostebek(kostebek.transform.position.x, false);
        yield return Walk(kostebek.transform, player.transform.position.x + 1.3f, 1.6f, 0.08f, 0.35f);
        yield return Wait(0.4f);

        foreach (var w in workers) StartCoroutine(FadeSprite(w, w.color.a, 1f, 1.2f));
        yield return Wait(1.2f);
        player.FaceDirection(1);
        yield return Say("speaker.hedgehog", "act1.intro.hedgehog_03");
        cameraFollow.Shake(0.25f, 0.5f);
        if (_carry != null) StopCoroutine(_carry);
        foreach (var w in workers) StartCoroutine(RunAway(w.transform, 5.5f + Random.Range(0f, 1.5f)));
        StartCoroutine(RunAway(kostebek.transform, 6f));
        yield return Wait(1.2f);

        yield return bars.Hide(0.5f);
        _introRunning = false;
        IntroEndState();
    }

    void SkipIntro()
    {
        if (_intro != null) StopCoroutine(_intro);
        _introRunning = false;
        if (dialogue != null && dialogue.panel != null) dialogue.panel.SetActive(false);
        bars.Set(0f);
        SetFade(0f);
        IntroEndState();
    }

    /// <summary>Açılış bitmiş hâli: Porsuk baygın, işçiler ve Köstebek gitmiş, kontrol oyuncuda.</summary>
    void IntroEndState()
    {
        StopAllCoroutinesExceptNothing();
        CameraManual(false);
        if (porsuk != null)
        {
            porsuk.gameObject.SetActive(true);
            porsuk.sprite = porsukRight;
            porsuk.transform.position = new Vector3(porsukStopX - 1.4f, 0f, 0f);
            porsuk.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
        }
        if (kostebek != null) kostebek.gameObject.SetActive(false);
        foreach (var w in workers) if (w != null) w.gameObject.SetActive(false);
        if (player.transform.position.x < -50f || player.transform.position.x > 200f)
            player.Teleport(new Vector3(kirpiX, 0.05f, 0f));
        cameraFollow.SnapToTarget();
        Lock(false);
    }

    void StopAllCoroutinesExceptNothing()
    {
        // Açılışa ait yan döngüler (taşıma, koşma) dursun
        StopAllCoroutines();
    }

    // ------------------------------------------------------------------ Sahne 5

    /// <summary>StoryTrigger çağırır. id 1 = maden çıkışı (Gergedan).</summary>
    public void Trigger(int id)
    {
        if (id == 1) StartCoroutine(BossIntro());
    }

    IEnumerator BossIntro()
    {
        Lock(true);
        yield return bars.Show(0.4f);
        player.ForcedMoveInput = 0.6f;
        float g = 0f;
        while (player.transform.position.x < kirpiStopX && g < 3f) { g += Time.deltaTime; yield return null; }
        player.ForcedMoveInput = 0f;
        player.FaceDirection(1);

        // Gergedan zaten merdivenin dibinde bekliyor
        var rb = boss.GetComponent<Rigidbody2D>();
        rb.linearVelocity = Vector2.zero;
        cameraFollow.Shake(0.15f, 0.25f);
        yield return Wait(0.6f);

        yield return Say("speaker.rhino", "act1.boss.rhino_01");
        yield return Say("speaker.hedgehog", "act1.boss.hedgehog_01");
        yield return Say("speaker.rhino", "act1.boss.rhino_02");

        // yere vurur, zemin çöker
        rb.linearVelocity = new Vector2(0f, 9f);
        yield return Wait(0.55f);
        cameraFollow.Shake(0.7f, 1.2f);
        for (int i = 0; i < 26; i++) SpawnDebris(player.transform.position.x + Random.Range(-9f, 9f), 13.5f, 6);
        yield return Wait(0.5f);
        yield return Fade(0f, 1f, 0.3f);

        // alt kat
        player.Teleport(arenaPlayerSpot);
        player.FaceDirection(1);
        boss.transform.position = arenaBossSpot;
        rb.linearVelocity = Vector2.zero;
        cameraFollow.SetBoundsOverride(arenaBounds);
        cameraFollow.SnapToTarget();
        yield return Wait(0.5f);
        cameraFollow.Shake(0.3f, 0.6f);
        yield return Fade(1f, 0f, 0.5f);
        yield return bars.Hide(0.4f);

        Lock(false);
        var bh = boss.GetComponent<Health>();
        if (bh != null) bh.Immune = false; // dövüş başladı, artık hasar alır
        MusicPlayer.Play(bossMusic, 0.8f);
        boss.Activate();
        if (bossUI != null) bossUI.Show(boss.GetComponent<Health>(), "boss.rhino");
    }

    // ------------------------------------------------------------------ Sahne 6

    IEnumerator PhaseTwo()
    {
        ClearHazards();
        Lock(true);
        yield return bars.Show(0.25f);
        cameraFollow.Shake(0.4f, 0.8f);
        yield return Say("speaker.rhino", "act1.boss.rhino_phase2");
        yield return bars.Hide(0.25f);
        Lock(false);
    }

    IEnumerator Finale()
    {
        MusicPlayer.Stop(2.5f);
        ClearHazards();
        Lock(true);
        if (bossUI != null) bossUI.Hide();
        yield return bars.Show(0.4f);
        yield return Wait(0.9f);

        // lamba kopar ve düşer
        if (fallingLamp != null)
        {
            fallingLamp.enabled = false;
            var rope = fallingLamp.transform.Find("Rope");
            var lamp = fallingLamp.transform.Find("GasLamp");
            var light = fallingLamp.transform.Find("Light");
            if (rope != null) rope.gameObject.SetActive(false);
            float vy = 0f;
            while (lamp != null && lamp.position.y > arenaFloorY + 0.3f)
            {
                vy -= 30f * Time.deltaTime;
                lamp.position += Vector3.up * vy * Time.deltaTime;
                lamp.Rotate(0f, 0f, 220f * Time.deltaTime);
                if (light != null) light.position = lamp.position;
                yield return null;
            }
            cameraFollow.Shake(0.3f, 0.3f);
            if (lamp != null) StartCoroutine(SpreadFire(lamp.position.x));
        }
        yield return Wait(1.4f);

        // tavan çöker
        cameraFollow.ShakeAmplitude = 0.25f;
        float t = 0f;
        while (t < 2.2f)
        {
            t += Time.deltaTime;
            if (Random.value < Time.deltaTime * 18f)
                SpawnDebris(cameraFollow.transform.position.x + Random.Range(-11f, 11f), arenaBounds.yMax - 1f, 6);
            yield return null;
        }
        yield return Fade(0f, 1f, 0.8f);
        cameraFollow.ShakeAmplitude = 0f;
        yield return Escape();
    }

    IEnumerator SpreadFire(float fromX)
    {
        float[] offs = { 0f, -2f, 2f, -4.5f, 4f, -7f, -9.5f };
        foreach (var o in offs)
        {
            SpawnFlame(new Vector3(fromX + o, arenaFloorY, 0f), 1f + Random.Range(0f, 0.6f));
            yield return Wait(0.18f);
        }
    }

    // ------------------------------------------------------------------ Sahne 7

    IEnumerator Escape()
    {
        cameraFollow.ClearBoundsOverride();
        foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None)) e.gameObject.SetActive(false);
        player.Teleport(escapeStart);
        player.FaceDirection(1);
        cameraFollow.SnapToTarget();

        // arkadaki yangının ışığı ikisinin üstüne vurur
        var glowGo = new GameObject("EscapeGlow");
        glowGo.transform.SetParent(player.transform, false);
        glowGo.transform.localPosition = new Vector3(-1.5f, 1.2f, 0f);
        var glow = glowGo.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
        glow.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
        glow.color = new Color(1f, 0.6f, 0.3f);
        glow.intensity = 0.9f;
        glow.pointLightOuterRadius = 6f;
        glow.pointLightInnerRadius = 0.5f;
        glowGo.AddComponent<FireLight>();

        // arkada yanan maden
        for (float x = escapeStart.x - 22f; x < escapeStart.x - 1f; x += 2.6f)
            SpawnFlame(new Vector3(x + Random.Range(-0.6f, 0.6f), 0f, 0f), 1.1f + Random.Range(0f, 0.7f));

        kostebek.gameObject.SetActive(true);
        SetAlpha(kostebek, 1f);
        kostebek.transform.rotation = Quaternion.Euler(0f, 0f, 6f); // yaralı, eğik
        PlaceKostebek(escapeStart.x - 0.9f, false);
        _escapeFollow = true;

        yield return Fade(1f, 0f, 1f);

        player.ForcedMoveInput = 0.38f;
        StartCoroutine(AmbientDebris(escapeStart.x, collapseX + 2f));
        float g = 0f;
        while (player.transform.position.x < escapeTalkX && g < 12f) { g += Time.deltaTime; yield return null; }
        player.ForcedMoveInput = 0f;
        yield return Wait(0.4f);
        yield return Say("speaker.mole", "act1.escape.mole_01");
        player.FaceDirection(-1);
        yield return Wait(0.5f);
        yield return Say("speaker.hedgehog", "act1.escape.hedgehog_01");
        player.FaceDirection(1);

        player.ForcedMoveInput = 0.45f;
        g = 0f;
        while (player.transform.position.x < collapseX && g < 12f) { g += Time.deltaTime; yield return null; }

        // çıkışın eşiğinde çöküş
        _escapeFollow = false;
        player.ForcedMoveInput = 0f;
        player.ApplyKnockback(new Vector2(7f, 5f), 0.35f);
        cameraFollow.Shake(0.8f, 1.5f);
        for (int i = 0; i < 30; i++) SpawnDebris(collapseX - 3f + Random.Range(-1.5f, 1.5f), 21f + Random.Range(0f, 4f), 11);
        yield return DropRocksOnMole();
        yield return Wait(0.45f);
        var dust = new Color(0.55f, 0.5f, 0.45f, 1f);
        if (fade != null) fade.color = new Color(dust.r, dust.g, dust.b, 0f);
        yield return Fade(0f, 0.95f, 0.35f);
        kostebek.gameObject.SetActive(false);
        if (rubble != null) rubble.SetActive(true);
        Destroy(glowGo);
        yield return Wait(1.2f);
        yield return Fade(0.95f, 0f, 2.5f);
        if (fade != null) fade.color = new Color(0f, 0f, 0f, 0f);
        yield return Wait(0.6f);
        player.FaceDirection(-1); // enkaza bakar
        yield return Wait(2.8f);
        yield return Fade(0f, 1f, 1.4f);

        // --- Sahne 8: final
        yield return Wait(0.6f);
        yield return Fade(1f, 0f, 1.2f);
        yield return Wait(1.5f);
        player.FaceDirection(1);
        yield return Wait(0.6f);
        CameraManual(true);
        var camStart = cameraFollow.transform.position;
        StartCoroutine(MoveCamera(camStart, camStart + new Vector3(2f, 3.5f, 0f), finalWalkTime + 1.5f));
        player.ForcedMoveInput = 0.35f;
        yield return Wait(finalWalkTime);
        yield return Fade(0f, 1f, 1.5f);
        yield return Wait(0.5f);

        CutsceneState.End();
        if (GameManager.Instance != null) GameManager.Instance.NextChapter();
        else SceneManager.LoadScene(fallbackNextScene);
    }

    void FollowKirpi()
    {
        if (kostebek == null || !kostebek.gameObject.activeSelf) return;
        var k = kostebek.transform;
        float targetX = player.transform.position.x - 0.95f;
        float x = Mathf.MoveTowards(k.position.x, targetX, 6f * Time.deltaTime);
        float y = GroundY(x, player.transform.position.y + 2f);
        float bob = Mathf.Abs(Mathf.Sin(Time.time * 7f)) * 0.06f * Mathf.Clamp01(Mathf.Abs(player.Velocity.x));
        k.position = new Vector3(x, y + bob, 0f);
    }

    // ------------------------------------------------------------------ yardımcılar

    void Lock(bool locked)
    {
        if (locked) CutsceneState.Begin(); else if (CutsceneState.IsActive) CutsceneState.End();
        if (playerCombat != null) playerCombat.enabled = !locked;
        player.ForcedMoveInput = locked ? 0f : (float?)null;
        if (hudToHide != null) hudToHide.SetActive(!locked);
    }

    void CameraManual(bool manual)
    {
        if (cameraFollow == null) return;
        cameraFollow.enabled = !manual;
    }

    IEnumerator MoveCamera(Vector3 from, Vector3 to, float time)
    {
        float t = 0f;
        while (t < time)
        {
            if (cameraFollow.enabled) yield break; // takip geri açıldıysa bırak
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / time);
            cameraFollow.transform.position = Vector3.Lerp(from, to, k);
            yield return null;
        }
    }

    /// <summary>Konuşma: speakerKey ve lineKey Localization kimlikleridir.</summary>
    IEnumerator Say(string speakerKey, string lineKey)
    {
        if (dialogue == null) yield break;
        yield return dialogue.PlayKeys(speakerKey, lineKey);
    }

    static IEnumerator Wait(float s)
    {
        float t = 0f;
        while (t < s) { t += Time.deltaTime; yield return null; }
    }

    IEnumerator Fade(float from, float to, float time)
    {
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            SetFade(Mathf.Lerp(from, to, Mathf.Clamp01(t / time)));
            yield return null;
        }
        SetFade(to);
    }

    void SetFade(float a)
    {
        if (fade == null) return;
        var c = fade.color; c.a = a; fade.color = c;
        fade.raycastTarget = false;
        fade.enabled = a > 0.001f;
    }

    void Flash(Color c, float time) => StartCoroutine(FlashRoutine(c, time));

    IEnumerator FlashRoutine(Color c, float time)
    {
        if (fade == null) yield break;
        var old = fade.color;
        fade.color = new Color(c.r, c.g, c.b, 0.8f);
        fade.enabled = true;
        yield return Wait(time);
        fade.color = new Color(old.r, old.g, old.b, 0f);
        fade.enabled = false;
    }

    void PlaceKostebek(float x, bool faceLeft)
    {
        if (kostebek == null) return;
        kostebek.gameObject.SetActive(true);
        kostebek.flipX = faceLeft;
        var p = kostebek.transform.position;
        kostebek.transform.position = new Vector3(x, GroundY(x, Mathf.Max(p.y, 0f) + 2f), 0f);
    }

    float GroundY(float x, float fromY)
    {
        var hit = Physics2D.Raycast(new Vector2(x, fromY), Vector2.down, 8f, groundMask);
        return hit.collider != null ? hit.point.y : 0f;
    }

    /// <summary>Kirpi kazar: görsel öne eğilip sarsılır; Köstebek sallanır.</summary>
    IEnumerator Dig(float time)
    {
        var vis = player.GetComponentInChildren<PlayerAnimator>();
        Transform v = vis != null ? vis.transform : null;
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            float swing = Mathf.Sin(t * 9f);
            if (v != null) v.localRotation = Quaternion.Euler(0f, 0f, Mathf.Max(0f, swing) * 10f);
            if (kostebek != null) kostebek.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 6f + 1f) * 4f);
            if (swing > 0.98f) cameraFollow.Shake(0.03f, 0.08f);
            yield return null;
        }
        if (v != null) v.localRotation = Quaternion.identity;
        if (kostebek != null) kostebek.transform.localRotation = Quaternion.identity;
    }

    IEnumerator Tilt(Transform t, float angle, float time)
    {
        var from = t.localRotation;
        var to = Quaternion.Euler(0f, 0f, angle);
        float e = 0f;
        while (e < time) { e += Time.deltaTime; t.localRotation = Quaternion.Slerp(from, to, e / time); yield return null; }
        t.localRotation = to;
    }

    /// <summary>Yürüyüş kayması; isteğe bağlı olarak kare animasyonu da oynatır.</summary>
    IEnumerator Walk(Transform t, float toX, float speed, float hop, float stepTime,
        SpriteRenderer animatedSprite = null, Sprite[] walkRightFrames = null, Sprite[] walkLeftFrames = null,
        Sprite idleSpriteRight = null, Sprite idleSpriteLeft = null, float framesPerSecond = 8f)
    {
        float baseY = t.position.y;
        float e = 0f;
        bool movingRight = toX >= t.position.x;
        var frames = movingRight ? walkRightFrames : walkLeftFrames;
        int frameIndex = 0;
        float frameTimer = 0f;
        float frameDuration = 1f / Mathf.Max(0.01f, framesPerSecond);
        while (Mathf.Abs(t.position.x - toX) > 0.02f)
        {
            e += Time.deltaTime;
            float x = Mathf.MoveTowards(t.position.x, toX, speed * Time.deltaTime);
            float y = baseY + Mathf.Abs(Mathf.Sin(e * Mathf.PI / stepTime)) * hop;
            t.position = new Vector3(x, y, t.position.z);
            if (animatedSprite != null && frames != null && frames.Length > 0)
            {
                frameTimer += Time.deltaTime;
                while (frameTimer >= frameDuration)
                {
                    frameTimer -= frameDuration;
                    frameIndex = (frameIndex + 1) % frames.Length;
                }
                if (frames[frameIndex] != null) animatedSprite.sprite = frames[frameIndex];
            }
            yield return null;
        }
        t.position = new Vector3(toX, baseY, t.position.z);
        if (animatedSprite != null)
        {
            var idle = movingRight ? idleSpriteRight : idleSpriteLeft;
            if (idle != null) animatedSprite.sprite = idle;
        }
    }

    IEnumerator Lunge(Transform t, float dist, float time)
    {
        var a = t.position; var b = a + Vector3.right * dist;
        float e = 0f;
        while (e < time) { e += Time.deltaTime; t.position = Vector3.Lerp(a, b, e / time); yield return null; }
        e = 0f;
        while (e < time) { e += Time.deltaTime; t.position = Vector3.Lerp(b, a, e / time); yield return null; }
        t.position = a;
    }

    /// <summary>Devrilme / doğrulma: pivot ayakta olduğu için yana döner.</summary>
    IEnumerator Fall(Transform t, float angle, float slideX, float time)
    {
        var fromRot = t.rotation; var toRot = Quaternion.Euler(0f, 0f, angle);
        var fromPos = t.position; var toPos = fromPos + Vector3.right * slideX;
        float e = 0f;
        while (e < time)
        {
            e += Time.deltaTime;
            float k = Mathf.Clamp01(e / time); k *= k;
            t.rotation = Quaternion.Slerp(fromRot, toRot, k);
            t.position = Vector3.Lerp(fromPos, toPos, k);
            yield return null;
        }
        t.rotation = toRot;
    }

    IEnumerator Knockdown(Transform t, float dx, float angle, float time)
    {
        var a = t.position;
        float e = 0f;
        while (e < time)
        {
            e += Time.deltaTime;
            float k = Mathf.Clamp01(e / time);
            t.position = a + new Vector3(dx * k, Mathf.Sin(k * Mathf.PI) * 0.6f, 0f);
            t.rotation = Quaternion.Euler(0f, 0f, angle * k);
            yield return null;
        }
    }

    IEnumerator CarryLoop(SpriteRenderer w, float x0, float x1)
    {
        if (w == null) yield break;
        SetAlpha(w, 0.85f);
        var t = w.transform;
        bool right = true;
        while (_introRunning && w.gameObject.activeSelf)
        {
            yield return Walk(t, right ? x1 : x0, 1.4f, 0.06f, 0.4f);
            right = !right;
            w.flipX = !right;
        }
    }

    IEnumerator RunAway(Transform t, float speed)
    {
        var sr = t.GetComponent<SpriteRenderer>();
        if (sr != null) sr.flipX = false;
        float baseY = t.position.y;
        float e = 0f;
        float startX = t.position.x;
        while (t.position.x < startX + 26f)
        {
            e += Time.deltaTime;
            t.position = new Vector3(t.position.x + speed * Time.deltaTime, baseY + Mathf.Abs(Mathf.Sin(e * 9f)) * 0.12f, 0f);
            yield return null;
        }
        t.gameObject.SetActive(false);
    }

    IEnumerator FadeSprite(SpriteRenderer sr, float from, float to, float time)
    {
        if (sr == null) yield break;
        sr.gameObject.SetActive(true);
        float e = 0f;
        while (e < time) { e += Time.deltaTime; SetAlpha(sr, Mathf.Lerp(from, to, e / time)); yield return null; }
        SetAlpha(sr, to);
    }

    static void SetAlpha(SpriteRenderer sr, float a)
    {
        if (sr == null) return;
        var c = sr.color; c.a = a; sr.color = c;
    }

    IEnumerator AmbientDebris(float x0, float x1)
    {
        while (_escapeFollow)
        {
            if (Random.value < 0.25f)
                SpawnDebris(player.transform.position.x + Random.Range(-6f, 4f), player.transform.position.y + 7f, 6);
            yield return Wait(0.15f);
        }
    }

    IEnumerator DropRocksOnMole()
    {
        if (kostebek == null || !kostebek.gameObject.activeInHierarchy) yield break;
        if (moleFallRockSprites == null || moleFallRockSprites.Length == 0)
        {
            Debug.LogWarning("MineStory has no mole fall rock sprites assigned.", this);
            yield break;
        }

        var molePosition = kostebek.transform.position;
        float[] xOffsets = { -0.55f, 0.55f, -0.2f, 0.25f };
        for (int i = 0; i < moleFallRockSprites.Length; i++)
        {
            var sprite = moleFallRockSprites[i];
            if (sprite == null)
            {
                Debug.LogWarning("MineStory has a missing mole fall rock sprite.", this);
                continue;
            }

            var rock = new GameObject("MoleFallRock");
            int debrisLayer = LayerMask.NameToLayer("Debris");
            if (debrisLayer >= 0) rock.layer = debrisLayer;
            float scale = 1.6f / Mathf.Max(sprite.bounds.size.x, 0.01f);
            float height = sprite.bounds.size.y * scale;
            var renderer = rock.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 12 + i;
            if (unlitMaterial != null) renderer.sharedMaterial = unlitMaterial;
            rock.transform.localScale = new Vector3(scale, scale, 1f);

            float xOffset = xOffsets[i % xOffsets.Length];
            float stackHeight = i < 2 ? 0f : height * 0.45f;
            var start = new Vector3(molePosition.x + xOffset, molePosition.y + 8f + i * 0.25f, 0f);
            var end = new Vector3(molePosition.x + xOffset, molePosition.y + height * 0.5f + stackHeight, 0f);
            float startAngle = Random.Range(-25f, 25f);
            float endAngle = Random.Range(-15f, 15f);
            rock.transform.SetPositionAndRotation(start, Quaternion.Euler(0f, 0f, startAngle));

            float elapsed = 0f;
            const float fallDuration = 0.35f;
            while (elapsed < fallDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fallDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                rock.transform.position = Vector3.Lerp(start, end, eased);
                rock.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(startAngle, endAngle, eased));
                yield return null;
            }

            rock.transform.SetPositionAndRotation(end, Quaternion.Euler(0f, 0f, endAngle));
            if (cameraFollow != null) cameraFollow.Shake(0.18f, 0.2f);
            yield return Wait(0.12f);
        }
    }

    void ClearHazards()
    {
        foreach (var s in FindObjectsByType<Shockwave>(FindObjectsSortMode.None)) Destroy(s.gameObject);
        foreach (var r in FindObjectsByType<FallingRock>(FindObjectsSortMode.None)) Destroy(r.gameObject);
    }

    void SpawnDebris(float x, float y, int order)
    {
        if (debrisSprite == null) return;
        var go = new GameObject("Debris");
        int layer = LayerMask.NameToLayer("Debris");
        if (layer >= 0) go.layer = layer;
        float size = Random.Range(0.15f, 0.5f);
        go.transform.position = new Vector3(x, y, 0f);
        go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));
        go.transform.localScale = new Vector3(size, size * Random.Range(0.7f, 1.2f), 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = debrisSprite;
        sr.color = new Color(0.32f, 0.28f, 0.24f) * Random.Range(0.8f, 1.2f);
        sr.sortingOrder = order;
        go.AddComponent<BoxCollider2D>();
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 2f;
        rb.angularVelocity = Random.Range(-360f, 360f);
        Destroy(go, 4f + Random.Range(0f, 1f));
    }

    void SpawnFlame(Vector3 pos, float scale)
    {
        var root = new GameObject("Flame");
        root.transform.position = pos;
        for (int i = 0; i < 3; i++)
        {
            var g = new GameObject("Tongue");
            g.transform.SetParent(root.transform, false);
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = glowSprite;
            if (unlitMaterial != null) sr.sharedMaterial = unlitMaterial;
            sr.color = i == 0 ? new Color(1f, 0.45f, 0.1f, 0.85f) : i == 1 ? new Color(1f, 0.7f, 0.2f, 0.8f) : new Color(1f, 0.95f, 0.6f, 0.75f);
            sr.sortingOrder = 7 + i;
            float s = scale * (1.4f - i * 0.4f);
            g.transform.localScale = new Vector3(s * 0.8f, s * 1.3f, 1f);
            g.transform.localPosition = new Vector3(0f, s * 0.5f, 0f);
        }
        var lgo = new GameObject("FireLight");
        lgo.transform.SetParent(root.transform, false);
        lgo.transform.localPosition = new Vector3(0f, scale * 0.6f, 0f);
        var l = lgo.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
        l.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
        l.color = new Color(1f, 0.55f, 0.25f);
        l.intensity = 1.4f;
        l.pointLightOuterRadius = 4.5f * scale;
        l.pointLightInnerRadius = 0.3f;
        lgo.AddComponent<FireLight>();
        root.AddComponent<FlameFlicker>();
    }
}
