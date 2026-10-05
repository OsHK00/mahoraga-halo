using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;

public class HaloController : MonoBehaviour
{
    public static readonly List<HaloController> Active = new List<HaloController>();

    public const string PhaseFsmName = "Attack Choices";
    public const string ArenaVarName = "Arena";
    public const int ArenaPlatforms = 2;

    public const string CustomHaloName = "Halo (Mahoraga)";

    private const float PlatformBlend = 1.5f;
    private const float StepSoundOffset = -0.1f;

    private Transform _original;
    private SpriteRenderer _originalSr;

    private SpriteRenderer _sr;
    private Sprite _originalSprite;
    private Sprite _customSprite;
    private Color _originalColor;
    private Vector3 _baseScale;
    private int _baseSortOrder;

    private PlayMakerFSM _phaseFsm;
    private FsmInt _arena;
    private float _phaseRetry;
    private bool _platformActive;
    private float _scaleMul = 1f;

    private float _angle;
    private float _appliedOriginalMul = 1f;
    private float _stepT;
    private float _stepFrom;
    private float _stepTo;
    private bool _stepActive;
    private bool _stepPending;
    private float _stepHitT;
    private float _lastStepDegrees = -1f;

    public GameObject CustomHaloObject { get; private set; }

    public static HaloController Ensure(GameObject originalGO)
    {
        if (originalGO == null) return null;

        Transform boss = originalGO.transform.parent;
        if (boss == null) return null;

        HaloController existing = FindCustom(boss);
        if (existing != null)
        {
            existing.BindOriginal(originalGO);
            return existing;
        }

        GameObject clone = CreateClone(originalGO);
        if (clone == null) return null;

        HaloController ctrl = clone.AddComponent<HaloController>();
        ctrl.CustomHaloObject = clone;
        ctrl.BindOriginal(originalGO);
        return ctrl;
    }

    private static HaloController FindCustom(Transform boss)
    {
        Transform found = boss.Find(CustomHaloName);
        if (found == null) return null;

        return found.GetComponent<HaloController>();
    }

    private static GameObject CreateClone(GameObject originalGO)
    {
        GameObject clone;
        try
        {
            clone = Instantiate(originalGO);
        }
        catch
        {
            return null;
        }

        if (clone == null) return null;

        clone.name = CustomHaloName;

        Transform parent = originalGO.transform.parent;
        if (parent != null)
        {
            clone.transform.SetParent(parent, false);
            clone.transform.localPosition   = originalGO.transform.localPosition;
            clone.transform.localRotation   = originalGO.transform.localRotation;
            clone.transform.localScale      = originalGO.transform.localScale;
        }

        StripComponents(clone);
        return clone;
    }

    private static void StripComponents(GameObject clone)
    {
        Component[] components = clone.GetComponents<Component>();
        for (int i = components.Length - 1; i >= 0; i--)
        {
            Component component = components[i];
            if (component == null) continue;

            if (component is Transform) continue;
            if (component is SpriteRenderer) continue;
            if (component is HaloController) continue;

            Destroy(component);
        }

        foreach (Collider2D collider in clone.GetComponentsInChildren<Collider2D>(true))
        {
            if (collider != null) Destroy(collider.gameObject == clone ? (Component)collider : collider.gameObject);
        }
    }

    public void BindOriginal(GameObject originalGO)
    {
        if (originalGO == null)
        {
            _original  = null;
            _originalSr = null;
            return;
        }

        if (_original == originalGO.transform) return;

        _original   = originalGO.transform;
        _originalSr = originalGO.GetComponent<SpriteRenderer>();
        _baseSortOrder = _originalSr != null ? _originalSr.sortingOrder : 0;
    }

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        if (_sr == null)
        {
            Debug.Log("[mahoraga-halo] Custom halo has no SpriteRenderer.");
            enabled = false;
            return;
        }

        _originalSprite = _sr.sprite;
        _originalColor  = _sr.color;
        _baseScale      = transform.localScale;
        _baseSortOrder  = _sr.sortingOrder;
        _angle          = transform.localEulerAngles.z;
        _scaleMul       = 1f;

        if (_originalSr != null) _baseSortOrder = _originalSr.sortingOrder;

