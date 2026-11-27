using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Runs the opening power outage:
/// normal lights flicker and die -> short blackout -> red emergency lights kick in,
/// with darker ambient light, fog, post-processing (vignette, grain, desaturation),
/// a small camera shake, optional sounds and an on-screen message.
/// Press the debug key (default O) in Play mode to trigger it manually.
/// </summary>
public class PowerOutageController : MonoBehaviour
{
    [Header("Timing")]
    public bool triggerOnStart = true;
    public float delayBeforeOutage = 3f;
    public float flickerDuration = 1.5f;
    [Tooltip("Seconds of total darkness before the emergency lights turn on.")]
    public float blackoutDuration = 1.5f;
    public KeyCode debugTriggerKey = KeyCode.O;

    [Header("Normal lights (leave empty = every non-emergency light in the scene)")]
    public List<Light> mainLights = new List<Light>();
    [Range(0f, 1f)] public float remainingLightFraction = 0.02f;

    [Header("Emergency lights (leave empty = find all EmergencyLight components)")]
    public List<EmergencyLight> emergencyLights = new List<EmergencyLight>();

    [Header("Ambient / Fog")]
    public Color outageAmbientColor = new Color(0.03f, 0.02f, 0.025f);
    [Range(0f, 1f)] public float outageReflectionIntensity = 0.1f;
    public bool enableFog = true;
    [Tooltip("Dim red so the haze is visible against the emergency lights.")]
    public Color fogColor = new Color(0.12f, 0.03f, 0.03f);
    public float fogDensity = 0.12f;

    [Header("Post processing")]
    public bool usePostProcessing = true;
    public float postFxFadeTime = 2f;
    [Range(0f, 1f)] public float vignetteIntensity = 0.45f;
    [Range(0f, 1f)] public float filmGrainIntensity = 0.35f;
    [Range(-100f, 0f)] public float saturation = -35f;
    public float exposureDrop = -0.5f;

    [Header("Camera shake")]
    public float shakeStrength = 0.04f;
    public float shakeDuration = 0.4f;

    [Header("Audio (optional)")]
    public AudioClip flickerBuzz;
    public AudioClip powerDownSound;
    public AudioClip emergencyOnSound;
    public AudioClip alarmLoop;
    [Range(0f, 1f)] public float alarmVolume = 0.35f;

    [Header("Objects to toggle (e.g. monitor screens)")]
    public GameObject[] disableOnOutage;
    public GameObject[] enableOnOutage;

    [Header("On-screen message (optional)")]
    [Tooltip("Leave empty and a warning text is created automatically.")]
    public TMP_Text messageText;
    public bool createMessageIfMissing = true;
    public string outageMessage = "POWER FAILURE\nEmergency lighting active";
    public float messageDuration = 3.5f;

    [Header("Events")]
    public UnityEvent onPowerLost;
    public UnityEvent onEmergencyLightsOn;
    public UnityEvent onPowerRestored;

    public bool IsOutage { get; private set; }

    // saved state so power can be restored later (e.g. a generator puzzle)
    private readonly Dictionary<Light, float> originalIntensities = new Dictionary<Light, float>();
    private Color originalAmbientLight;
    private AmbientMode originalAmbientMode;
    private SphericalHarmonicsL2 originalAmbientProbe;
    private float originalReflectionIntensity;
    private bool originalFog;
    private Color originalFogColor;
    private FogMode originalFogMode;
    private float originalFogDensity;
    private CameraClearFlags originalClearFlags;
    private Color originalBackground;
    private Material originalSkybox;

    private Camera cam;
    private AudioSource sfxSource;
    private AudioSource alarmSource;
    private Volume outageVolume;
    private Coroutine running;

    void Awake()
    {
        cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();

        if (mainLights.Count == 0)
        {
            foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (l.GetComponent<EmergencyLight>() == null) mainLights.Add(l);
            }
        }

        if (emergencyLights.Count == 0)
        {
            emergencyLights.AddRange(FindObjectsByType<EmergencyLight>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        }

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;

        alarmSource = gameObject.AddComponent<AudioSource>();
        alarmSource.playOnAwake = false;
        alarmSource.loop = true;
        alarmSource.spatialBlend = 0f;
        alarmSource.volume = alarmVolume;

        if (usePostProcessing) BuildVolume();

        if (messageText == null && createMessageIfMissing) messageText = CreateMessageUI();
        if (messageText != null) messageText.gameObject.SetActive(false);

        SaveOriginalState();
    }

