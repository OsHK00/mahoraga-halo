using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;

public class HaloController : MonoBehaviour
{
    public static readonly List<HaloController> Active = new List<HaloController>();

    public const string PhaseFsmName = "Attack Choices";
    public const string ArenaVarName = "Arena";
    public const int ArenaPlatforms = 2;

    private const float PlatformBlend = 1.5f;
    private const float StepSoundOffset = -0.1f;

    private SpriteRenderer _sr;
    private Sprite _originalSprite;
    private Sprite _customSprite;
    private Color _originalColor;
    private Vector3 _baseScale;

    private PlayMakerFSM _phaseFsm;
    private FsmInt _arena;
    private float _phaseRetry;
    private bool _platformActive;
    private float _scaleMul = 1f;

    private float _angle;
    private float _stepT;
    private float _stepFrom;
    private float _stepTo;
    private bool _stepActive;
    private bool _stepPending;
    private float _stepHitT;
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
            Debug.Log("[mahoraga-halo] Halo has no SpriteRenderer.");
            enabled = false;
            return;
        }

        _originalSprite = _sr.sprite;
        _originalColor  = _sr.color;
        _baseScale      = transform.localScale;
        _angle          = transform.localEulerAngles.z;
        _scaleMul       = 1f;

        LocatePhaseVariable();
        LogPhaseHook();

        Active.Add(this);
        ApplyCustomSprite();
    }

    private void LocatePhaseVariable()
    {
        _phaseFsm = null;
        _arena    = null;

        if (transform.parent == null) return;

        _phaseFsm = PlayMakerFSM.FindFsmOnGameObject(transform.parent.gameObject, PhaseFsmName);
        if (_phaseFsm == null) return;

        _arena = _phaseFsm.FsmVariables.GetFsmInt(ArenaVarName);
    }

    private void UpdatePhase(float dt, HaloSettings s)
    {
        if (!s.PhaseAware)
        {
            _platformActive = false;
            return;
        }

        if (_arena == null || _phaseFsm == null)
        {
            _phaseRetry -= dt;
            if (_phaseRetry <= 0f)
            {
                _phaseRetry = 0.5f;
                LocatePhaseVariable();
            }
        }

        _platformActive = _arena != null && _arena.Value == ArenaPlatforms;
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
    }

    private void LogPhaseHook()
    {
        RadianceHalo.Trace("Halo attached: arena " + (_arena != null ? _arena.Value.ToString() : "?")
            + ", platforms " + _platformActive
            + ", phaseFsm " + (_phaseFsm != null));
    }

    private void ReassertSprite(Sprite sprite)
    {
        if (_sr == null || _sr.sprite == sprite) return;

        _sr.sprite = sprite;
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        HaloSettings s = HaloConfig.Current;

        if (!s.Enabled)
        {
            _platformActive = false;
            ReassertSprite(_originalSprite);
            if (_sr != null) _sr.color = _originalColor;
            transform.localScale = _baseScale;
            return;
        }

        ApplyCustomSprite();

        UpdatePhase(dt, s);

        TickStepped(dt, s, s.Reverse ? -1f : 1f);

        ApplyRotation();
        ApplyScale(dt, s);
        ApplyAlpha(dt, s);
    }

    private void TickStepped(float dt, HaloSettings s, float dir)
    {
        float stepDeg  = Mathf.Max(1f, s.StepDegrees);
        float stepTime = Mathf.Max(0.05f, s.StepTime);
        float stepRest = Mathf.Max(0f, s.StepRest);
        float cycle    = stepTime + stepRest;

        if (Mathf.Abs(stepDeg - _lastStepDegrees) > 0.01f || !_stepActive)
        {
            _lastStepDegrees = stepDeg;
            _stepFrom   = _angle;
            _stepTo     = _stepFrom + stepDeg * dir;
            _stepT      = 0f;
            _stepActive = true;
            ArmStepSound(stepTime);
        }

        _stepT += dt / cycle;

        if (_stepT >= 1f)
        {
            _angle    = _stepTo;
            _stepFrom = _stepTo;
            _stepTo   = _stepFrom + stepDeg * dir;
            _stepT    -= 1f;
            ArmStepSound(stepTime);
        }

        float moveT = _stepT * cycle;
        float t = moveT >= stepTime ? 1f : ClockEase(moveT / stepTime);

        _angle = Mathf.LerpAngle(_stepFrom, _stepTo, t);

        if (_stepPending && moveT >= _stepHitT)
        {
            _stepPending = false;
            PlayStepSound(s);
        }
    }

    private void ArmStepSound(float stepTime)
    {
        _stepPending = true;
        _stepHitT = Mathf.Max(0f, stepTime + StepSoundOffset);
    }

    private void PlayStepSound(HaloSettings s)
    {
        if (!s.StepSound || s.StepSoundVolume <= 0f) return;

        HaloAudioPlayer.Ensure().Play(s.StepSoundVolume);
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

    private void ApplyScale(float dt, HaloSettings s)
    {
        float target   = _platformActive ? Mathf.Max(1f, s.PlatformScale) : 1f;
        float duration = PlatformBlend;
        float rate     = 3f / duration;

        _scaleMul = Mathf.Lerp(_scaleMul, target, 1f - Mathf.Exp(-rate * dt));

        float mul = s.Scale * _scaleMul;

        transform.localScale = new Vector3(_baseScale.x * mul, _baseScale.y * mul, _baseScale.z);
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
        _platformActive = false;
        _scaleMul = 1f;

        if (_sr != null)
        {
            _sr.sprite = _originalSprite;
            _sr.color  = _originalColor;
        }
        transform.localRotation = Quaternion.Euler(0f, 0f, _angle);
        transform.localScale    = _baseScale;
    }
}