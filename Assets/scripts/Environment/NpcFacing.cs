using UnityEngine;

/// <summary>
/// Sola ve sağa bakan iki ayrı çizimi olan karakterler için yön değiştirme.
/// Face(-1) sola, Face(1) sağa baktırır.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class NpcFacing : MonoBehaviour
{
    public Sprite facingLeft;
    public Sprite facingRight;
    [Tooltip("Başlangıçta hangi yöne baksın (-1 sol, 1 sağ)")]
    public int startDirection = -1;

    SpriteRenderer _sr;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        Face(startDirection);
    }

    public void Face(int dir)
    {
        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        var s = dir < 0 ? facingLeft : facingRight;
        if (s != null) _sr.sprite = s;
    }
}
