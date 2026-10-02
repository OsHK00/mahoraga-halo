using System;

public enum HaloMode
{
    Normal,
    Stepped
}

public class HaloSettings
{
    public bool Enabled = true;
    public HaloMode Mode = HaloMode.Normal;
    public float Speed = 15f;
    public bool Reverse = false;
    public float Acceleration = 120f;

    public float StepDegrees = 90f;
    public float StepTime = 1f;

public float Alpha = 1f;
    public float Scale = 1f;

    public bool LiveReload = false;

    public void Sanitize()
    {
        Alpha = Fix(Alpha, 0.05f, 1f, 1f);
        Scale = Fix(Scale, 0.5f, 3f, 1f);

        Speed        = Fix(Speed,        0f,   90f,  15f);
        Acceleration = Fix(Acceleration, 0f,  360f, 120f);

        StepDegrees = Fix(StepDegrees, 15f, 180f, 90f);
        StepTime    = Fix(StepTime,     0.2f,   4f,  1f);
    }

    public bool UsesSpeed(HaloMode mode)
    {
        return mode == HaloMode.Normal;
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