using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sahneler arası yaşayan yönetici. mainMenu sahnesinde duruyor.
/// Her chapter'ın kendi sahnesi var (ChapterData.sceneName).
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Tooltip("Sırayla Chapter1, Chapter2, Chapter3")]
    public ChapterData[] chapters;
    public string mainMenuSceneName = "mainMenu";

    public int CurrentChapterIndex { get; private set; }
    public ChapterData CurrentChapter =>
        chapters != null && CurrentChapterIndex < chapters.Length ? chapters[CurrentChapterIndex] : null;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void StartChapter(int index)
    {
        CurrentChapterIndex = Mathf.Clamp(index, 0, chapters.Length - 1);
        string scene = CurrentChapter.sceneName;
        CheckpointState.Clear(); // yeni bölüm baştan başlasın

        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogWarning($"'{scene}' sahnesi yok ya da Build Settings'te değil. Ana menüye dönülüyor.");
            SceneManager.LoadScene(mainMenuSceneName);
            return;
        }
        SceneManager.LoadScene(scene);
    }

    public void NextChapter()
    {
        if (CurrentChapterIndex + 1 < chapters.Length) StartChapter(CurrentChapterIndex + 1);
        else SceneManager.LoadScene(mainMenuSceneName); // oyun bitti
    }
}
