using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Level 3'ün hikâyesi (bkz. "Level 3 — Saray: Krallığın Çöküşü — Sahne Tasarımı").
///   Sahne 1  : saraya giriş, asiller, diken, asiller kaçar (açılış). Esc ile atlanır, ölünce tekrar oynamaz.
///   Sahne 2  : üç muhafız grubu (oynanış). 2. grup ölünce Aslan'ın gönderdiği 3. grup gelir (kısa geçiş).
///   Sahne 3  : taht odası (StoryTrigger id=1) → Aslan boss savaşı.
///   Sahne 4  : Aslan yenilir, yalvarır; Kirpi'nin son sözüyle kesilir → ana menü.
/// </summary>
public class PalaceStory : MonoBehaviour, IStoryTriggerTarget
{
    [Header("Referanslar")]
    public PlayerController player;
    public PlayerCombat playerCombat;
    public CameraFollow cameraFollow;
    public CinemaBars bars;
    public DialogueBox dialogue;
    public Image fade;
    public GameObject hudToHide;
    public BossHealthUI bossUI;
    public ChurchDoor palaceDoor;
    public TMP_FontAsset font;
    public Sprite squareSprite;

    [Header("Sahne 1 – asiller")]
    public SpriteRenderer noble1;
    public SpriteRenderer noble2;
    public float introWalkToX = 12f;
    public float kirpiRevealX = 15f;
    public float nobleFleeToX = 30f;

    [Header("Sahne 2 – muhafızlar")]
    public EnemyGate gate1;
    public EnemyGate gate2;
    [Tooltip("Aslan'ın gönderdiği muhafızlar (başta gizli)")]
    public GameObject wave3Root;
    public float throneDoorCamX = 604f;

    [Header("Sahne 3 – taht odası")]
    public LionBoss lion;
    public GameObject[] arenaWalls;
    public float kirpiThroneStopX = 627f;
    public float throneCamX = 640f;
    public float wideCamX = 634f;

    [Header("Sahne 4 – final")]
    public float throneX = 655.5f;
    public float kirpiFinalX = 650.5f;
    public string mainMenuScene = "mainMenu";

    [Header("Müzik")]
    [Tooltip("Aslan savaşı")]
    public AudioClip bossMusic;


    Coroutine _intro;
    bool _introRunning;
    bool _wave3Started;
    TMP_Text _objective;

    // ------------------------------------------------------------------ başlangıç

    void Start()
    {
        SetFade(0f);
        if (arenaWalls != null) foreach (var w in arenaWalls) if (w != null) w.SetActive(false);
        if (wave3Root != null) wave3Root.SetActive(false);
        if (gate2 != null) gate2.enabled = false; // 3. grup gelmeden açılmasın
        if (palaceDoor != null) palaceDoor.Entered += HideObjective;
        if (lion != null)
        {
            lion.bossName = "boss.lion";
            lion.Defeated += () => StartCoroutine(Finale());
            lion.Face(-1);
        }

        if (CutsceneState.SkipIntroOnce)
        {
            CutsceneState.SkipIntroOnce = false;
            IntroEndState();
            ShowObjective("objective.palace");
            return;
        }
        _intro = StartCoroutine(Intro());
    }

    void OnDestroy()
    {
        if (palaceDoor != null) palaceDoor.Entered -= HideObjective;
    }

