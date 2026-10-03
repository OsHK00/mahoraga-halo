using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

public class HaloAudioPlayer : MonoBehaviour
{
    public const string ClipName = "wheel.mp3";

    public static HaloAudioPlayer Instance { get; private set; }

    public AudioClip Clip { get; private set; }

    private AudioSource _source;
    private UnityWebRequest _request;
    private string _loadPath;
    private bool _warned;

    public static HaloAudioPlayer Ensure()
    {
        if (Instance != null) return Instance;

        GameObject go = new GameObject("mahoraga-halo-audio");
        UnityEngine.Object.DontDestroyOnLoad(go);

        return go.AddComponent<HaloAudioPlayer>();
    }

    private void Awake()
    {
        Instance = this;

        _source = gameObject.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = false;
        _source.spatialBlend = 0f;
        _source.dopplerLevel = 0f;

        _loadPath = ResolvePath();

        if (!string.IsNullOrEmpty(_loadPath)) Log("Step sound file: " + _loadPath);

        TryLoad();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (_request != null)
        {
            _request.Abort();
            _request.Dispose();
            _request = null;
        }

        if (Clip != null)
        {
            UnityEngine.Object.Destroy(Clip);
            Clip = null;
        }
    }

    private string ResolvePath()
    {
        string dir = Path.GetDirectoryName(typeof(HaloAudioPlayer).Assembly.Location);
        if (string.IsNullOrEmpty(dir))
        {
            Log("Could not resolve the mod folder from " + typeof(HaloAudioPlayer).Assembly.Location + ".");
            return null;
        }

        string direct = Path.Combine(dir, ClipName);
        if (File.Exists(direct)) return direct;

        string cached = Path.Combine(Application.temporaryCachePath, "mahoraga-halo_" + ClipName);
        if (File.Exists(cached)) return cached;

        if (TryExtractEmbedded(cached)) return cached;

        Log("Sound file '" + ClipName + "' is neither in " + dir + " nor embedded in the DLL.");
        return null;
    }

    private static bool TryExtractEmbedded(string target)
    {
        try
        {
            Assembly asm = typeof(HaloAudioPlayer).Assembly;
            string[] names = asm.GetManifestResourceNames();

            foreach (string name in names)
            {
                if (!name.EndsWith("." + ClipName, StringComparison.OrdinalIgnoreCase)) continue;

                using (Stream src = asm.GetManifestResourceStream(name))
                {
                    if (src == null) return false;

                    using (FileStream dst = File.Create(target))
                    {
                        src.CopyTo(dst);
                    }
                }

                return true;
            }

            RadianceHalo.Trace("Embedded resources present: " + string.Join(", ", names));
        }
        catch (Exception e)
        {
            RadianceHalo.Trace("Could not extract '" + ClipName + "': " + e.Message);
        }

        return false;
    }

    private void TryLoad()
    {
        _loadPath = ResolvePath() ?? _loadPath;

        if (string.IsNullOrEmpty(_loadPath) || !File.Exists(_loadPath))
        {
            Warn("Sound file '" + ClipName + "' not found, step sound disabled.");
            return;
        }

        StartCoroutine(LoadRoutine(_loadPath));
    }

    private IEnumerator LoadRoutine(string path)
    {
        string url = "file:///" + path.Replace('\\', '/');
        AudioType type = AudioTypeFor(path);

        UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(url, type);
        _request = request;

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Warn("Could not load '" + path + "': " + request.error);
            yield break;
        }

        AudioClip clip = DownloadHandlerAudioClip.GetContent(request);

        if (clip == null)
        {
            Warn("Could not decode '" + path + "'.");
            yield break;
        }

        ReplaceClip(clip);
    }

    private static AudioType AudioTypeFor(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();

        switch (ext)
        {
            case ".mp3": return AudioType.MPEG;
            case ".ogg": return AudioType.OGGVORBIS;
            case ".wav": return AudioType.WAV;
            case ".aif":
            case ".aiff": return AudioType.AIFF;
            default: return AudioType.UNKNOWN;
        }
    }

    private void ReplaceClip(AudioClip clip)
    {
        AudioClip previous = Clip;
        Clip = clip;
        _warned = false;

        if (previous != null) UnityEngine.Object.Destroy(previous);
    }

    public void Play(float volume)
    {
        if (_source == null || Clip == null) return;

        _source.spatialBlend = 0f;
        _source.pitch = 1f;
        _source.PlayOneShot(Clip, Mathf.Clamp01(volume));
    }

    private void Warn(string message)
    {
        if (_warned) return;

        _warned = true;
        Debug.Log("[mahoraga-halo] " + message);
        RadianceHalo.Trace(message);
    }

    private void Log(string message)
    {
        RadianceHalo.Trace(message);
    }
}