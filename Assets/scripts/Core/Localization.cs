using System;
using System.Collections.Generic;
using UnityEngine;

public enum Language
{
    Turkish = 0,
    English = 1,
}

/// <summary>
/// Dil sistemi (Türkçe / English).
///   Metinler Resources/Localization/strings.json dosyasında, kimlik (id) → {tr, en} olarak tutulur.
///   Seçilen dil PlayerPrefs'e kaydedilir (ses ayarıyla aynı yöntem) ve oyun yeniden açılınca korunur.
///   Dil seçilmemişse Türkçe açılır.
///   Çeviri eksikse önce diğer dile bakılır, o da yoksa "[id]" gösterilir ve konsola bir kez uyarı yazılır.
///
/// Kullanım:
///   Localization.Get("act1.intro.badger_01")
///   Localization.Get("perk.damage.gain", 19, 24)      // {0}, {1} yerine değer koyar
///   Localization.SetLanguage(Language.English)        // her yerde anında güncellenir
///   Localization.LanguageChanged += Yenile;           // arayüz kendini yenilemek için
/// </summary>
public static class Localization
{
    const string PrefKey = "language";
    const string ResourcePath = "Localization/strings";

    [Serializable] public class Entry { public string id; public string tr; public string en; public string note; }
    [Serializable] class Table { public Entry[] entries; }

    static Dictionary<string, Entry> _map;
    static Language _lang = Language.Turkish;
    static bool _langLoaded;
    static readonly HashSet<string> _warned = new HashSet<string>();

    /// <summary>Dil değişince tetiklenir.</summary>
    public static event Action LanguageChanged;

    public static Language Current
    {
        get { EnsureLanguage(); return _lang; }
    }

    /// <summary>"tr" ya da "en".</summary>
    public static string Code => Current == Language.English ? "en" : "tr";

    /// <summary>Dili değiştirir, kaydeder ve herkese haber verir.</summary>
    public static void SetLanguage(Language language)
    {
        EnsureLanguage();
        PlayerPrefs.SetString(PrefKey, language == Language.English ? "en" : "tr");
        PlayerPrefs.Save();
        if (_lang == language) return;
        _lang = language;
        LanguageChanged?.Invoke();
    }

    /// <summary>Türkçe ↔ English arasında geçer.</summary>
    public static void Toggle() => SetLanguage(Current == Language.Turkish ? Language.English : Language.Turkish);

    /// <summary>Dilin kendi adı ("Türkçe" / "English"); hiçbir zaman çevrilmez.</summary>
    public static string NameOf(Language language) => Get(language == Language.English ? "lang.en" : "lang.tr");

    public static bool Has(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        EnsureTable();
        return _map.ContainsKey(id);
    }

    public static string Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return string.Empty;
        EnsureTable();
        if (!_map.TryGetValue(id, out var e)) return Missing(id, "id yok");

        bool en = Current == Language.English;
        string text = en ? e.en : e.tr;
        if (!string.IsNullOrEmpty(text)) return text;

        // seçili dilde çeviri eksik: diğer dile düş
        string other = en ? e.tr : e.en;
        Warn(id, en ? "İngilizce çeviri eksik, Türkçe gösteriliyor" : "Türkçe metin eksik, İngilizce gösteriliyor");
        return string.IsNullOrEmpty(other) ? "[" + id + "]" : other;
    }

    public static string Get(string id, params object[] args)
    {
        string text = Get(id);
        if (args == null || args.Length == 0) return text;
        try { return string.Format(text, args); }
        catch (FormatException) { Warn(id, "biçim hatası ({0} sayısı uyuşmuyor)"); return text; }
    }

    // ------------------------------------------------------------------ iç işler

    static void EnsureLanguage()
    {
        if (_langLoaded) return;
        _langLoaded = true;
        string saved = PlayerPrefs.GetString(PrefKey, "tr");
        _lang = saved == "en" ? Language.English : Language.Turkish;
    }

    static void EnsureTable()
    {
        if (_map != null) return;
        _map = new Dictionary<string, Entry>(256);
        var asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null)
        {
            Debug.LogError("Localization: Resources/" + ResourcePath + ".json bulunamadı.");
            return;
        }
        Table table = null;
        try { table = JsonUtility.FromJson<Table>(asset.text); }
        catch (Exception ex) { Debug.LogError("Localization: strings.json okunamadı: " + ex.Message); }
        if (table == null || table.entries == null) return;
        foreach (var e in table.entries)
        {
            if (e == null || string.IsNullOrEmpty(e.id)) continue;
            if (_map.ContainsKey(e.id)) { Debug.LogWarning("Localization: yinelenen id '" + e.id + "'"); continue; }
            _map[e.id] = e;
        }
    }

    static string Missing(string id, string why)
    {
        Warn(id, why);
        return "[" + id + "]";
    }

    static void Warn(string id, string why)
    {
        if (_warned.Add(id + why)) Debug.LogWarning("Localization: '" + id + "' — " + why);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _map = null;
        _langLoaded = false;
        _warned.Clear();
        LanguageChanged = null;
    }
}
