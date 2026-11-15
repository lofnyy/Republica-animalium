using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Level 2'nin hikâyesi (bkz. "Level 2 — Köy ve Kilise: Sahne Tasarımı").
///   Sahne 1   : köye dönüş (açılış). Esc ile atlanır, ölünce tekrar oynamaz.
///   Sahne 2   : yıkık ev + Kaplumbağa (StoryTrigger id=1).
///   Sahne 3–4 : kilisede Baykuş'un vaazı, Kirpi'nin isyanı, dart, kalabalık kaçar (kilise kapısından girince).
///   Sahne 5   : boss savaşı; Baykuş yenilince gözlüğü düşer, Kirpi alır.
///   Sahne 6   : kürsüdeki boş parşömen + gözlük merceği → Kral'ın emri.
///   Sahne 7   : Kirpi kiliseden çıkar, saraya yönelir → sonraki bölüm.
/// </summary>
public class VillageStory : MonoBehaviour, IStoryTriggerTarget
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
    public HiddenScrollReader scrollReader;
    public TMP_FontAsset font;

    [Header("Sahne 1 – köye dönüş")]
    public float introWalkToX = 22f;
    public float ruinFocusX = 38f;

    [Header("Sahne 2 – ev ve Kaplumbağa")]
    public float houseStopX = 167f;
    public float houseFocusX = 172f;
    public Transform turtle;
    public NpcFacing turtleFacing;
    public float kirpiNearTurtleX = 175.5f;
    public float churchViewX = 297f;

    [Header("Kilise")]
    public ChurchDoor churchDoor;
    public TallBoss owl;
    [Tooltip("Vaaz sırasında Baykuş'un durduğu yer (kürsünün üstü)")]
    public Vector3 owlPulpitSpot = new Vector3(531f, 2.15f, 0f);
    [Tooltip("Savaş başında Baykuş'un indiği yer")]
    public Vector3 owlArenaSpot = new Vector3(519f, 0f, 0f);
    public SpriteRenderer[] crowd;
    public GameObject doorBars;
    public float churchDoorX = 502f;
    public float kirpiChurchX = 505.5f;
    [Tooltip("Kamera: kapı / kalabalık / kürsü / geniş plan (x)")]
    public float camDoorX = 511.5f;
    public float camPulpitX = 529.5f;
    public float camWideX = 516.5f;
    public Sprite squareSprite;

    [Header("Sahne 5–6 – gözlük ve parşömen")]
    public Sprite monocleSprite;
    public float monocleScale = 0.2f;
    [Tooltip("Kürsüdeki katlı parşömen")]
    public SpriteRenderer pulpitScroll;
    [Tooltip("Parşömenin üstündeki '[E] Al' yazısı")]
    public GameObject pulpitPrompt;

    [Header("Sahne 7 – final")]
    public Vector3 outsideSpot = new Vector3(306.5f, 0.05f, 0f);
    public Vector2 palacePan = new Vector2(8f, 3f);
    public string fallbackNextScene = "LEVEL3";

    [Header("Müzik")]
    [Tooltip("Baykuş savaşı")]
    public AudioClip bossMusic;

    const string Narrator = "speaker.narrator";

    Coroutine _intro;
    bool _introRunning;
    bool _awaitScroll;
    bool _busy;
    bool _turtleWatch;
    float _churchCamY;
    GameObject _monocle;
    GameObject _owlSpot;
    GameObject _scrollGlow;
    TMP_Text _objective;

    // ------------------------------------------------------------------ başlangıç

    void Start()
    {
        SetFade(0f);
        if (doorBars != null) doorBars.SetActive(false);
        LocalizedText.Bind(pulpitPrompt, "prompt.take");
        if (pulpitPrompt != null) pulpitPrompt.SetActive(false);
        if (churchDoor != null) churchDoor.Entered += OnChurchEntered;

        if (owl != null)
        {
            owl.stayOnDeath = true;
            owl.bossName = "boss.owl";
            PlaceOwlOnPulpit();
            var oh = owl.GetComponent<Health>();
            if (oh != null) oh.Died += OnOwlDied;
            owl.Defeated += () => StartCoroutine(BossOutro());
        }
        foreach (var c in crowd) if (c != null) { c.gameObject.SetActive(true); c.flipX = false; }

        if (CutsceneState.SkipIntroOnce)
        {
            CutsceneState.SkipIntroOnce = false;
            return;
        }
        _intro = StartCoroutine(Intro());
    }

    void OnDestroy()
    {
        if (churchDoor != null) churchDoor.Entered -= OnChurchEntered;
    }

    void Update()
    {
        if (_introRunning)
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) SkipIntro();
        }

        // Kaplumbağa sahneden sonra Kirpi'yi bakışlarıyla izler
        if (_turtleWatch && turtleFacing != null && turtle != null && !CutsceneState.IsActive)
            turtleFacing.Face(player.transform.position.x > turtle.position.x ? 1 : -1);

        if (_awaitScroll && !_busy && pulpitScroll != null)
        {
            bool near = Mathf.Abs(player.transform.position.x - pulpitScroll.transform.position.x) < 2.8f;
            if (pulpitPrompt != null && pulpitPrompt.activeSelf != near) pulpitPrompt.SetActive(near);
            // parşömen hafifçe parlar
            float k = 0.75f + 0.25f * Mathf.Sin(Time.time * 4f);
            pulpitScroll.color = new Color(1f, 1f, k, 1f);
            if (near && InteractPressed()) StartCoroutine(ReadScroll());
        }
    }

    public void Trigger(int id)
    {
        if (id == 1) StartCoroutine(HouseScene());
    }

    // ------------------------------------------------------------------ Sahne 1

    IEnumerator Intro()
    {
        _introRunning = true;
        Lock(true);
        bars.Set(1f);
        SetFade(1f);
        yield return Wait(0.3f);
        yield return Fade(1f, 0f, 1.5f);

        // Kirpi soldan ağır adımlarla girer
        player.ForcedMoveInput = 0.45f;
        float g = 0f;
        while (player.transform.position.x < introWalkToX - 8f && g < 6f) { g += Time.deltaTime; yield return null; }
        yield return Say(Narrator, "act2.village.narrator_01");
        g = 0f;
        while (player.transform.position.x < introWalkToX && g < 6f) { g += Time.deltaTime; yield return null; }
        player.ForcedMoveInput = 0f;
        player.FaceDirection(1);

        // kamera ilk yıkık eve kayar, artçı sarsıntı
        CameraManual(true);
        var camStart = cameraFollow.transform.position;
        var ruin = new Vector3(ruinFocusX, camStart.y, camStart.z);
        yield return MoveCamera(camStart, ruin, 2f);
        cameraFollow.transform.position = ruin;
        Shake(0.2f, 0.6f);
        yield return Wait(1.6f);
        yield return MoveCamera(ruin, camStart, 1.2f);

        yield return bars.Hide(0.5f);
        _introRunning = false;
        CameraManual(false);
        Lock(false);
    }

    void SkipIntro()
    {
        if (_intro != null) StopCoroutine(_intro);
        _introRunning = false;
        if (dialogue != null && dialogue.panel != null) dialogue.panel.SetActive(false);
        bars.Set(0f);
        SetFade(0f);
        CameraManual(false);
        if (player.transform.position.x < introWalkToX - 4f)
            player.Teleport(new Vector3(introWalkToX, 0.05f, 0f));
        cameraFollow.SnapToTarget();
        Lock(false);
    }

    // ------------------------------------------------------------------ Sahne 2

    IEnumerator HouseScene()
    {
        Lock(true);
        yield return bars.Show(0.4f);

        // Kirpi eve koşar
        player.ForcedMoveInput = 1f;
        float g = 0f;
        while (player.transform.position.x < houseStopX && g < 4f) { g += Time.deltaTime; yield return null; }
        player.ForcedMoveInput = 0f;
        player.FaceDirection(1);

        // kamera evin önüne sabitlenir, yavaşça yaklaşır
        CameraManual(true);
        var cam = Camera.main;
        float baseSize = cam.orthographicSize;
        var c0 = cameraFollow.transform.position;
        var house = new Vector3(houseFocusX - 1.5f, c0.y - 0.6f, c0.z);
        yield return MoveCamera(c0, house, 1.2f);
        yield return Animate(2f, k => cam.orthographicSize = Mathf.Lerp(baseSize, baseSize * 0.78f, Smooth(k)));

        yield return Say("speaker.hedgehog", "act2.home.hedgehog_01");
        // başını eğer
        var vis = PlayerVisual();
        Vector3 vScale = vis != null ? vis.localScale : Vector3.one;
        if (vis != null) yield return Animate(0.6f, k => vis.localScale = new Vector3(vScale.x, vScale.y * Mathf.Lerp(1f, 0.9f, k), vScale.z));
        yield return Wait(2f);
        yield return Say(Narrator, "act2.home.narrator_01");
        if (vis != null) yield return Animate(0.4f, k => vis.localScale = new Vector3(vScale.x, vScale.y * Mathf.Lerp(0.9f, 1f, k), vScale.z));

        // kamera sağa açılır, Kaplumbağa
        var both = new Vector3(turtle != null ? (player.transform.position.x + turtle.position.x) * 0.5f : house.x, house.y, house.z);
        StartCoroutine(Animate(1.2f, k => cam.orthographicSize = Mathf.Lerp(baseSize * 0.78f, baseSize, Smooth(k))));
        yield return MoveCamera(house, both, 1.2f);

        // Kirpi Kaplumbağa'ya birkaç adım yaklaşır
        player.ForcedMoveInput = 0.5f;
        g = 0f;
        while (player.transform.position.x < kirpiNearTurtleX && g < 4f) { g += Time.deltaTime; yield return null; }
        player.ForcedMoveInput = 0f;
        player.FaceDirection(1);
        yield return Say("speaker.hedgehog", "act2.home.hedgehog_02");

        // Kaplumbağa yavaşça döner
        yield return Wait(0.5f);
        if (turtleFacing != null) turtleFacing.Face(-1);
        yield return Wait(0.5f);
        yield return Say("speaker.tortoise", "act2.home.tortoise_01");
        if (turtleFacing != null) turtleFacing.Face(1); // kiliseye bakar

        // uzakta kilise kulesi
        var church = new Vector3(churchViewX, both.y + 1.5f, both.z);
        yield return MoveCamera(both, church, 2.2f);
        Shake(0.02f, 0.4f);
        yield return Wait(2f);
        yield return MoveCamera(church, both, 1.4f);
        cam.orthographicSize = baseSize;

        _turtleWatch = true; // Kirpi'yi izler
        yield return bars.Hide(0.4f);
        CameraManual(false);
        Lock(false);
        ShowObjective("objective.church");
    }

    // ------------------------------------------------------------------ Sahne 3–4

    void OnChurchEntered()
    {
        HideObjective();
        StartCoroutine(ChurchScene());
    }

    IEnumerator ChurchScene()
    {
        Lock(true);
        bars.Set(1f);
        PlaceOwlOnPulpit();
        // vitraydan Baykuş'un üstüne düşen ışık
        _owlSpot = MakeLight("OwlSpot", owlPulpitSpot + new Vector3(0f, 3f, 0f), new Color(1f, 0.92f, 0.75f), 1.3f, 6.5f);
        _churchCamY = cameraFollow.transform.position.y;
        CameraManual(true);
        var z = cameraFollow.transform.position.z;
        Vector3 Cam(float x) => new Vector3(x, _churchCamY, z);

        player.FaceDirection(1);
        cameraFollow.transform.position = Cam(camDoorX);
        yield return Wait(1f);

        // kürsüye kayar
        yield return MoveCamera(Cam(camDoorX), Cam(camPulpitX), 3f);
        yield return OwlPulse(1.08f, 0.5f);
        yield return Say("speaker.owl", "act2.church.owl_01");

        StartCoroutine(OwlLean(8f, 0.35f));
        StartCoroutine(CrowdShift(0.25f, 0.3f));
        yield return Say("speaker.owl", "act2.church.owl_02");

        StartCoroutine(MoveCamera(Cam(camPulpitX), Cam(camWideX + 2f), 3f));
        StartCoroutine(CrowdTremble(2.5f));
        yield return Say("speaker.owl", "act2.church.owl_03");

        yield return MoveCamera(cameraFollow.transform.position, Cam(camPulpitX), 0.6f);
        yield return OwlPulse(1.15f, 0.25f);
        Shake(0.25f, 0.35f);
        yield return Say("speaker.owl", "act2.church.owl_04");

        // --- Sahne 4: isyan
        yield return MoveCamera(Cam(camPulpitX), Cam(camDoorX), 0.4f);
        player.ForcedMoveInput = 1f;
        float g = 0f;
        while (player.transform.position.x < kirpiChurchX && g < 1.5f) { g += Time.deltaTime; yield return null; }
        player.ForcedMoveInput = 0f;
        player.FaceDirection(1);
        yield return Say("speaker.hedgehog", "act2.church.hedgehog_01");

        // herkes Kirpi'ye döner
        yield return MoveCamera(Cam(camDoorX), Cam(camWideX), 0.8f);
        for (int i = 0; i < crowd.Length; i++)
        {
            if (crowd[i] != null) crowd[i].flipX = true;
            yield return Wait(0.1f);
        }
        yield return Wait(0.5f);
        yield return Say("speaker.hedgehog", "act2.church.hedgehog_02");

        // dart
        yield return ThrowDart();
        // kalabalık kaçar
        foreach (var c in crowd) if (c != null) StartCoroutine(Flee(c));
        yield return Wait(1.6f);
        Shake(0.3f, 0.4f); // kapı çarpar
        yield return Wait(0.6f);

        // --- Sahne 5: giriş
        if (_owlSpot != null) Destroy(_owlSpot);
        yield return OwlFlyTo(owlArenaSpot, 1.1f);
        if (doorBars != null) doorBars.SetActive(true);
        Shake(0.2f, 0.4f);
        yield return Wait(0.4f);
        yield return OwlPulse(1.12f, 0.4f);
        yield return bars.Hide(0.4f);
        CameraManual(false);
        cameraFollow.SnapToTarget();
        Lock(false);

        MusicPlayer.Play(bossMusic, 0.8f);
        owl.Activate();
        if (bossUI != null) bossUI.Show(owl.GetComponent<Health>(), "boss.owl");
    }

    IEnumerator ThrowDart()
    {
        var go = new GameObject("Dart");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = squareSprite;
        sr.color = new Color(0.35f, 0.25f, 0.16f);
        sr.sortingOrder = 30;
        go.transform.localScale = new Vector3(0.55f, 0.06f, 1f);
        var from = player.transform.position + new Vector3(0.5f, 0.9f, 0f);
        var to = new Vector3(owlPulpitSpot.x - 1.1f, owlPulpitSpot.y + 0.4f, 0f);
        var dir = to - from;
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        // fırlatma pozu
        var vis = PlayerVisual();
        if (vis != null) StartCoroutine(Animate(0.15f, k => vis.localRotation = Quaternion.Euler(0f, 0f, -12f * Mathf.Sin(k * Mathf.PI))));

        yield return Animate(0.3f, k => go.transform.position = Vector3.Lerp(from, to, k));
        // saplanır: kısa donma + sarsıntı, Baykuş havalanır
        Shake(0.35f, 0.3f);
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(0.1f);
        Time.timeScale = 1f;
        if (owl != null && owl.body != null) StartCoroutine(FlashSprite(owl.body, 0.12f));
        yield return OwlHop(1.2f, 0.4f);
    }

    // ------------------------------------------------------------------ Sahne 5 – bitiş

    void OnOwlDied()
    {
        MusicPlayer.Stop(2.5f);
        StartCoroutine(SlowMo());
        if (owl == null || monocleSprite == null) return;
        // gözlük yüzünden kopar
        var b = owl.GetComponent<Collider2D>().bounds;
        float side = player.transform.position.x < b.center.x ? -1f : 1f;
        _monocle = new GameObject("Monocle");
        _monocle.transform.position = new Vector3(b.center.x + side * 0.4f, b.max.y - 0.9f, 0f);
        _monocle.transform.localScale = Vector3.one * monocleScale;
        var sr = _monocle.AddComponent<SpriteRenderer>();
        sr.sprite = monocleSprite;
        sr.sortingOrder = 25;
        StartCoroutine(MonocleBounce(_monocle.transform, side));
    }

    IEnumerator SlowMo()
    {
        Time.timeScale = 0.3f;
        yield return new WaitForSecondsRealtime(0.35f);
        Time.timeScale = 1f;
    }

    IEnumerator MonocleBounce(Transform t, float side)
    {
        Vector2 v = new Vector2(side * 2.2f, 5f);
        float floor = 0.3f;
        int bounces = 0;
        float spin = side * -360f;
        while (bounces < 3)
        {
            v.y -= 25f * Time.deltaTime;
            t.position += (Vector3)(v * Time.deltaTime);
            t.Rotate(0f, 0f, spin * Time.deltaTime);
            if (t.position.y <= floor && v.y < 0f)
            {
                t.position = new Vector3(t.position.x, floor, 0f);
                v = new Vector2(v.x * 0.6f, -v.y * 0.4f);
                spin *= 0.5f;
                bounces++;
            }
            yield return null;
        }
        // yarım daire yuvarlanıp durur
        float e = 0f;
        var r0 = t.rotation;
        while (e < 0.5f)
        {
            e += Time.deltaTime;
            t.position += new Vector3(v.x * 0.5f * (1f - e / 0.5f) * Time.deltaTime, 0f, 0f);
            t.rotation = Quaternion.Slerp(r0, Quaternion.identity, e / 0.5f);
            yield return null;
        }
        t.position = new Vector3(t.position.x, floor, 0f);
        t.rotation = Quaternion.identity;
    }

    IEnumerator BossOutro()
    {
        Lock(true);
        if (bossUI != null) bossUI.Hide();
        yield return bars.Show(0.3f);
        yield return Wait(0.5f);

        if (_monocle != null)
        {
            // cam ışık yakalar
            var msr = _monocle.GetComponent<SpriteRenderer>();
            yield return FlashSprite(msr, 0.15f);
            yield return Wait(0.3f);

            // Kirpi yürüyüp alır
            float tx = _monocle.transform.position.x;
            int dir = tx > player.transform.position.x ? 1 : -1;
            player.ForcedMoveInput = dir * 0.6f;
            float g = 0f;
            while (Mathf.Abs(player.transform.position.x - tx) > 0.5f && g < 5f)
            {
                if ((tx - player.transform.position.x) * dir < 0f) break;
                g += Time.deltaTime; yield return null;
            }
            player.ForcedMoveInput = 0f;
            player.FaceDirection(dir);
            var vis = PlayerVisual();
            Vector3 vs = vis != null ? vis.localScale : Vector3.one;
            if (vis != null) yield return Animate(0.2f, k => vis.localScale = new Vector3(vs.x, vs.y * Mathf.Lerp(1f, 0.8f, k), vs.z));
            Destroy(_monocle);
            if (vis != null) yield return Animate(0.2f, k => vis.localScale = new Vector3(vs.x, vs.y * Mathf.Lerp(0.8f, 1f, k), vs.z));
            if (scrollReader != null) scrollReader.ShowItem(monocleSprite, "item.monocle");
            yield return Wait(1.2f);
        }

        yield return bars.Hide(0.3f);
        Lock(false);
        _awaitScroll = true;
        if (pulpitScroll != null)
            _scrollGlow = MakeLight("ScrollGlow", pulpitScroll.transform.position + Vector3.up * 0.3f, new Color(1f, 0.85f, 0.55f), 1.2f, 2.5f);
    }

    // ------------------------------------------------------------------ Sahne 6

    IEnumerator ReadScroll()
    {
        _busy = true;
        _awaitScroll = false;
        if (pulpitPrompt != null) pulpitPrompt.SetActive(false);
        if (pulpitScroll != null) pulpitScroll.gameObject.SetActive(false);
        if (_scrollGlow != null) Destroy(_scrollGlow);
        Lock(true);
        player.FaceDirection(1);
        yield return Wait(0.2f);
        if (scrollReader != null) yield return scrollReader.Play(dialogue);
        yield return Final();
    }

    // ------------------------------------------------------------------ Sahne 7

    IEnumerator Final()
    {
        yield return bars.Show(0.4f);
        var cam = Camera.main;
        float baseSize = cam.orthographicSize;
        CameraManual(true);
        var c0 = cameraFollow.transform.position;
        var close = new Vector3(player.transform.position.x, c0.y - 0.8f, c0.z);
        StartCoroutine(MoveCamera(c0, close, 1.5f));
        yield return Animate(1.5f, k => cam.orthographicSize = Mathf.Lerp(baseSize, baseSize * 0.75f, Smooth(k)));

        // titrer, dikenleri kabarır, ekran kızarır
        var vis = PlayerVisual();
        Vector3 vs = vis != null ? vis.localScale : Vector3.one;
        var vp = vis != null ? vis.localPosition : Vector3.zero;
        yield return Animate(0.8f, k =>
        {
            if (vis != null) vis.localPosition = vp + new Vector3(Mathf.Sin(k * 90f) * 0.04f, 0f, 0f);
        });
        if (vis != null) vis.localPosition = vp;
        Shake(0.25f, 0.5f);
        StartCoroutine(RedPulse(0.7f));
        if (vis != null)
        {
            yield return Animate(0.25f, k => vis.localScale = Vector3.Lerp(vs, vs * 1.15f, k));
            yield return Animate(0.5f, k => vis.localScale = Vector3.Lerp(vs * 1.15f, vs, k));
        }
        yield return Wait(0.6f);

        // yerdeki Baykuş'a bir kez bakar, sonra arkasını döner
        if (owl != null) player.FaceDirection(owl.transform.position.x > player.transform.position.x ? 1 : -1);
        yield return Wait(1.2f);
        player.FaceDirection(-1);
        yield return Wait(0.5f);
        cam.orthographicSize = baseSize;

        // parmaklık kalkar, kapıya yürür
        if (doorBars != null) doorBars.SetActive(false);
        Shake(0.15f, 0.3f);
        CameraManual(false);
        player.ForcedMoveInput = -0.6f;
        float g = 0f;
        while (player.transform.position.x > churchDoorX && g < 3f) { g += Time.deltaTime; yield return null; }

        // kapıdan çıkış (beyaza geçiş)
        yield return Fade(0f, 1f, 0.5f, Color.white);
        player.ForcedMoveInput = 0f;
        if (churchDoor != null) churchDoor.ApplyOutside();
        player.Teleport(outsideSpot);
        player.FaceDirection(1);
        cameraFollow.SnapToTarget();
        yield return Wait(0.3f);
        yield return Fade(1f, 0f, 0.8f, Color.white);
        Shake(0.2f, 0.5f); // gök gürültüsü
        yield return Wait(0.8f);

        // ufukta saray, şimşek
        CameraManual(true);
        var o0 = cameraFollow.transform.position;
        var view = o0 + new Vector3(palacePan.x, palacePan.y, 0f);
        yield return MoveCamera(o0, view, 2.5f);
        yield return Lightning();
        yield return Wait(0.8f);

        player.ForcedMoveInput = 0.45f;
        yield return Wait(2.2f);
        yield return Fade(0f, 1f, 2f, Color.black);
        yield return Wait(0.5f);

        Time.timeScale = 1f;
        CutsceneState.End();
        if (GameManager.Instance != null) GameManager.Instance.NextChapter();
        else SceneManager.LoadScene(fallbackNextScene);
    }

    IEnumerator Lightning()
    {
        if (fade == null) yield break;
        for (int i = 0; i < 2; i++)
        {
            fade.color = new Color(1f, 1f, 1f, i == 0 ? 0.85f : 0.5f);
            fade.enabled = true;
            yield return Wait(0.07f);
            fade.color = new Color(1f, 1f, 1f, 0f);
            fade.enabled = false;
            yield return Wait(0.12f);
        }
        Shake(0.15f, 0.6f);
    }

    IEnumerator RedPulse(float time)
    {
        if (fade == null) yield break;
        yield return Animate(time, k =>
        {
            fade.color = new Color(0.55f, 0.05f, 0.05f, Mathf.Sin(k * Mathf.PI) * 0.35f);
            fade.enabled = true;
        });
        SetFade(0f);
    }

    // ------------------------------------------------------------------ Baykuş ve kalabalık

    void PlaceOwlOnPulpit()
    {
        if (owl == null) return;
        var rb = owl.GetComponent<Rigidbody2D>();
        rb.simulated = false;
        rb.linearVelocity = Vector2.zero;
        owl.transform.position = owlPulpitSpot;
    }

    IEnumerator OwlPulse(float scale, float time)
    {
        if (owl == null || owl.body == null) yield break;
        var t = owl.body.transform;
        var s = t.localScale;
        yield return Animate(time * 0.5f, k => t.localScale = new Vector3(s.x * Mathf.Lerp(1f, scale, k), s.y * Mathf.Lerp(1f, 1f + (scale - 1f) * 0.4f, k), s.z));
        yield return Animate(time * 0.5f, k => t.localScale = new Vector3(s.x * Mathf.Lerp(scale, 1f, k), s.y * Mathf.Lerp(1f + (scale - 1f) * 0.4f, 1f, k), s.z));
        t.localScale = s;
    }

    IEnumerator OwlLean(float angle, float time)
    {
        if (owl == null || owl.body == null) yield break;
        var t = owl.body.transform;
        yield return Animate(time, k => t.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Sin(k * Mathf.PI)));
        t.localRotation = Quaternion.identity;
    }

    IEnumerator OwlHop(float height, float time)
    {
        var p = owl.transform.position;
        yield return Animate(time, k => owl.transform.position = p + Vector3.up * Mathf.Sin(k * Mathf.PI) * height);
        owl.transform.position = p;
    }

    IEnumerator OwlFlyTo(Vector3 to, float time)
    {
        var from = owl.transform.position;
        yield return Animate(time, k =>
        {
            float e = Smooth(k);
            owl.transform.position = Vector3.Lerp(from, to, e) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 2.5f;
        });
        owl.transform.position = to;
        var rb = owl.GetComponent<Rigidbody2D>();
        rb.simulated = true;
        rb.linearVelocity = Vector2.zero;
        Shake(0.2f, 0.3f);
    }

    IEnumerator CrowdShift(float dx, float time)
    {
        var start = new Vector3[crowd.Length];
        for (int i = 0; i < crowd.Length; i++) if (crowd[i] != null) start[i] = crowd[i].transform.position;
        yield return Animate(time, k =>
        {
            for (int i = 0; i < crowd.Length; i++)
                if (crowd[i] != null) crowd[i].transform.position = start[i] + Vector3.left * dx * Smooth(k);
        });
    }

    IEnumerator CrowdTremble(float time)
    {
        var start = new Vector3[crowd.Length];
        for (int i = 0; i < crowd.Length; i++) if (crowd[i] != null) start[i] = crowd[i].transform.localScale;
        yield return Animate(time, k =>
        {
            for (int i = 0; i < crowd.Length; i++)
                if (crowd[i] != null)
                {
                    float bow = Mathf.Sin(Mathf.Min(1f, k * 3f) * Mathf.PI * 0.5f) * 0.08f;   // başını eğer
                    float shiver = (i % 3 == 0) ? Mathf.Sin(Time.time * 50f + i) * 0.015f : 0f; // biri titrer
                    crowd[i].transform.localScale = new Vector3(start[i].x * (1f + shiver), start[i].y * (1f - bow), start[i].z);
                }
        });
    }

    IEnumerator Flee(SpriteRenderer c)
    {
        yield return Wait(Random.Range(0f, 0.35f));
        c.flipX = true;
        float speed = Random.Range(6f, 9f);
        var t = c.transform;
        float baseY = t.position.y, e = 0f;
        while (t.position.x > churchDoorX)
        {
            e += Time.deltaTime;
            t.position = new Vector3(t.position.x - speed * Time.deltaTime, baseY + Mathf.Abs(Mathf.Sin(e * 12f)) * 0.15f, 0f);
            yield return null;
        }
        yield return FadeSprite(c, c.color.a, 0f, 0.15f);
        c.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------ yardımcılar

    static GameObject MakeLight(string name, Vector3 pos, Color color, float intensity, float radius)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var l = go.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
        l.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.pointLightOuterRadius = radius;
        l.pointLightInnerRadius = radius * 0.2f;
        return go;
    }

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

    static bool InteractPressed()
    {
        var kb = Keyboard.current;
        var gp = Gamepad.current;
        return (kb != null && kb.eKey.wasPressedThisFrame) || (gp != null && gp.buttonNorth.wasPressedThisFrame);
    }

    void Lock(bool locked)
    {
        if (locked) CutsceneState.Begin(); else if (CutsceneState.IsActive) CutsceneState.End();
        if (playerCombat != null) playerCombat.enabled = !locked;
        player.ForcedMoveInput = locked ? 0f : (float?)null;
        if (hudToHide != null) hudToHide.SetActive(!locked);
        if (_objective != null && _objective.text.Length > 0) _objective.enabled = !locked;
    }

    void CameraManual(bool manual)
    {
        if (cameraFollow == null) return;
        cameraFollow.enabled = !manual;
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
            t += Time.deltaTime;
            step(Mathf.Clamp01(t / time));
            yield return null;
        }
        step(1f);
    }

    IEnumerator Fade(float from, float to, float time) => Fade(from, to, time, Color.black);

    IEnumerator Fade(float from, float to, float time, Color color)
    {
        if (fade == null) yield break;
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            SetFade(Mathf.Lerp(from, to, Mathf.Clamp01(t / time)), color);
            yield return null;
        }
        SetFade(to, color);
    }

    void SetFade(float a) => SetFade(a, Color.black);

    void SetFade(float a, Color color)
    {
        if (fade == null) return;
        fade.color = new Color(color.r, color.g, color.b, a);
        fade.raycastTarget = false;
        fade.enabled = a > 0.001f;
    }

    IEnumerator FlashSprite(SpriteRenderer sr, float time)
    {
        if (sr == null) yield break;
        var c = sr.color;
        sr.color = Color.white * 2f;
        yield return Wait(time);
        sr.color = c;
    }

    static IEnumerator FadeSprite(SpriteRenderer sr, float from, float to, float time)
    {
        float e = 0f;
        while (e < time)
        {
            e += Time.deltaTime;
            var c = sr.color; c.a = Mathf.Lerp(from, to, e / time); sr.color = c;
            yield return null;
        }
    }
}
