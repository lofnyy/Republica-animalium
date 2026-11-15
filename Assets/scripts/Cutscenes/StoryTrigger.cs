using UnityEngine;

/// <summary>Hikâye yöneticisinin (MineStory, VillageStory…) uyguladığı arayüz.</summary>
public interface IStoryTriggerTarget
{
    void Trigger(int id);
}

/// <summary>Oyuncu girince hikâye yöneticisine haber veren tek seferlik tetikleyici.</summary>
[RequireComponent(typeof(Collider2D))]
public class StoryTrigger : MonoBehaviour
{
    [Tooltip("IStoryTriggerTarget uygulayan bileşen (MineStory, VillageStory…)")]
    public MonoBehaviour story;
    public int id = 1;

    bool _used;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_used || !other.CompareTag("Player")) return;
        if (story is IStoryTriggerTarget target)
        {
            _used = true;
            target.Trigger(id);
        }
    }
}
