using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Yere düşen katlanmış parşömen.
/// Etkinleşince hafifçe zıplayıp yere düşer, sonra yerinde süzülür.
/// Oyuncu yanındayken "[E] Oku" çıkar; E (ya da gamepad Y) ile ScrollReader'da açılır.
/// İlk okunuşta activateOnRead'deki objeler açılır (ör. bölüm çıkışı).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ScrollPickup : MonoBehaviour
{
    [Header("Referanslar")]
    public ScrollReader reader;
    [Tooltip("Üstte çıkan '[E] Oku' yazısı")]
    public GameObject prompt;
    [Tooltip("Süzülen görsel")]
    public Transform visual;

    [Header("Yazı")]
    public string titleKey = "item.letter.title";
    public string bodyKey = "item.letter.body";
    public string title = "";
    [TextArea(5, 15)]
    public string body = "";

    [Header("Okununca")]
    public GameObject[] activateOnRead;

    [Header("Düşme")]
    public LayerMask groundMask;
    public float gravity = 25f;
    public float popVelocity = 6f;
    [Tooltip("Yere değdiğinde görselin yerden yüksekliği")]
    public float restHeight = 0.35f;

    bool _near;
    bool _falling;
    bool _read;
    float _vy;
    float _t;

    void Awake()
    {
        LocalizedText.Bind(prompt, "prompt.read");
    }

    void OnEnable()
    {
        _falling = true;
        _vy = popVelocity;
        if (prompt != null) prompt.SetActive(false);
    }

    void Update()
    {
        _t += Time.deltaTime;

        if (_falling) Fall();
        else if (visual != null)
            visual.localPosition = new Vector3(0f, restHeight + Mathf.Sin(_t * 2.5f) * 0.08f, 0f);

        bool canRead = _near && !_falling && reader != null && !reader.IsOpen;
        if (prompt != null && prompt.activeSelf != canRead) prompt.SetActive(canRead);
        if (!canRead || CutsceneState.IsActive || Time.frameCount == reader.LastClosedFrame) return;

        var kb = Keyboard.current;
        var gp = Gamepad.current;
        if ((kb != null && kb.eKey.wasPressedThisFrame) || (gp != null && gp.buttonNorth.wasPressedThisFrame))
            Read();
    }

    void Fall()
    {
        _vy -= gravity * Time.deltaTime;
        float step = _vy * Time.deltaTime;
        var pos = transform.position;
        if (_vy < 0f)
        {
            var hit = Physics2D.Raycast(pos, Vector2.down, -step + 0.05f, groundMask);
            if (hit.collider != null)
            {
                transform.position = new Vector3(pos.x, hit.point.y, pos.z);
                _falling = false;
                _t = 0f;
                return;
            }
        }
        transform.position = pos + Vector3.up * step;
        if (visual != null)
        {
            visual.localPosition = new Vector3(0f, restHeight, 0f);
            visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_t * 12f) * 15f);
        }
    }

    /// <summary>Parşömeni aç (E tuşu bunu çağırır).</summary>
    public void Read()
    {
        if (reader == null || reader.IsOpen) return;
        if (visual != null) visual.localRotation = Quaternion.identity;
        reader.Open(
            string.IsNullOrEmpty(titleKey) ? title : titleKey,
            string.IsNullOrEmpty(bodyKey) ? body : bodyKey,
            OnClosed);
    }

    void OnClosed()
    {
        if (_read) return;
        _read = true;
        if (activateOnRead != null)
            foreach (var go in activateOnRead) if (go != null) go.SetActive(true);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player")) _near = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player")) _near = false;
    }
}