    void Start()
    {
        if (triggerOnStart) running = StartCoroutine(OutageSequence(delayBeforeOutage));
    }

    void Update()
    {
        if (Input.GetKeyDown(debugTriggerKey))
        {
            if (IsOutage) RestorePower();
            else TriggerOutage();
        }
    }

    /// <summary>Starts the outage right now (call from triggers, puzzles, etc.).</summary>
    public void TriggerOutage()
    {
        if (IsOutage) return;
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(OutageSequence(0f));
    }

    private IEnumerator OutageSequence(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        IsOutage = true;

        // 1) Flicker
        if (flickerBuzz != null) sfxSource.PlayOneShot(flickerBuzz);
        float end = Time.time + flickerDuration;
        bool lit = true;
        while (Time.time < end)
        {
            lit = !lit;
            float level = lit ? Random.Range(0.4f, 1f) : Random.Range(0f, 0.1f);
            SetMainLights(level);
            yield return new WaitForSeconds(Random.Range(0.03f, 0.15f));
        }

        // 2) Power dies
        SetMainLights(remainingLightFraction);
        ApplyDarkEnvironment();
        foreach (GameObject go in disableOnOutage) if (go != null) go.SetActive(false);
        if (powerDownSound != null) sfxSource.PlayOneShot(powerDownSound);
        StartCoroutine(Shake());
        onPowerLost?.Invoke();

        yield return new WaitForSeconds(blackoutDuration);

        // 3) Emergency lights
        foreach (EmergencyLight e in emergencyLights)
        {
            if (e == null) continue;
            e.gameObject.SetActive(true);
            e.SetOn(true);
            yield return new WaitForSeconds(0.12f); // they click on one after another
        }
        foreach (GameObject go in enableOnOutage) if (go != null) go.SetActive(true);
        if (emergencyOnSound != null) sfxSource.PlayOneShot(emergencyOnSound);
        if (alarmLoop != null) { alarmSource.clip = alarmLoop; alarmSource.Play(); }
        onEmergencyLightsOn?.Invoke();

        StartCoroutine(FadeVolume(1f));
        if (messageText != null) StartCoroutine(ShowMessage());
        running = null;
    }

    /// <summary>Brings everything back to normal.</summary>
    public void RestorePower()
    {
        if (running != null) { StopCoroutine(running); running = null; }
        IsOutage = false;

        foreach (var kv in originalIntensities)
            if (kv.Key != null) kv.Key.intensity = kv.Value;

        RenderSettings.ambientMode = originalAmbientMode;
        RenderSettings.ambientLight = originalAmbientLight;
        RenderSettings.ambientProbe = originalAmbientProbe;
        RenderSettings.reflectionIntensity = originalReflectionIntensity;
        RenderSettings.fog = originalFog;
        RenderSettings.fogColor = originalFogColor;
        RenderSettings.fogMode = originalFogMode;
        RenderSettings.fogDensity = originalFogDensity;
        if (cam != null) { cam.clearFlags = originalClearFlags; cam.backgroundColor = originalBackground; }
        RenderSettings.skybox = originalSkybox;

        foreach (EmergencyLight e in emergencyLights) if (e != null) e.SetOn(false);
        foreach (GameObject go in disableOnOutage) if (go != null) go.SetActive(true);
        foreach (GameObject go in enableOnOutage) if (go != null) go.SetActive(false);
        alarmSource.Stop();
        StartCoroutine(FadeVolume(0f));
        onPowerRestored?.Invoke();
    }

    // ---------- helpers ----------

    private void SaveOriginalState()
    {
        foreach (Light l in mainLights)
            if (l != null && !originalIntensities.ContainsKey(l)) originalIntensities[l] = l.intensity;

        originalAmbientMode = RenderSettings.ambientMode;
        originalAmbientLight = RenderSettings.ambientLight;
        originalAmbientProbe = RenderSettings.ambientProbe;
        originalReflectionIntensity = RenderSettings.reflectionIntensity;
        originalFog = RenderSettings.fog;
        originalFogColor = RenderSettings.fogColor;
        originalFogMode = RenderSettings.fogMode;
        originalFogDensity = RenderSettings.fogDensity;
        if (cam != null) { originalClearFlags = cam.clearFlags; originalBackground = cam.backgroundColor; }
        originalSkybox = RenderSettings.skybox;
    }

