using TMPro;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Ana menü: Oyunu başlatır, ayarları açar veya oyundan çıkar.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Serializable]
    public class LocalizedLabel
    {
        public TMP_Text text;
        public string key;
    }

    [Header("Paneller")]
    public GameObject mainPanel;
    public GameObject settingsPanel;

    [Header("Ayarlar")]
    public Slider volumeSlider;
    public TMP_Text volumeValueText;
    public LocalizedLabel[] localizedLabels;

    [Tooltip("GameManager yoksa yüklenecek sahne")]
    public string firstLevelScene = "LEVEL1";

    void Start()
    {
        volumeSlider.minValue = 0f;
        volumeSlider.maxValue = 1f;
        volumeSlider.SetValueWithoutNotify(AudioSettingsManager.Volume);
        volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        UpdateVolumeText(volumeSlider.value);

        ShowMain();
        RefreshLocalizedTexts();
    }

    void OnEnable() => Localization.LanguageChanged += RefreshLocalizedTexts;

    void OnDisable() => Localization.LanguageChanged -= RefreshLocalizedTexts;

    public void Play()
    {
        if (GameManager.Instance != null) GameManager.Instance.StartChapter(0);
        else SceneManager.LoadScene(firstLevelScene);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OpenSettings()
    {
        mainPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void ShowMain()
    {
        settingsPanel.SetActive(false);
        mainPanel.SetActive(true);
    }

    void OnVolumeChanged(float value)
    {
        AudioSettingsManager.Volume = value;
        UpdateVolumeText(value);
    }

    void UpdateVolumeText(float value)
    {
        if (volumeValueText != null)
            volumeValueText.text = Localization.Get("settings.volume_pct", Mathf.RoundToInt(value * 100));
    }

    void RefreshLocalizedTexts()
    {
        if (localizedLabels != null)
        {
            foreach (var label in localizedLabels)
                if (label != null && label.text != null && !string.IsNullOrEmpty(label.key))
                    label.text.text = Localization.Get(label.key);
        }

        if (volumeSlider != null) UpdateVolumeText(volumeSlider.value);
    }
}
