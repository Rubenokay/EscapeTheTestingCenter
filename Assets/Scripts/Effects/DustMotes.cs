using UnityEngine;

/// <summary>
/// Floating dust particles that follow the camera. Add to the Main Camera.
/// They catch the red emergency light and make the room feel stale and abandoned.
/// </summary>
public class DustMotes : MonoBehaviour
{
    public Material particleMaterial;
    public Vector3 areaSize = new Vector3(10f, 4f, 10f);
    public int maxParticles = 400;
    public float emissionRate = 50f;
    public Color dustColor = new Color(1f, 0.9f, 0.85f, 0.18f);

    void Start()
    {
        var go = new GameObject("Dust Motes");
        go.transform.SetParent(transform, false);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.05f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.025f);
        main.startColor = dustColor;
        main.maxParticles = maxParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.002f;

        var emission = ps.emission;
        emission.rateOverTime = emissionRate;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = areaSize;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.08f;
        noise.frequency = 0.3f;

        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        fade.color = g;

        var r = go.GetComponent<ParticleSystemRenderer>();
        if (particleMaterial != null) r.sharedMaterial = particleMaterial;

        ps.Play();
    }
}
