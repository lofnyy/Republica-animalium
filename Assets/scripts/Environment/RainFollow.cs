using UnityEngine;

/// <summary>
/// Yağmur yayıcısını yatayda kamerayla birlikte taşır; yükseklik sabit kalır (bulutların altı).
/// Damlalar dünya uzayında düştüğü için kamera hareket edince yağmur kaymaz.
/// </summary>
public class RainFollow : MonoBehaviour
{
    public Transform cameraTransform;
    [Tooltip("Yağmurun başladığı yükseklik")]
    public float height = 42f;
    [Tooltip("Rüzgar yüzünden damlalar sola kaydığı için yayıcıyı biraz sağa al")]
    public float xOffset = 4f;

    void Start()
    {
        var ps = GetComponent<ParticleSystem>();
        if (ps != null && !ps.isPlaying) ps.Play();
    }

    void LateUpdate()
    {
        if (cameraTransform == null) return;
        transform.position = new Vector3(cameraTransform.position.x + xOffset, height, 0f);
    }
}
