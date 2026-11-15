using UnityEngine;

/// <summary>
/// Bulutu yavaşça yatayda kaydırır; sağ sınırı geçince sol sınırdan tekrar girer.
/// </summary>
public class CloudDrift : MonoBehaviour
{
    [Tooltip("Birim/saniye (eksi = sola)")]
    public float speed = 0.3f;
    public float minX = -40f;
    public float maxX = 200f;

    void Update()
    {
        var p = transform.position;
        p.x += speed * Time.deltaTime;
        if (speed > 0f && p.x > maxX) p.x = minX;
        else if (speed < 0f && p.x < minX) p.x = maxX;
        transform.position = p;
    }
}
