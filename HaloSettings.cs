using System;
using System.Collections.Generic;
using UnityEngine;

public enum HaloMode
{
    Both = 0,
    CustomOnly = 1,
    OriginalOnly = 2,
}

public class HaloSettings
{
    public bool Enabled = true;

    public HaloMode Mode = HaloMode.Both;

    public bool Reverse = false;

    public float StepDegrees = 45f;
    public float StepTime = 0.246941909f;
    public float StepRest = 2.979122f;

    public float Alpha = 0.9f;
    public float Scale = 1f;

    public float OriginalAlpha = 1f;

    public bool PhaseAware = true;
    public float PlatformScale = 1.10855341f;

    public bool StepSound = true;
    public float StepSoundVolume = 0.6f;

    public HaloSettings Clone()
    {
        return (HaloSettings)MemberwiseClone();
    }

    public bool AlmostEquals(HaloSettings other)
    {
        if (other == null) return false;

        const float eps = 0.00001f;

        return Enabled == other.Enabled
            && Mode == other.Mode
            && Reverse == other.Reverse
            && Near(StepDegrees, other.StepDegrees, eps)
            && Near(StepTime, other.StepTime, eps)
            && Near(StepRest, other.StepRest, eps)
            && Near(Alpha, other.Alpha, eps)
            && Near(Scale, other.Scale, eps)
            && Near(OriginalAlpha, other.OriginalAlpha, eps)
            && PhaseAware == other.PhaseAware
            && Near(PlatformScale, other.PlatformScale, eps)
            && StepSound == other.StepSound
            && Near(StepSoundVolume, other.StepSoundVolume, eps);
    }

    private static bool Near(float a, float b, float eps)
    {
        return Mathf.Abs(a - b) <= eps;
    }

    public string Diff(HaloSettings o)
    {
        if (o == null) return null;

        const float eps = 0.00001f;

        List<string> parts = new List<string>();

        if (Enabled != o.Enabled) parts.Add("Enabled " + o.Enabled + " -> " + Enabled);
        if (Mode != o.Mode) parts.Add("Mode " + o.Mode + " -> " + Mode);
        if (Reverse != o.Reverse) parts.Add("Reverse " + o.Reverse + " -> " + Reverse);

        if (!Near(StepDegrees, o.StepDegrees, eps)) parts.Add("StepDegrees: " + F(o.StepDegrees) + " -> " + F(StepDegrees));
        if (!Near(StepTime, o.StepTime, eps)) parts.Add("StepTime: " + F(o.StepTime) + " -> " + F(StepTime));
        if (!Near(StepRest, o.StepRest, eps)) parts.Add("StepRest: " + F(o.StepRest) + " -> " + F(StepRest));

        if (!Near(Alpha, o.Alpha, eps)) parts.Add("Alpha: " + F(o.Alpha) + " -> " + F(Alpha));
        if (!Near(Scale, o.Scale, eps)) parts.Add("Scale: " + F(o.Scale) + " -> " + F(Scale));

        if (!Near(OriginalAlpha, o.OriginalAlpha, eps)) parts.Add("OriginalAlpha: " + F(o.OriginalAlpha) + " -> " + F(OriginalAlpha));

        if (PhaseAware != o.PhaseAware) parts.Add("PhaseAware " + o.PhaseAware + " -> " + PhaseAware);
        if (!Near(PlatformScale, o.PlatformScale, eps)) parts.Add("PlatformScale: " + F(o.PlatformScale) + " -> " + F(PlatformScale));

        if (StepSound != o.StepSound) parts.Add("StepSound " + o.StepSound + " -> " + StepSound);
        if (!Near(StepSoundVolume, o.StepSoundVolume, eps)) parts.Add("StepSoundVolume: " + F(o.StepSoundVolume) + " -> " + F(StepSoundVolume));

        if (parts.Count == 0) return null;

        return string.Join(", ", parts.ToArray());
    }

    private static string F(float value)
    {
        return value.ToString("F3");
    }

    public void Sanitize()
    {
        StepDegrees = Fix(StepDegrees, 15f, 180f, 45f);
        StepTime    = Fix(StepTime,     0.2f,   4f, 0.246941909f);
        StepRest    = Fix(StepRest,     0f,     4f, 2.979122f);

        Alpha = Fix(Alpha, 0.05f, 1f, 1f);
        Scale = Fix(Scale, 0.5f, 3f, 1f);

        OriginalAlpha = Fix(OriginalAlpha, 0f, 1f, 1f);

        PlatformScale = Fix(PlatformScale, 1f, 4f, 1.10855341f);

        StepSoundVolume = Fix(StepSoundVolume, 0f, 1f, 0.6f);

        if (Mode != HaloMode.Both && Mode != HaloMode.CustomOnly && Mode != HaloMode.OriginalOnly)
            Mode = HaloMode.Both;
    }

    private static float Fix(float value, float min, float max, float fallback)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return fallback;
        if (value < min || value > max) return fallback;
        return value;
    }
}

public static class HaloConfig
{
    public static HaloSettings Current = new HaloSettings();

    public static Action SaveHook;

    public static bool Dirty;

    public static float InputBlockedUntil;

    public static bool InputBlocked
    {
        get { return Time.unscaledTime < InputBlockedUntil; }
    }

    public static void BlockInputFor(float seconds)
    {
        float until = Time.unscaledTime + seconds;

        if (until > InputBlockedUntil) InputBlockedUntil = until;
    }

    public static void NotifyChanged()
    {
        Dirty = true;
    }

    public static bool Flush()
    {
        if (!Dirty) return false;

        Dirty = false;

        SaveHook?.Invoke();
        return true;
    }

    public static void Load(HaloSettings settings)
    {
        Current = settings ?? new HaloSettings();
        Current.Sanitize();
    }

    public static void Reset()
    {
        Current = new HaloSettings();
        NotifyChanged();
    }
}
