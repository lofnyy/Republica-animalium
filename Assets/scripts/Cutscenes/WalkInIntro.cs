using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Bölüm girişi: sinema şeritleri gelir, karakter kendi kendine belirlenen noktaya yürür,
/// sonra kontrol oyuncuya geçer. Esc ile atlanabilir. Ölünce yeniden başlarsa oynamaz.
/// </summary>
public class WalkInIntro : MonoBehaviour
{
    [Header("Referanslar")]
    public PlayerController player;
    public PlayerCombat playerCombat;
    public CinemaBars bars;
    [Tooltip("Cutscene sırasında gizlenecek arayüz (ör. can yazısı)")]
    public GameObject hudToHide;

    [Header("Yürüyüş")]
    [Tooltip("Karakter bu x'e kadar kendi yürür")]
    public float walkToX = 30f;
    [Tooltip("Yürüyüş en fazla bu kadar sürer (takılırsa diye)")]
    public float maxWalkTime = 10f;
    public float barsTime = 0.5f;
    [Tooltip("Yürüdükten sonra kontrol verilmeden önce bekleme")]
    public float pauseAfterWalk = 0.6f;

    bool _skip;

    void Start()
    {
        if (CutsceneState.SkipIntroOnce)
        {
            CutsceneState.SkipIntroOnce = false;
            return;
        }
        StartCoroutine(Play());
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) _skip = true;
    }

    IEnumerator Play()
    {
        CutsceneState.Begin();
        if (playerCombat != null) playerCombat.enabled = false;
        if (hudToHide != null) hudToHide.SetActive(false);
        player.ForcedMoveInput = 0f;

        yield return bars.Show(barsTime);

        player.ForcedMoveInput = 1f;
        float t = 0f;
        while (player.transform.position.x < walkToX && t < maxWalkTime && !_skip)
        {
            t += Time.deltaTime;
            yield return null;
        }
        player.ForcedMoveInput = 0f;

        if (!_skip) yield return new WaitForSeconds(pauseAfterWalk);
        if (_skip) bars.Set(0f); else yield return bars.Hide(barsTime);

        player.ForcedMoveInput = null;
        if (playerCombat != null) playerCombat.enabled = true;
        if (hudToHide != null) hudToHide.SetActive(true);
        CutsceneState.End();
    }
}
