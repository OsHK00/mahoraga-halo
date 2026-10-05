using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Modding;
using Satchel.BetterMenus;
using UnityEngine;

public class RadianceHalo : Mod, ICustomMenuMod, IGlobalSettings<HaloSettings>
{
    public const string BossName    = "Absolute Radiance";
    public const string HaloName    = "Halo";
    public const string SceneName   = "GG_Radiance";
    public const string TextureName = "halo.png";

    public static RadianceHalo Instance { get; private set; }

    public static void Trace(string message)
    {
        if (Instance != null) Instance.Log(message);
        else UnityEngine.Debug.Log("[mahoraga-halo] " + message);
    }

    public Texture2D HaloTexture { get; private set; }

    private static Menu _menu;
    private static bool _built;
    private static readonly Dictionary<string, Element> Rows = new Dictionary<string, Element>();

    private static readonly string[] PhaseRows = { "optPlatformScale" };
    private static readonly string[] SoundRows = { "optSoundVolume" };

    public const string ModName = "mahoraga-halo";

    public RadianceHalo() : base(ModName)
    {
        HaloConfig.SaveHook = OnSaveGlobal;
    }

    public override string GetVersion()
    {
        return FileVersionInfo
            .GetVersionInfo(Assembly.GetAssembly(typeof(RadianceHalo)).Location)
            .FileVersion;
    }

    public override void Initialize()
    {
        Instance = this;

        HaloTexture = LoadPng(TextureName);
        if (HaloTexture == null)
        {
            Log("Could not load " + TextureName + ", mod stays inactive.");
            return;
        }

        GameObject watchdog = new GameObject("mahoraga-halo-watchdog");
        UnityEngine.Object.DontDestroyOnLoad(watchdog);
        watchdog.AddComponent<HaloWatchdog>();

        HaloAudioPlayer.Ensure();
    }

    public override string GetMenuButtonText()
    {
        return ModName;
    }

    public void OnLoadGlobal(HaloSettings settings)
    {
        HaloConfig.Load(settings);
    }

    public HaloSettings OnSaveGlobal()
    {
        return HaloConfig.Current;
    }

    public bool ToggleButtonInsideMenu => false;

    public MenuScreen GetMenuScreen(MenuScreen modListMenu, ModToggleDelegates? toggleDelegates)
    {
        try
        {
            if (_menu == null) _menu = BuildMenu();

            MenuScreen screen = _menu.GetMenuScreen(modListMenu);
            _built = true;

            ApplyVisibility();

            return screen;
        }
        catch (Exception e)
        {
            Log("Could not build the options menu: " + e);
            return modListMenu;
        }
    }

    private static Menu BuildMenu()
    {
        Rows.Clear();

        var elements = new List<Element>
        {
            Row("optEnabled", new HorizontalOption(
                "Enabled",
                "Halo sprite and animation",
                new[] { "OFF", "ON" },
                i => Set(v => v.Enabled = i == 1),
                () => HaloConfig.Current.Enabled ? 1 : 0)),

            Row("optHaloMode", new HorizontalOption(
                "Halo Mode",
                "Shows both halos, only the custom one, or only the original",
                new[] { "BOTH", "CUSTOM", "ORIGINAL" },
                i => Set(v => v.Mode = i == 0 ? HaloMode.Both : i == 1 ? HaloMode.CustomOnly : HaloMode.OriginalOnly),
                () => (int)HaloConfig.Current.Mode)),

            Row("optOriginalAlpha", new CustomSlider(
                "Original Opacity",
                v => Set(x => x.OriginalAlpha = v),
                () => HaloConfig.Current.OriginalAlpha,
                0f, 1f, false, "optOriginalAlpha")),

            Row("optDirection", new HorizontalOption(
                "Direction",
                "Flips the direction of the steps",
                new[] { "NORMAL", "REVERSED" },
                i => Set(v => v.Reverse = i == 1),
                () => HaloConfig.Current.Reverse ? 1 : 0)),

            Row("optStepSize", new CustomSlider(
                "Step Size",
                v => Set(x => x.StepDegrees = v),
                () => HaloConfig.Current.StepDegrees,
                15f, 180f, true, "optStepSize")),

            Row("optStepTime", new CustomSlider(
                "Step Time",
                v => Set(x => x.StepTime = v),
                () => HaloConfig.Current.StepTime,
                0.2f, 4f, false, "optStepTime")),

            Row("optStepRest", new CustomSlider(
                "Step Rest",
                v => Set(x => x.StepRest = v),
                () => HaloConfig.Current.StepRest,
                0f, 4f, false, "optStepRest")),

            Row("optAlpha", new CustomSlider(
                "Alpha",
                v => Set(x => x.Alpha = v),
                () => HaloConfig.Current.Alpha,
                0.05f, 1f, false, "optAlpha")),

            Row("optScale", new CustomSlider(
                "Scale",
                v => Set(x => x.Scale = v),
                () => HaloConfig.Current.Scale,
                0.5f, 3f, false, "optScale")),

            Row("optPhaseAware", new HorizontalOption(
                "Phase Aware",
                "Grows the halo during the platform phase",
                new[] { "OFF", "ON" },
                i => Set(v => v.PhaseAware = i == 1),
                () => HaloConfig.Current.PhaseAware ? 1 : 0)),

            Row("optPlatformScale", new CustomSlider(
                "Platform Scale",
                v => Set(x => x.PlatformScale = v),
                () => HaloConfig.Current.PlatformScale,
                1f, 4f, false, "optPlatformScale")),

            Row("optStepSound", new HorizontalOption(
                "Step Sound",
                "Plays a sound every time a step lands",
                new[] { "OFF", "ON" },
                i => Set(v => v.StepSound = i == 1),
                () => HaloConfig.Current.StepSound ? 1 : 0)),

            Row("optSoundVolume", new CustomSlider(
                "Sound Volume",
                v => Set(x => x.StepSoundVolume = v),
                () => HaloConfig.Current.StepSoundVolume,
                0f, 1f, false, "optSoundVolume")),

            Row("optReset", new MenuButton(
                "Reset to defaults",
                "Restores every option to its default value",
                _ =>
                {
                    HaloConfig.Reset();
                    ApplyVisibility();
                },
                false)),
        };

        return new Menu(ModName, elements.ToArray());
    }

