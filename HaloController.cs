using System.Collections.Generic;
using UnityEngine;

public class HaloController : MonoBehaviour
{
    public static readonly List<HaloController> Active = new List<HaloController>();

    private SpriteRenderer _sr;
    private Sprite _originalSprite;
    private Sprite _customSprite;
    private Color _originalColor;
    private Vector3 _baseScale;
    private bool _reportedSwap;

    private float _angle;
    private float _currentSpeed;
    private float _stepT;
    private float _stepFrom;
    private float _stepTo;
    private bool _stepActive;
    private float _lastStepDegrees = -1f;

    public static HaloController Attach(GameObject haloGO)
    {
        if (haloGO == null) return null;

        if (haloGO.GetComponent<HaloController>() != null) return null;

        return haloGO.AddComponent<HaloController>();
    }

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        if (_sr == null)
        {
            Debug.Log("[RadianceHalo] Halo has no SpriteRenderer.");
            enabled = false;
            return;
        }

        _originalSprite = _sr.sprite;
        _originalColor  = _sr.color;
        _baseScale      = transform.localScale;
        _angle          = transform.localEulerAngles.z;
        _currentSpeed   = 0f;

        Active.Add(this);
        ApplyCustomSprite();
    }

    private void OnDestroy()
    {
        Active.Remove(this);
    }

    private void ApplyCustomSprite()
    {
        if (_sr == null) return;

        Texture2D tex = RadianceHalo.Instance != null ? RadianceHalo.Instance.HaloTexture : null;
        if (tex == null) return;

        if (_customSprite != null && _customSprite.texture == tex)
        {
            ReassertSprite(_customSprite);
            return;
        }

        if (_originalSprite == null) return;

        float ppu = tex.width / Mathf.Max(0.0001f, _originalSprite.bounds.size.x);

        Sprite previous = _customSprite;

        _customSprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            ppu);

        _sr.sprite = _customSprite;

        if (previous != null) Destroy(previous);

        LogState(tex, ppu);
    }

    private void LogState(Texture2D tex, float ppu)
    {
        RadianceHalo mod = RadianceHalo.Instance;
        if (mod == null) return;

        HaloSettings s = HaloConfig.Current;

        mod.Log(
            "Halo: texture " + tex.width + "x" + tex.height
            + ", ppu " + ppu.ToString("F2")
            + ", world " + (tex.width / ppu).ToString("F2") + "x" + (tex.height / ppu).ToString("F2")
            + " (original world " + _originalSprite.bounds.size.x.ToString("F2") + ")"
            + ", alpha " + s.Alpha.ToString("F2")
            + ", scale " + s.Scale.ToString("F2")
            + ", mode " + s.Mode);
    }

    private void ReassertSprite(Sprite sprite)
    {
        if (_sr == null || _sr.sprite == sprite) return;

        if (!_reportedSwap)
        {
            _reportedSwap = true;
            Debug.Log("[RadianceHalo] Halo sprite was replaced externally, re-applying.");
        }

        _sr.sprite = sprite;
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        HaloSettings s = HaloConfig.Current;

        if (!s.Enabled)
        {
            _currentSpeed = 0f;
            ReassertSprite(_originalSprite);
            if (_sr != null) _sr.color = _originalColor;
            transform.localScale = _baseScale;
            return;
        }

        ApplyCustomSprite();

        float dir = s.Reverse ? -1f : 1f;

        if (s.Mode == HaloMode.Stepped)
        {
            _currentSpeed = 0f;
            TickStepped(dt, s, dir);
        }
        else
        {
            UpdateSpeed(s, dt);
            _angle += _currentSpeed * dir * dt;
        }

        ApplyRotation();
        ApplyScale(s);
        ApplyAlpha(dt, s);
    }

    private void UpdateSpeed(HaloSettings s, float dt)
    {
        float target = s.Speed;
        float accel  = Mathf.Max(0f, s.Acceleration);

        if (accel <= 0f)
        {
            _currentSpeed = target;
            return;
        }

        _currentSpeed = Mathf.MoveTowards(_currentSpeed, target, accel * dt);
    }

    private void TickStepped(float dt, HaloSettings s, float dir)
    {
        float stepDeg  = Mathf.Max(1f, s.StepDegrees);
        float stepTime = Mathf.Max(0.05f, s.StepTime);

        if (Mathf.Abs(stepDeg - _lastStepDegrees) > 0.01f || !_stepActive)
        {
            _lastStepDegrees = stepDeg;
            _stepFrom   = _angle;
            _stepTo     = _stepFrom + stepDeg * dir;
            _stepT      = 0f;
            _stepActive = true;
        }

        _stepT += dt / stepTime;

        if (_stepT >= 1f)
        {
            _angle    = _stepTo;
            _stepFrom = _stepTo;
            _stepTo   = _stepFrom + stepDeg * dir;
            _stepT    -= 1f;
        }

        _angle = Mathf.LerpAngle(_stepFrom, _stepTo, ClockEase(Mathf.Clamp01(_stepT)));
    }

    private static float ClockEase(float t)
    {
        if (t < 0.18f) { float n = t / 0.18f; return n * n * 0.12f; }
        if (t < 0.78f) return 0.12f + (t - 0.18f) / 0.60f * 0.78f;
        float b = (t - 0.78f) / 0.22f;
        return 0.90f + b * 0.10f + Mathf.Sin(b * Mathf.PI) * 0.10f * (1f - b);
    }

    private void ApplyRotation()
    {
        transform.localRotation = Quaternion.Euler(0f, 0f, _angle);
    }

    private void ApplyScale(HaloSettings s)
    {
        transform.localScale = new Vector3(_baseScale.x * s.Scale, _baseScale.y * s.Scale, _baseScale.z);
    }

    private void ApplyAlpha(float dt, HaloSettings s)
    {
        if (_sr == null) return;
        Color c = _sr.color;
        c.a = Mathf.Lerp(c.a, s.Alpha, Mathf.Clamp01(dt * 6f));
        _sr.color = c;
    }

    public void Restore()
    {
        if (_sr != null)
        {
            _sr.sprite = _originalSprite;
            _sr.color  = _originalColor;
        }
        transform.localRotation = Quaternion.Euler(0f, 0f, _angle);
        transform.localScale    = _baseScale;
    }
}