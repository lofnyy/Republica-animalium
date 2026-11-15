using UnityEngine;

/// <summary>
/// Oyuncunun üstünde durur: bu sahnede kayıt noktası varsa oyuncuyu oraya koyar.
/// Awake'te çalışır ki kamera ilk karede doğru yerde olsun.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class CheckpointSpawner : MonoBehaviour
{
    void Awake()
    {
        if (CheckpointState.TryGet(out var pos))
            GetComponent<PlayerController>().Teleport(pos);
    }
}
