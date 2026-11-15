using UnityEngine;

/// <summary>
/// Bir chapter'ın bilgileri. Assets/Data/Chapters altında her chapter için bir tane var.
/// Sağ tık > Create > Game > Chapter Data
/// </summary>
[CreateAssetMenu(fileName = "Chapter", menuName = "Game/Chapter Data")]
public class ChapterData : ScriptableObject
{
    public int chapterNumber = 1;
    public string chapterName = "Chapter 1";

    [Min(1f)]
    public float enemyHealthMultiplier = 1f;

    [Tooltip("Bu chapter'ın sahnesi (Build Settings'e eklenmiş olmalı)")]
    public string sceneName = "LEVEL1";
}