    private void SetMainLights(float fraction)
    {
        foreach (var kv in originalIntensities)
            if (kv.Key != null) kv.Key.intensity = kv.Value * fraction;
    }

    private void ApplyDarkEnvironment()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = outageAmbientColor;
        var sh = new SphericalHarmonicsL2();
        sh.AddAmbientLight(outageAmbientColor);
        RenderSettings.ambientProbe = sh;
        RenderSettings.reflectionIntensity = outageReflectionIntensity;

        if (enableFog)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
        }

        // hide the bright sky so it actually feels like a blackout
        RenderSettings.skybox = null;
        if (cam == null) cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        foreach (Camera c in Camera.allCameras)
        {
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = enableFog ? fogColor : Color.black;
        }
    }

    private void BuildVolume()
    {
        if (cam != null)
        {
            var camData = cam.GetComponent<UniversalAdditionalCameraData>();
            if (camData != null) camData.renderPostProcessing = true;
        }

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(vignetteIntensity);
        vignette.smoothness.Override(0.5f);
        vignette.color.Override(new Color(0.08f, 0f, 0f));

        var grain = profile.Add<FilmGrain>(true);
        grain.type.Override(FilmGrainLookup.Medium3);
        grain.intensity.Override(filmGrainIntensity);

        var color = profile.Add<ColorAdjustments>(true);
        color.saturation.Override(saturation);
        color.postExposure.Override(exposureDrop);
        color.contrast.Override(15f);

        var chroma = profile.Add<ChromaticAberration>(true);
        chroma.intensity.Override(0.2f);

        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.9f);
        bloom.intensity.Override(0.8f);

        outageVolume = gameObject.AddComponent<Volume>();
        outageVolume.isGlobal = true;
        outageVolume.priority = 50f;
        outageVolume.weight = 0f;
        outageVolume.sharedProfile = profile;
    }

    private IEnumerator FadeVolume(float target)
    {
        if (outageVolume == null) yield break;
        float start = outageVolume.weight;
        float t = 0f;
        while (t < postFxFadeTime)
        {
            t += Time.deltaTime;
            outageVolume.weight = Mathf.Lerp(start, target, t / postFxFadeTime);
            yield return null;
        }
        outageVolume.weight = target;
    }

    private IEnumerator Shake()
    {
        if (cam == null || shakeStrength <= 0f) yield break;
        Transform t = cam.transform;
        Vector3 basePos = t.localPosition;
        float end = Time.time + shakeDuration;
        while (Time.time < end)
        {
            float falloff = (end - Time.time) / shakeDuration;
            t.localPosition = basePos + Random.insideUnitSphere * shakeStrength * falloff;
            yield return null;
        }
        t.localPosition = basePos;
    }

    private TMP_Text CreateMessageUI()
    {
        var canvasGo = new GameObject("Outage Message Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var textGo = new GameObject("Outage Message", typeof(RectTransform));
        textGo.transform.SetParent(canvasGo.transform, false);
        var rt = (RectTransform)textGo.transform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -80f);
        rt.sizeDelta = new Vector2(1200f, 160f);

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 48;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(1f, 0.2f, 0.15f);
        tmp.raycastTarget = false;
        return tmp;
    }

    private IEnumerator ShowMessage()
    {
        messageText.text = outageMessage;
        messageText.gameObject.SetActive(true);
        Color c = messageText.color;

        // blink a few times like a warning display
        for (int i = 0; i < 3; i++)
        {
            messageText.alpha = 1f; yield return new WaitForSeconds(0.35f);
            messageText.alpha = 0.2f; yield return new WaitForSeconds(0.2f);
        }
        messageText.alpha = 1f;
        yield return new WaitForSeconds(messageDuration);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime;
            messageText.alpha = 1f - t;
            yield return null;
        }
        messageText.gameObject.SetActive(false);
        messageText.color = c;
    }
}
