using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Modding;
using Satchel.BetterMenus;
using UnityEngine;

public class RadianceHalo : Mod, ICustomMenuMod, ITogglableMod, IGlobalSettings<HaloSettings>
{
    public const string BossName    = "Absolute Radiance";
    public const string HaloName    = "Halo";
    public const string SceneName   = "GG_Radiance";
    public const string TextureName = "halo.png";

    public static RadianceHalo Instance { get; private set; }

    public Texture2D HaloTexture { get; private set; }

    private DateTime _textureStamp = DateTime.MinValue;

    private static Menu _menu;
    private static bool _built;
    private static readonly Dictionary<string, Element> Rows = new Dictionary<string, Element>();

    private static readonly string[] SpeedRows   = { "optSpeed", "optAccel" };
    private static readonly string[] SteppedRows = { "optStepSize", "optStepTime", "optStepRest" };

    public RadianceHalo() : base("Radiance Halo")
    {
        HaloConfig.SaveHook = SaveGlobalSettings;
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
        Log("Halo texture loaded: " + HaloTexture.width + "x" + HaloTexture.height);

        GameObject watchdog = new GameObject("RadianceHaloWatchdog");
        UnityEngine.Object.DontDestroyOnLoad(watchdog);
        watchdog.AddComponent<HaloWatchdog>();
    }

    public override string GetMenuButtonText()
    {
        return "Radiance Halo";
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
                "Halo sprite and animation on or off",
                new[] { "OFF", "ON" },
                i => Set(v => v.Enabled = i == 1),
                () => HaloConfig.Current.Enabled ? 1 : 0)),

            Row("optMode", new HorizontalOption(
                "Mode",
                "How the halo moves",
                new[] { "Normal", "Stepped" },
                i => Set(v => v.Mode = (HaloMode)i),
                () => (int)HaloConfig.Current.Mode)),

            Row("optSpeed", new CustomSlider(
                "Speed",
                v => Set(x => x.Speed = v),
                () => HaloConfig.Current.Speed,
                0f, 90f, false)),

            Row("optDirection", new HorizontalOption(
                "Direction",
                "Flips the spin in any mode",
                new[] { "NORMAL", "REVERSED" },
                i => Set(v => v.Reverse = i == 1),
                () => HaloConfig.Current.Reverse ? 1 : 0)),

            Row("optAccel", new CustomSlider(
                "Acceleration",
                v => Set(x => x.Acceleration = v),
                () => HaloConfig.Current.Acceleration,
                0f, 360f, false)),

            Row("optStepSize", new CustomSlider(
                "Step Size",
                v => Set(x => x.StepDegrees = v),
                () => HaloConfig.Current.StepDegrees,
                15f, 180f, true)),

Row("optStepTime", new CustomSlider(
                "Step Time",
                v => Set(x => x.StepTime = v),
                () => HaloConfig.Current.StepTime,
                0.2f, 4f, false)),

            Row("optStepRest", new CustomSlider(
                "Step Rest",
                v => Set(x => x.StepRest = v),
                () => HaloConfig.Current.StepRest,
                0f, 4f, false)),

            Row("optAlpha", new CustomSlider(
                "Alpha",
                v => Set(x => x.Alpha = v),
                () => HaloConfig.Current.Alpha,
                0.05f, 1f, false)),

            Row("optScale", new CustomSlider(
                "Scale",
                v => Set(x => x.Scale = v),
                () => HaloConfig.Current.Scale,
                0.5f, 3f, false)),

            Row("optLiveReload", new HorizontalOption(
                "Live Reload",
                "Watch halo.png and reload it when it changes, for texture tests",
                new[] { "OFF", "ON" },
                i => Set(v => v.LiveReload = i == 1),
                () => HaloConfig.Current.LiveReload ? 1 : 0)),

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

        return new Menu("Radiance Halo", elements.ToArray());
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
        HaloMode mode  = s.Mode;

SetVisible(SpeedRows,   s.UsesSpeed(mode));
        SetVisible(SteppedRows, mode == HaloMode.Stepped);

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
            ctrl.Restore();
            UnityEngine.Object.Destroy(ctrl);
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

            return BuildTexture(ms.ToArray());
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
            _textureStamp = File.GetLastWriteTimeUtc(path);
            Log("Halo texture loaded from " + path + ": " + tex.width + "x" + tex.height);
            return tex;
        }
        catch (Exception e)
        {
            Log("Error loading '" + path + "': " + e.Message);
            return null;
        }
    }

    public void ReloadTextureIfChanged()
    {
        if (HaloTexture == null) return;

        string path = TexturePath(TextureName);
        if (path == null || !File.Exists(path)) return;

        DateTime stamp = File.GetLastWriteTimeUtc(path);
        if (stamp == _textureStamp) return;

        Texture2D next;
        try
        {
            next = BuildTexture(File.ReadAllBytes(path));
        }
        catch (Exception e)
        {
            Log("Error reloading '" + path + "': " + e.Message);
            return;
        }

        Texture2D previous = HaloTexture;
        HaloTexture    = next;
        _textureStamp  = stamp;

        if (previous != null) UnityEngine.Object.Destroy(previous);

        Log("Halo texture reloaded: " + next.width + "x" + next.height);
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

if (halo.GetComponent<HaloController>() == null)
        {
            RadianceHalo.Instance?.Log("HaloController attached.");
        }

        HaloController.Attach(halo.gameObject);
    }

    private float _reloadTimer;

    private void LateUpdate()
    {
        if (!HaloConfig.Current.LiveReload) return;

        _reloadTimer -= Time.deltaTime;
        if (_reloadTimer > 0f) return;
        _reloadTimer = 1f;

        RadianceHalo.Instance?.ReloadTextureIfChanged();
    }
}