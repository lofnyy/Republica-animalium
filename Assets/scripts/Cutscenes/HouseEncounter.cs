using System.Collections;
using UnityEngine;

/// <summary>
/// Oyuncu bu tetikleyiciye girince oynayan karşılaşma cutscene'i:
///   1) Oyuncu durur, sinema şeritleri gelir
///   2) Karakter (npc) evden çıkıp oyuncunun yanına yürür
///   3) Alttaki konuşma kutusunda metin yazılır, oyuncu devam tuşuyla kapatır
///   4) Şeritler çekilir, kontrol geri gelir
/// Bir kez oynar.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class HouseEncounter : MonoBehaviour
{
    [Header("Referanslar")]
    public PlayerController player;
    public PlayerCombat playerCombat;
    public CinemaBars bars;
    public DialogueBox dialogue;
    public GameObject hudToHide;
    [Tooltip("Oyuncunun yanına gelecek karakter")]
    public Transform npc;

    [Header("Karakterin yürüyüşü")]
    public float npcWalkSpeed = 2.5f;
    [Tooltip("Oyuncuya ne kadar yaklaşıp durur")]
    public float stopDistance = 1.6f;

    [Header("Konuşma")]
    public string speakerKey;
    public string[] lineKeys;

    bool _played;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_played || !other.CompareTag("Player")) return;
        _played = true;
        StartCoroutine(Play());
    }

    IEnumerator Play()
    {
        CutsceneState.Begin();
        if (playerCombat != null) playerCombat.enabled = false;
        if (hudToHide != null) hudToHide.SetActive(false);
        player.ForcedMoveInput = 0f;

        yield return bars.Show(0.5f);

        // Karakter oyuncunun yanına yürür
        if (npc != null)
        {
            float side = Mathf.Sign(npc.position.x - player.transform.position.x);
            if (side == 0f) side = 1f;
            player.FaceDirection((int)side); // oyuncu karaktere dönsün
            var facing = npc.GetComponentInChildren<NpcFacing>();
            if (facing != null) facing.Face(-(int)side); // karakter de oyuncuya dönsün
            float targetX = player.transform.position.x + side * stopDistance;
            while (Mathf.Abs(npc.position.x - targetX) > 0.02f)
            {
                var p = npc.position;
                p.x = Mathf.MoveTowards(p.x, targetX, npcWalkSpeed * Time.deltaTime);
                npc.position = p;
                yield return null;
            }
        }

        yield return new WaitForSeconds(0.3f);
        if (dialogue != null && lineKeys != null && lineKeys.Length > 0)
            yield return dialogue.PlayKeys(speakerKey, lineKeys);

        yield return bars.Hide(0.5f);
        player.ForcedMoveInput = null;
        if (playerCombat != null) playerCombat.enabled = true;
        if (hudToHide != null) hudToHide.SetActive(true);
        CutsceneState.End();
    }
}