    void Update()
    {
        if (_introRunning)
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) SkipIntro();
        }

        if (!_wave3Started && gate1 != null && AllDead(gate1))
        {
            _wave3Started = true;
            StartCoroutine(Wave3());
        }
    }

    static bool AllDead(EnemyGate g)
    {
        foreach (var e in g.enemies) if (e != null && !e.IsDead) return false;
        return true;
    }

    public void Trigger(int id)
    {
        if (id == 1) StartCoroutine(ThroneScene());
    }

    // ------------------------------------------------------------------ Sahne 1

    IEnumerator Intro()
    {
        _introRunning = true;
        Lock(true);
        bars.Set(1f);
        SetFade(1f);
        PlaceNobles();
        yield return Wait(0.4f);
        yield return Fade(1f, 0f, 1.5f);

        // Kirpi soldan ağır adımlarla gelir, duvarın gölgesinde durur
        player.ForcedMoveInput = 0.45f;
        float g = 0f;
        while (player.transform.position.x < introWalkToX && g < 8f) { g += Time.deltaTime; yield return null; }
        player.ForcedMoveInput = 0f;
        player.FaceDirection(1);

        // kamera asillere
        CameraManual(true);
        var c0 = cameraFollow.transform.position;
        var nob = new Vector3((noble1.transform.position.x + noble2.transform.position.x) * 0.5f - 1.5f, c0.y, c0.z);
        yield return MoveCamera(c0, nob, 1.4f);

        StartCoroutine(Pulse(noble1.transform, 1.08f, 0.5f));               // omuz silker
        yield return Say("speaker.noble1", "act3.gate.noble1_01");
        StartCoroutine(Laugh(noble2.transform, 1.6f));                      // kahkaha
        yield return Say("speaker.noble2", "act3.gate.noble2_01");
        StartCoroutine(Tilt(noble1.transform, -10f, 0.4f));                 // burnunu kaldırır
        yield return Say("speaker.noble1", "act3.gate.noble1_02");
        StartCoroutine(Nod(noble2.transform));
        yield return Say("speaker.noble2", "act3.gate.noble2_02");
        noble1.transform.localRotation = Quaternion.identity;

        // Kirpi gölgeden çıkar, asiller irkilip döner
        yield return MoveCamera(cameraFollow.transform.position, new Vector3(nob.x - 2f, c0.y, c0.z), 0.6f);
        player.ForcedMoveInput = 0.5f;
        g = 0f;
        while (player.transform.position.x < kirpiRevealX && g < 3f) { g += Time.deltaTime; yield return null; }
        player.ForcedMoveInput = 0f;
        player.FaceDirection(1);
        FaceNoblesTo(player.transform.position.x);
        StartCoroutine(Hop(noble1.transform, 0.35f, 0.2f));
        StartCoroutine(Hop(noble2.transform, 0.35f, 0.2f));
        yield return Wait(0.5f);
        yield return Say("speaker.hedgehog", "act3.gate.hedgehog_01");

        // diken
        var wallTarget = new Vector3((noble1.transform.position.x + noble2.transform.position.x) * 0.5f, 1.6f, 0f);
        yield return ThrowSpike(wallTarget);
        StartCoroutine(Hop(noble1.transform, 0.5f, 0.25f));
        StartCoroutine(Hop(noble2.transform, 0.5f, 0.25f));
        yield return Wait(0.3f);
        StartCoroutine(Flee(noble1, 7f));
        StartCoroutine(Flee(noble2, 8.5f));
        yield return Wait(1.6f);

        yield return MoveCamera(cameraFollow.transform.position, c0, 0.8f);
        yield return bars.Hide(0.5f);
        _introRunning = false;
        CameraManual(false);
        Lock(false);
        ShowObjective("objective.palace");
    }

    void SkipIntro()
    {
        if (_intro != null) StopCoroutine(_intro);
        StopAllCoroutines();
        _introRunning = false;
        if (dialogue != null && dialogue.panel != null) dialogue.panel.SetActive(false);
        bars.Set(0f);
        SetFade(0f);
        CameraManual(false);
        if (player.transform.position.x < introWalkToX) player.Teleport(new Vector3(introWalkToX, 0.05f, 0f));
        cameraFollow.SnapToTarget();
        IntroEndState();
        Lock(false);
        ShowObjective("objective.palace");
    }

    void IntroEndState()
    {
        if (noble1 != null) noble1.gameObject.SetActive(false);
        if (noble2 != null) noble2.gameObject.SetActive(false);
    }

    void PlaceNobles()
    {
        // Kirpi'ye sırtları dönük, birbirlerine bakarak konuşurlar
        noble1.gameObject.SetActive(true);
        noble2.gameObject.SetActive(true);
        noble1.flipX = false; // sağa (Asil 2'ye) bakar
        noble2.flipX = true;  // sola (Asil 1'e) bakar
    }

    /// <summary>Asillerin sprite'ı sağa bakıyor; flipX = sola bakar.</summary>
    void FaceNoblesTo(float x)
    {
        noble1.flipX = x < noble1.transform.position.x;
        noble2.flipX = x < noble2.transform.position.x;
    }

    IEnumerator Flee(SpriteRenderer n, float speed)
    {
        n.flipX = false;
        var t = n.transform;
        float baseY = t.position.y, e = 0f;
        while (t.position.x < nobleFleeToX)
        {
            e += Time.deltaTime;
            t.position = new Vector3(t.position.x + speed * Time.deltaTime, baseY + Mathf.Abs(Mathf.Sin(e * 14f)) * 0.18f, 0f);
            yield return null;
        }
        float a = 1f;
        while (a > 0f)
        {
            a -= Time.deltaTime * 4f;
            var c = n.color; c.a = Mathf.Max(0f, a); n.color = c;
            t.position += Vector3.right * speed * Time.deltaTime;
            yield return null;
        }
        n.gameObject.SetActive(false);
    }

    IEnumerator ThrowSpike(Vector3 to)
    {
        var go = new GameObject("Spike");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = squareSprite;
        sr.color = new Color(0.35f, 0.25f, 0.16f);
        sr.sortingOrder = 30;
        go.transform.localScale = new Vector3(0.55f, 0.06f, 1f);
        var from = player.transform.position + new Vector3(0.5f, 0.9f, 0f);
        var dir = to - from;
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        var vis = PlayerVisual();
        if (vis != null) StartCoroutine(Animate(0.15f, k => vis.localRotation = Quaternion.Euler(0f, 0f, -12f * Mathf.Sin(k * Mathf.PI))));
        yield return Animate(0.25f, k => go.transform.position = Vector3.Lerp(from, to, k));
        Shake(0.3f, 0.25f);
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(0.08f);
        Time.timeScale = 1f;
    }

    // ------------------------------------------------------------------ Sahne 2 – Aslan muhafız gönderir

    IEnumerator Wave3()
    {
        yield return Wait(0.6f); // parmaklık kalkmaya başlasın
        Lock(true);
        CameraManual(true);
        var c0 = cameraFollow.transform.position;
        var door = new Vector3(throneDoorCamX, c0.y, c0.z);
        yield return MoveCamera(c0, door, 0.9f);

        // parmaklığın ardından Aslan'ın gölgesi geçer
        if (lion != null && lion.body != null)
        {
            var shadow = new GameObject("LionShadow");
            var sr = shadow.AddComponent<SpriteRenderer>();
            sr.sprite = lion.idleRight;
            sr.color = new Color(0f, 0f, 0f, 0.55f);
            sr.sortingOrder = -3;
            shadow.transform.localScale = lion.body.transform.lossyScale;
            shadow.transform.position = new Vector3(612f, 0f, 0f);
            StartCoroutine(Animate(1.4f, k => shadow.transform.position = new Vector3(Mathf.Lerp(611.5f, 619f, k), 0f, 0f)));
            Destroy(shadow, 1.5f);
        }
        Shake(0.3f, 1.0f); // uzaktan kükreme
        yield return Wait(1.0f);

        // muhafızlar koşarak gelir
        if (wave3Root != null) wave3Root.SetActive(true);
        if (gate2 != null) gate2.enabled = true;
        Shake(0.15f, 0.4f);
        yield return Wait(0.8f);

        yield return MoveCamera(cameraFollow.transform.position, c0, 0.6f);
        CameraManual(false);
        Lock(false);
    }

    // ------------------------------------------------------------------ Sahne 3 – taht odası

    IEnumerator ThroneScene()
    {
        Lock(true);
        yield return bars.Show(0.4f);
        player.ForcedMoveInput = 0.6f;
        float g = 0f;
        while (player.transform.position.x < kirpiThroneStopX && g < 3f) { g += Time.deltaTime; yield return null; }
        player.ForcedMoveInput = 0f;
        player.FaceDirection(1);

        CameraManual(true);
        var c0 = cameraFollow.transform.position;
        var throne = new Vector3(throneCamX, c0.y, c0.z);
        yield return MoveCamera(c0, throne, 1.4f);

        // Aslan ayağa kalkar, göğsünü şişirir
        lion.Face(-1);
        var lt = lion.body.transform;
        var ls = lt.localScale;
        yield return Animate(0.6f, k => lt.localScale = new Vector3(ls.x, ls.y * Mathf.Lerp(0.88f, 1.04f, Smooth(k)), ls.z));
        yield return Animate(0.2f, k => lt.localScale = new Vector3(ls.x, ls.y * Mathf.Lerp(1.04f, 1f, k), ls.z));
        yield return Say("speaker.lion", "act3.throne.lion_01");

        yield return MoveCamera(throne, new Vector3(wideCamX - 4f, c0.y, c0.z), 0.4f);
        yield return Say("speaker.hedgehog", "act3.throne.hedgehog_01");

        yield return MoveCamera(cameraFollow.transform.position, throne, 0.4f);
        yield return Wait(0.4f);
        lion.Face(1); yield return Wait(0.35f); lion.Face(-1); yield return Wait(0.3f); // kapılara bakar
        yield return Say("speaker.lion", "act3.throne.lion_02");

        // kimse gelmez
        yield return MoveCamera(throne, new Vector3(wideCamX, c0.y, c0.z), 0.8f);
        yield return Wait(1.0f);
        var vis = PlayerVisual();
        if (vis != null)
        {
            var vs = vis.localScale;
            yield return Animate(0.2f, k => vis.localScale = Vector3.Lerp(vs, vs * 1.15f, k));
            yield return Animate(0.35f, k => vis.localScale = Vector3.Lerp(vs * 1.15f, vs, k));
        }
        yield return Say("speaker.hedgehog", "act3.throne.hedgehog_02");

        // Aslan kılıcını kaldırır, kükrer; giriş kapanır
        lion.ShowRaised(true);
        Shake(0.45f, 1.0f);
        if (arenaWalls != null) foreach (var w in arenaWalls) if (w != null) w.SetActive(true);
        yield return Wait(1.1f);
        lion.ShowRaised(false);

        yield return bars.Hide(0.4f);
        CameraManual(false);
        cameraFollow.SnapToTarget();
        Lock(false);
        if (bossUI != null) bossUI.Show(lion.GetComponent<Health>(), "boss.lion");
        MusicPlayer.Play(bossMusic, 0.8f);
        lion.Activate();
    }

    // ------------------------------------------------------------------ Sahne 4 – final

    IEnumerator Finale()
    {
        MusicPlayer.Stop(3f);
        Time.timeScale = 0.3f;
        yield return new WaitForSecondsRealtime(0.35f);
        Time.timeScale = 1f;
        Lock(true);
        if (bossUI != null) bossUI.Hide();
        yield return bars.Show(0.4f);
        yield return Wait(0.6f);

        CameraManual(true);
        var c0 = cameraFollow.transform.position;
        var both = new Vector3(Mathf.Min((player.transform.position.x + lion.transform.position.x) * 0.5f, 648f), c0.y, c0.z);
        StartCoroutine(MoveCamera(c0, both, 1.2f));

        // Aslan yere yığılır, Kirpi ağır ağır yaklaşır
        lion.Face(player.transform.position.x > lion.transform.position.x ? 1 : -1);
        float target = Mathf.Min(kirpiFinalX, throneX - 4.5f);
        int dir = target > player.transform.position.x ? 1 : -1;
        player.ForcedMoveInput = 0.35f * dir;
        float g = 0f;
        while ((target - player.transform.position.x) * dir > 0.1f && g < 6f) { g += Time.deltaTime; yield return null; }
        player.ForcedMoveInput = 0f;
        player.FaceDirection(lion.transform.position.x > player.transform.position.x ? 1 : -1);
        yield return Wait(0.6f);

        yield return Say("speaker.lion", "act3.end.lion_01");
        yield return Say("speaker.hedgehog", "act3.end.hedgehog_01");
        yield return Say("speaker.lion", "act3.end.lion_02");

        // Kirpi'nin dikenleri kabarır
        var vis = PlayerVisual();
        if (vis != null)
        {
            var vs = vis.localScale;
            yield return Animate(0.25f, k => vis.localScale = Vector3.Lerp(vs, vs * 1.18f, k));
        }
        Shake(0.2f, 0.4f);
        yield return Say("speaker.hedgehog", "act3.end.hedgehog_02");

        // anında siyaha kesme, tek sert ses yerine kısa sarsıntı
        SetFade(1f);
        Shake(0.4f, 0.2f);
        yield return new WaitForSecondsRealtime(1.2f);

        Time.timeScale = 1f;
        CutsceneState.End();
        CheckpointState.Clear();
        SceneManager.LoadScene(GameManager.Instance != null ? GameManager.Instance.mainMenuSceneName : mainMenuScene);
    }

    // ------------------------------------------------------------------ küçük animasyonlar

    IEnumerator Pulse(Transform t, float scale, float time)
    {
        var s = t.localScale;
        yield return Animate(time * 0.5f, k => t.localScale = s * Mathf.Lerp(1f, scale, k));
        yield return Animate(time * 0.5f, k => t.localScale = s * Mathf.Lerp(scale, 1f, k));
        t.localScale = s;
    }

    IEnumerator Laugh(Transform t, float time)
    {
        var p = t.position;
        float e = 0f;
        while (e < time)
        {
            e += Time.deltaTime;
            t.position = p + new Vector3(Mathf.Sin(e * 40f) * 0.03f, Mathf.Abs(Mathf.Sin(e * 12f)) * 0.15f, 0f);
            yield return null;
        }
        t.position = p;
    }

    IEnumerator Tilt(Transform t, float angle, float time)
    {
        var a = t.localRotation;
        var b = Quaternion.Euler(0f, 0f, angle);
        yield return Animate(time, k => t.localRotation = Quaternion.Slerp(a, b, Smooth(k)));
    }

    IEnumerator Nod(Transform t)
    {
        for (int i = 0; i < 2; i++)
        {
            yield return Animate(0.15f, k => t.localRotation = Quaternion.Euler(0f, 0f, -8f * k));
            yield return Animate(0.15f, k => t.localRotation = Quaternion.Euler(0f, 0f, -8f * (1f - k)));
        }
    }

    IEnumerator Hop(Transform t, float height, float time)
    {
        var p = t.position;
        yield return Animate(time, k => t.position = p + Vector3.up * Mathf.Sin(k * Mathf.PI) * height);
        t.position = p;
    }

    // ------------------------------------------------------------------ yardımcılar

    void ShowObjective(string key)
    {
        if (_objective == null)
        {
            var canvas = bars != null ? bars.GetComponentInParent<Canvas>() : null;
            if (canvas == null) return;
            var go = new GameObject("Objective", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas.transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-40f, -40f);
            rt.sizeDelta = new Vector2(600f, 50f);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = 30f;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Right;
            t.color = new Color(1f, 0.9f, 0.6f);
            t.raycastTarget = false;
            _objective = t;
        }
        // "▸" yazı tipinde yok; "»" var. Dil değişince metin kendiliğinden yenilenir.
        var lt = _objective.GetComponent<LocalizedText>();
        if (lt == null) lt = _objective.gameObject.AddComponent<LocalizedText>();
        lt.prefix = "» ";
        lt.SetKey(key);
        _objective.gameObject.SetActive(true);
    }

    void HideObjective()
    {
        if (_objective != null) _objective.gameObject.SetActive(false);
    }

    Transform PlayerVisual()
    {
        var a = player.GetComponentInChildren<PlayerAnimator>();
        return a != null ? a.transform : null;
    }

    void Lock(bool locked)
    {
        if (locked) CutsceneState.Begin(); else if (CutsceneState.IsActive) CutsceneState.End();
        if (playerCombat != null) playerCombat.enabled = !locked;
        player.ForcedMoveInput = locked ? 0f : (float?)null;
        if (hudToHide != null) hudToHide.SetActive(!locked);
        if (_objective != null) _objective.enabled = !locked;
    }

    void CameraManual(bool manual)
    {
        if (cameraFollow != null) cameraFollow.enabled = !manual;
    }

    void Shake(float amp, float time)
    {
        if (cameraFollow != null) cameraFollow.Shake(amp, time);
    }

    IEnumerator MoveCamera(Vector3 from, Vector3 to, float time)
    {
        float t = 0f;
        while (t < time)
        {
            if (cameraFollow.enabled) yield break;
            t += Time.deltaTime;
            cameraFollow.transform.position = Vector3.Lerp(from, to, Smooth(t / time));
            yield return null;
        }
        cameraFollow.transform.position = to;
    }

    /// <summary>Konuşma: speakerKey ve lineKey Localization kimlikleridir.</summary>
    IEnumerator Say(string speakerKey, string lineKey)
    {
        if (dialogue == null) yield break;
        yield return dialogue.PlayKeys(speakerKey, lineKey);
    }

    static float Smooth(float k) { k = Mathf.Clamp01(k); return k * k * (3f - 2f * k); }

    static IEnumerator Wait(float s)
    {
        float t = 0f;
        while (t < s) { t += Time.deltaTime; yield return null; }
    }

    static IEnumerator Animate(float time, System.Action<float> step)
    {
        float t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            step(Mathf.Clamp01(t / time));
            yield return null;
        }
        step(1f);
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
        fade.color = new Color(0f, 0f, 0f, a);
        fade.raycastTarget = false;
        fade.enabled = a > 0.001f;
    }
}
