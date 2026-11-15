using System.Collections;
using UnityEngine;

/// <summary>
/// Ekranın üstünden ve altından gelen siyah sinema şeritleri.
/// </summary>
public class CinemaBars : MonoBehaviour
{
    public RectTransform topBar;
    public RectTransform bottomBar;
    public float barHeight = 110f;

    void Awake() => Set(0f);

    public IEnumerator Show(float duration) => Animate(0f, 1f, duration);
    public IEnumerator Hide(float duration) => Animate(1f, 0f, duration);

    IEnumerator Animate(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            Set(Mathf.Lerp(from, to, k * k * (3f - 2f * k)));
            yield return null;
        }
        Set(to);
    }

    public void Set(float k)
    {
        float h = barHeight * k;
        if (topBar != null) topBar.sizeDelta = new Vector2(topBar.sizeDelta.x, h);
        if (bottomBar != null) bottomBar.sizeDelta = new Vector2(bottomBar.sizeDelta.x, h);
    }
}