        LocatePhaseVariable();
        LogPhaseHook();

        if (!Active.Contains(this)) Active.Add(this);

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

    private Vector3 OriginalScale()
    {
        if (_original == null) return _baseScale;
        return _original.localScale;
    }

    private void MirrorPosition()
    {
        if (_original == null) return;

        transform.localPosition = _original.localPosition;
    }

    private void ApplyLayering()
    {
        if (_sr == null) return;

        if (_sr.sortingOrder != _baseSortOrder) _sr.sortingOrder = _baseSortOrder;
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

        float ppu = OriginalPpu(tex);
        if (ppu <= 0f) return;

        Sprite previous = _customSprite;

        _customSprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            ppu);

        _sr.sprite = _customSprite;

        if (previous != null) Destroy(previous);
    }

    private float OriginalPpu(Texture2D tex)
    {
        Bounds bounds = ReferenceBounds();

        float reference = bounds.size.x;
        if (reference < 0.0001f) reference = bounds.size.y;
        if (reference < 0.0001f) return 0f;

        return tex.width / reference;
    }

    private Bounds ReferenceBounds()
    {
        if (_originalSprite != null) return _originalSprite.bounds;

        if (_originalSr != null && _originalSr.sprite != null) return _originalSr.sprite.bounds;

        return new Bounds(Vector3.zero, Vector3.one);
    }

    private void LogPhaseHook()
    {
        RadianceHalo.Trace("Custom halo ready: arena " + (_arena != null ? _arena.Value.ToString() : "?")
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
            if (_sr != null)
            {
                _sr.color = _originalColor;
                if (_sr.enabled) _sr.enabled = false;
            }
            ForceOriginalVisible();
            return;
        }

        bool wantCustom = s.Mode != HaloMode.OriginalOnly;
        bool wantOriginal = s.Mode != HaloMode.CustomOnly;

        ApplyVisibility(wantCustom, wantOriginal);

        if (wantCustom)
        {
            ApplyCustomSprite();
            ApplyLayering();
            MirrorPosition();

            UpdatePhase(dt, s);

            TickStepped(dt, s, s.Reverse ? -1f : 1f);

            ApplyRotation();
            ApplyScale(dt, s);
            ApplyAlpha(dt, s);
        }

        ApplyOriginalAlpha(s.OriginalAlpha);
    }

    private void ApplyVisibility(bool customVisible, bool originalVisible)
    {
        if (_sr != null && _sr.enabled != customVisible) _sr.enabled = customVisible;

        SetOriginalVisible(originalVisible);
    }

    private void SetOriginalVisible(bool visible)
    {
        if (_originalSr == null) return;

        if (_originalSr.enabled != visible) _originalSr.enabled = visible;
    }

    private void ApplyOriginalAlpha(float multiplier)
    {
        if (_originalSr == null || _originalSr.enabled == false) return;

        float mul = Mathf.Clamp01(multiplier);

        Color c = _originalSr.color;

        float baseAlpha = _appliedOriginalMul > 0.0001f
            ? Mathf.Clamp01(c.a / _appliedOriginalMul)
            : c.a;

        float target = Mathf.Clamp01(baseAlpha * mul);
        if (Mathf.Abs(c.a - target) > 0.001f)
            _originalSr.color = new Color(c.r, c.g, c.b, target);

        _appliedOriginalMul = mul;
    }

    private void ForceOriginalVisible()
    {
        if (_originalSr == null) return;
        if (!_originalSr.enabled) _originalSr.enabled = true;
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

        Vector3 baseScale = OriginalScale();
        float mul = s.Scale * _scaleMul;

        transform.localScale = new Vector3(baseScale.x * mul, baseScale.y * mul, baseScale.z);
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
            _sr.sprite = _customSprite != null ? _customSprite : _originalSprite;
            _sr.color  = _originalColor;
            _sr.sortingOrder = _baseSortOrder;
            if (!_sr.enabled) _sr.enabled = true;
        }

        transform.localRotation = Quaternion.Euler(0f, 0f, _angle);
        transform.localScale    = _baseScale;

        ForceOriginalVisible();
    }
}
