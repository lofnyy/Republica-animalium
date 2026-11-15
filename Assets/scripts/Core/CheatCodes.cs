using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gizli kodlar. Oyun açılınca kendiliğinden oluşur, sahneler arası yaşar.
/// Konami kodu (↑ ↑ ↓ ↓ ← → ← → B A) ya da "losangles" yazmak:
/// oyuncunun vuruşları düşmanları tek vuruşta öldürür.
/// Tekrar girilirse kapanır.
/// </summary>
public class CheatCodes : MonoBehaviour
{
    /// <summary>Açıksa oyuncunun her vuruşu öldürür.</summary>
    public static bool OneHitKill { get; private set; }

    // Hepsi tek vuruş modunu açıp kapatır
    static readonly Key[][] Codes =
    {
        // Konami: ↑ ↑ ↓ ↓ ← → ← → B A
        new[]
        {
            Key.UpArrow, Key.UpArrow, Key.DownArrow, Key.DownArrow,
            Key.LeftArrow, Key.RightArrow, Key.LeftArrow, Key.RightArrow,
            Key.B, Key.A
        },
        // losangles
        new[] { Key.L, Key.O, Key.S, Key.A, Key.N, Key.G, Key.L, Key.E, Key.S },
    };

    readonly int[] _progress = new int[Codes.Length];
    string _message;
    float _messageTimer;
    GUIStyle _style;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        OneHitKill = false;
        var go = new GameObject("CheatCodes");
        go.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(go);
        go.AddComponent<CheatCodes>();
    }

    void Update()
    {
        if (_messageTimer > 0f) _messageTimer -= Time.unscaledDeltaTime;

        var kb = Keyboard.current;
        if (kb == null || !kb.anyKey.wasPressedThisFrame) return;

        for (int c = 0; c < Codes.Length; c++)
        {
            var code = Codes[c];
            if (kb[code[_progress[c]]].wasPressedThisFrame)
            {
                _progress[c]++;
                if (_progress[c] == code.Length)
                {
                    System.Array.Clear(_progress, 0, _progress.Length);
                    OneHitKill = !OneHitKill;
                    Show(Localization.Get(OneHitKill ? "cheat.onehit_on" : "cheat.onehit_off"));
                    return;
                }
            }
            else
            {
                // Yanlış tuş: baştan başla (ama bu tuş kodun ilk tuşuysa onu say)
                _progress[c] = kb[code[0]].wasPressedThisFrame ? 1 : 0;
            }
        }
    }

    void Show(string text)
    {
        _message = text;
        _messageTimer = 2.5f;
    }

    void OnGUI()
    {
        if (_messageTimer <= 0f || string.IsNullOrEmpty(_message)) return;
        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
        }
        _style.fontSize = Mathf.RoundToInt(Screen.height * 0.05f);
        float a = Mathf.Clamp01(_messageTimer / 0.5f);
        var rect = new Rect(0f, Screen.height * 0.18f, Screen.width, Screen.height * 0.1f);

        _style.normal.textColor = new Color(0f, 0f, 0f, a * 0.8f);
        GUI.Label(new Rect(rect.x + 3f, rect.y + 3f, rect.width, rect.height), _message, _style);
        _style.normal.textColor = new Color(1f, 0.85f, 0.3f, a);
        GUI.Label(rect, _message, _style);
    }
}
