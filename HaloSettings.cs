using System;

public class HaloSettings
{
    public bool Enabled = true;

    public bool Reverse = false;

    public float StepDegrees = 45f;
    public float StepTime = 0.246941909f;
    public float StepRest = 2.979122f;

    public float Alpha = 0.9f;
    public float Scale = 1f;

    public bool PhaseAware = true;
    public float PlatformScale = 1.10855341f;

    public bool StepSound = true;
    public float StepSoundVolume = 0.6f;

    public void Sanitize()
    {
        Alpha = Fix(Alpha, 0.05f, 1f, 1f);
        Scale = Fix(Scale, 0.5f, 3f, 1f);

        PlatformScale = Fix(PlatformScale, 1f, 4f, 1.10855341f);

        StepSoundVolume = Fix(StepSoundVolume, 0f, 1f, 0.6f);

        StepDegrees = Fix(StepDegrees, 15f, 180f, 90f);
        StepTime    = Fix(StepTime,     0.2f,  4f,  1f);
        StepRest    = Fix(StepRest,     0f,    4f,  0.1f);
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

    public static void NotifyChanged()
    {
        SaveHook?.Invoke();
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