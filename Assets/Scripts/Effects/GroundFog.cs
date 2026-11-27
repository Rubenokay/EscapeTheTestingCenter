using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Low, drifting white mist (horror-movie style) that hugs the floor around the player.
/// Fades in when the power goes out (if a PowerOutageController exists), otherwise is always on.
/// Put it on the Main Camera. It needs a URP particle material (the setup menu assigns one).
/// </summary>
public class GroundFog : MonoBehaviour
{
    public Material particleMaterial;

    [Header("Look")]
    [Tooltip("Alpha (A) = thickness of the mist.")]
    public Color mistColor = new Color(0.8f, 0.82f, 0.86f, 0.3f);
    public Vector2 puffSize = new Vector2(2.5f, 5f);
    [Tooltip("Tick to set the floor height yourself instead of detecting the Plane/Ground.")]
    public bool overrideFloorHeight = false;
    public float floorHeight = 1f;
    [Tooltip("How high above the floor the mist sits.")]
    public float heightAboveFloor = 0.5f;
    public float layerThickness = 0.8f;
    public Vector2 areaSize = new Vector2(18f, 18f);

    [Tooltip("Spread the mist over the whole floor (Plane/Ground) instead of following the player.")]
    public bool coverWholeFloor = true;

    [Header("Amount / motion")]
    public int maxPuffs = 160;
    public float puffsPerSecond = 12f;
    public float driftSpeed = 0.15f;
    public Vector3 wind = new Vector3(0.08f, 0f, 0.03f);

    [Tooltip("Softens hard lines where mist meets the floor. Turn off if the mist is invisible.")]
    public bool softEdges = false;

    [Header("Fade in with the outage")]
    public bool onlyDuringOutage = true;
    public float fadeTime = 4f;

    private ParticleSystem ps;
    private Transform emitter;
    private Material mat;
    private PowerOutageController outage;
    private float strength;
    private float floorY;
    private Renderer floorRenderer;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    void Start()
    {
        outage = FindFirstObjectByType<PowerOutageController>();
        strength = (onlyDuringOutage && outage != null) ? 0f : 1f;
        floorY = FindFloor();

        var go = new GameObject("Ground Fog");
        emitter = go.transform;
        emitter.position = new Vector3(transform.position.x, floorY + heightAboveFloor, transform.position.z);

        // cover the whole platform: centre on it, match its size, scale the amount by area
        if (coverWholeFloor && floorRenderer != null)
        {
            Bounds b = floorRenderer.bounds;
            emitter.position = new Vector3(b.center.x, floorY + heightAboveFloor, b.center.z);
            float baseArea = areaSize.x * areaSize.y;
            areaSize = new Vector2(b.size.x, b.size.z);
            float k = Mathf.Max(1f, (areaSize.x * areaSize.y) / baseArea);
            puffsPerSecond *= k;
            maxPuffs = Mathf.RoundToInt(maxPuffs * k);
        }

        ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(12f, 18f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, driftSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(puffSize.x, puffSize.y);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Color.white;
        main.maxParticles = maxPuffs;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.prewarm = true;

        var emission = ps.emission;
        emission.rateOverTime = puffsPerSecond;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(areaSize.x, layerThickness, areaSize.y);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = wind.x; vel.y = wind.y; vel.z = wind.z;

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

        var growth = ps.sizeOverLifetime;
        growth.enabled = true;
        growth.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.8f, 1f, 1.3f));

        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        fade.color = g;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sortMode = ParticleSystemSortMode.Distance;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.maxParticleSize = 2f;

        if (particleMaterial != null)
        {
            mat = new Material(particleMaterial);
            mat.SetTexture("_BaseMap", MakePuffTexture());
            mat.SetTexture("_MainTex", mat.GetTexture("_BaseMap"));
            // soft edges where the mist meets the floor/furniture
            if (softEdges)
            {
                mat.EnableKeyword("_SOFTPARTICLES_ON");
                mat.SetFloat("_SoftParticlesEnabled", 1f);
                mat.SetVector("_SoftParticleFadeParams", new Vector4(0f, 1f / 1.5f, 0f, 0f));
            }
            else
            {
                mat.DisableKeyword("_SOFTPARTICLES_ON");
                mat.SetFloat("_SoftParticlesEnabled", 0f);
            }
            r.sharedMaterial = mat;
        }

        var camData = GetComponent<UniversalAdditionalCameraData>();
        if (camData != null) camData.requiresDepthTexture = true;

        if (particleMaterial == null)
            Debug.LogWarning("GroundFog: no Particle Material assigned. Run Tools > Escape The Testing Center > Add Horror Ground Fog again (outside Play mode).");
        Debug.Log($"GroundFog: camera at y={transform.position.y:0.00}, floor at y={floorY:0.00}, mist at y={floorY + heightAboveFloor:0.00}. " +
                  (strength > 0f ? "Visible now." : "Fades in when the power goes out."));

        ApplyStrength();
        ps.Play();
    }

    void LateUpdate()
    {
        if (emitter == null) return;

        // follow the player horizontally, stay on the floor
        if (!(coverWholeFloor && floorRenderer != null)) emitter.position = new Vector3(transform.position.x, floorY + heightAboveFloor, transform.position.z);

        float target = (!onlyDuringOutage || outage == null || outage.IsOutage) ? 1f : 0f;
        if (!Mathf.Approximately(strength, target))
        {
            strength = Mathf.MoveTowards(strength, target, Time.deltaTime / Mathf.Max(0.01f, fadeTime));
            ApplyStrength();
        }
    }

    private void ApplyStrength()
    {
        if (mat == null) return;
        Color c = mistColor;
        c.a *= strength;
        mat.SetColor(BaseColorId, c);
    }

    private float FindFloor()
    {
        float camY = transform.position.y;

        if (overrideFloorHeight) return floorHeight;

        // 1) the visible floor: highest Plane/Ground/Floor renderer that is below the camera
        float best = float.MinValue;
        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            string n = r.gameObject.name.ToLower();
            if (!(n.Contains("plane") || n.Contains("ground") || n.Contains("floor"))) continue;
            float top = r.bounds.max.y;
            if (top > best) { best = top; floorRenderer = r; }
        }
        if (best > float.MinValue) return best;

        // 2) first collider under the camera that isn't the player
        Transform player = transform.root;
        RaycastHit[] hits = Physics.RaycastAll(transform.position, Vector3.down, 50f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit h in hits)
            if (!h.collider.transform.IsChildOf(player)) return h.point.y;

        // 3) guess: standing height below the camera
        return camY - 1.6f;
    }

    // soft, cloudy round puff generated in code so no texture file is needed
    private static Texture2D MakePuffTexture()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float ox = Random.value * 100f, oy = Random.value * 100f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (size - 1f) * 2f - 1f;
                float v = y / (size - 1f) * 2f - 1f;
                float d = Mathf.Sqrt(u * u + v * v);
                float falloff = Mathf.Clamp01(1f - d);
                falloff = falloff * falloff * (3f - 2f * falloff);

                float n = 0f, amp = 0.5f, freq = 3f;
                for (int o = 0; o < 4; o++)
                {
                    n += Mathf.PerlinNoise(ox + u * freq, oy + v * freq) * amp;
                    amp *= 0.5f; freq *= 2f;
                }
                float a = falloff * Mathf.Lerp(0.4f, 1.1f, n);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
            }
        }
        tex.Apply();
        return tex;
    }
}
