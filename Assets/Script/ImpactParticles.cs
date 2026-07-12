using UnityEngine;

// Helper partikel satu-tembak (burst) yang dibangun via kode — dipakai untuk
// debu tembok hancur, debu mendarat, whoosh dash, dan kilau pickup.
// Panggil: ImpactParticles.Spawn(posisi, warna, jumlah, kecepatan).
public static class ImpactParticles
{
    private static Material particleMaterial;

    public static void Spawn(Vector3 position, Color color, int count = 20, float speed = 3f,
                             float size = 0.15f, float lifetime = 0.6f)
    {
        var go = new GameObject("Particles_Impact");
        go.transform.position = position;

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startColor = color;
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
        main.gravityModifier = 0.5f;

        // Emisi: satu burst di awal, tanpa aliran kontinu.
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;

        // Mengecil sampai hilang di akhir umur — nggak "pop" tiba-tiba.
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = GetMaterial();

        ps.Play();
        Object.Destroy(go, lifetime + 0.5f); // bersih-bersih otomatis
    }

    static Material GetMaterial()
    {
        if (particleMaterial == null)
        {
            // Shader partikel URP; fallback ke Sprites/Default kalau nggak ketemu.
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            particleMaterial = new Material(shader);
        }
        return particleMaterial;
    }
}
