using UnityEngine;

/// <summary>
/// A red emergency light that stays off until the power goes out.
/// Modes: Pulse (breathing glow), Rotate (spinning beacon, use a Spot light),
/// Steady (always on), Flicker (broken / unstable light).
/// </summary>
[RequireComponent(typeof(Light))]
public class EmergencyLight : MonoBehaviour
{
    public enum Mode { Pulse, Rotate, Steady, Flicker }

    [Header("Look")]
    public Mode mode = Mode.Pulse;
    public Color color = new Color(1f, 0.08f, 0.04f);
    public float intensity = 6f;
    public float range = 14f;

    [Header("Pulse")]
    public float pulseSpeed = 1.5f;
    [Range(0f, 1f)] public float minIntensityFraction = 0.3f;

    [Header("Rotate")]
    public float rotateSpeed = 180f;

    [Header("Flicker")]
    [Range(0f, 1f)] public float flickerChance = 0.08f;

    [Header("Optional glowing bulb mesh")]
    public Renderer bulbRenderer;
    public float bulbGlow = 4f;

    [Header("State")]
    public bool startOn = false;

    private Light lightSource;
    private bool isOn;
    private float seed;
    private float flickerTimer;
    private float flickerValue = 1f;
    private MaterialPropertyBlock block;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    public bool IsOn => isOn;

    void Awake()
    {
        lightSource = GetComponent<Light>();
        lightSource.color = color;
        lightSource.range = range;
        seed = Random.value * 100f;
        block = new MaterialPropertyBlock();
        SetOn(startOn);
    }

    public void SetOn(bool on)
    {
        isOn = on;
        if (lightSource == null) lightSource = GetComponent<Light>();
        lightSource.enabled = on;
        lightSource.intensity = on ? intensity : 0f;
        ApplyBulb(on ? 1f : 0.05f);
    }

    void Update()
    {
        if (!isOn) return;

        float t = 1f;
        switch (mode)
        {
            case Mode.Pulse:
                float s = (Mathf.Sin((Time.time + seed) * pulseSpeed * Mathf.PI) + 1f) * 0.5f;
                t = Mathf.Lerp(minIntensityFraction, 1f, s);
                break;

            case Mode.Rotate:
                transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
                break;

            case Mode.Flicker:
                flickerTimer -= Time.deltaTime;
                if (flickerTimer <= 0f)
                {
                    flickerValue = Random.value < flickerChance ? Random.Range(0f, 0.3f) : Random.Range(0.8f, 1f);
                    flickerTimer = Random.Range(0.03f, 0.2f);
                }
                t = flickerValue;
                break;
        }

        lightSource.intensity = intensity * t;
        ApplyBulb(t);
    }

    private void ApplyBulb(float t)
    {
        if (bulbRenderer == null) return;
        if (block == null) block = new MaterialPropertyBlock();
        bulbRenderer.GetPropertyBlock(block);
        block.SetColor(BaseColorId, color * Mathf.Max(0.05f, t * bulbGlow));
        bulbRenderer.SetPropertyBlock(block);
    }
}