    private static Element Row(string id, Element element)
    {
        MenuRow row = new MenuRow(new List<Element> { element }, id);
        Rows[id] = row;
        return row;
    }

    private static void SetVisible(string[] ids, bool visible)
    {
        for (int i = 0; i < ids.Length; i++)
        {
            if (!Rows.TryGetValue(ids[i], out Element element)) continue;
            element.isVisible = visible;
        }
    }

    private static void ApplyVisibility()
    {
        if (!_built || _menu == null) return;

        HaloSettings s = HaloConfig.Current;

        SetVisible(PhaseRows, s.PhaseAware);
        SetVisible(SoundRows, s.StepSound);

        _menu.Update();
    }

    private static void Set(Action<HaloSettings> apply)
    {
        apply(HaloConfig.Current);
        ApplyVisibility();
        HaloConfig.NotifyChanged();
    }

    public void Unload()
    {
        foreach (HaloController ctrl in new List<HaloController>(HaloController.Active))
        {
            if (ctrl == null) continue;

            GameObject clone = ctrl.CustomHaloObject;

            ctrl.Restore();
            UnityEngine.Object.Destroy(ctrl);

            if (clone != null) UnityEngine.Object.Destroy(clone);
        }
        HaloController.Active.Clear();
    }

    private Texture2D LoadPng(string fileName)
    {
        Texture2D tex = LoadFromDisk(fileName);
        if (tex != null) return tex;

        try
        {
            Assembly asm    = Assembly.GetExecutingAssembly();
            string[] names = asm.GetManifestResourceNames();
            string target   = null;

            foreach (string name in names)
            {
                if (name.EndsWith("." + fileName)) { target = name; break; }
            }

            if (target == null)
            {
                Log("LoadPng: resource '" + fileName + "' not found. Available: "
                    + string.Join(", ", names));
                return null;
            }

            using Stream stream = asm.GetManifestResourceStream(target);
            if (stream == null) return null;

            MemoryStream ms = new MemoryStream((int)stream.Length);
            stream.CopyTo(ms);

            Texture2D embedded = BuildTexture(ms.ToArray());
            Log("Halo texture loaded from the DLL: " + embedded.width + "x" + embedded.height);
            return embedded;
        }
        catch (Exception e)
        {
            Log("Error loading embedded '" + fileName + "': " + e.Message);
            return null;
        }
    }

    public static string TexturePath(string fileName)
    {
        string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(dir)) return null;
        return Path.Combine(dir, fileName);
    }

    private Texture2D LoadFromDisk(string fileName)
    {
        string path = TexturePath(fileName);
        if (path == null || !File.Exists(path)) return null;

        try
        {
            Texture2D tex = BuildTexture(File.ReadAllBytes(path));
            Log("Halo texture loaded from " + path + ": " + tex.width + "x" + tex.height);
            return tex;
        }
        catch (Exception e)
        {
            Log("Error loading '" + path + "': " + e.Message);
            return null;
        }
    }

    private static Texture2D BuildTexture(byte[] bytes)
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.LoadImage(bytes, markNonReadable: true);
        return tex;
    }
}

internal class HaloWatchdog : MonoBehaviour
{
    private float _timer;

    private void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = 0.25f;

        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != RadianceHalo.SceneName) return;

        GameObject boss = GameObject.Find(RadianceHalo.BossName);
        if (boss == null) return;

        Transform halo = boss.transform.Find(RadianceHalo.HaloName);
        if (halo == null) return;

        HaloController.Ensure(halo.gameObject);
    }
}