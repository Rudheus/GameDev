using UnityEngine;

// Platform rapuh (gimmick rooftop chase): diinjak player → bergetar sebentar →
// runtuh jatuh. Memaksa pemain terus bergerak — jangan berhenti di atap!
// Taruh di cube platform (layer Ground biar bisa dipijak). Balik utuh saat
// scene di-restart (bukan saat respawn — lubang yang kamu buat tetap ada).
public class CrumblingPlatform : MonoBehaviour
{
    [Tooltip("Jeda dari diinjak sampai runtuh (detik) — waktu pemain buat kabur.")]
    public float crumbleDelay = 0.8f;
    [Tooltip("Getaran peringatan sebelum runtuh (meter).")]
    public float shakeAmount = 0.06f;
    [Tooltip("Puing platform dihapus setelah jatuh segini detik.")]
    public float destroyAfterFall = 4f;

    private bool triggered;
    private float timer;
    private Vector3 originalPosition;

    void Start()
    {
        originalPosition = transform.position;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (triggered) return;
        // Cuma player yang memicu (puing/musuh nggak).
        if (collision.rigidbody == null || !collision.rigidbody.TryGetComponent(out PlayerController _)) return;

        triggered = true;
        timer = crumbleDelay;
    }

    void Update()
    {
        if (!triggered) return;

        timer -= Time.deltaTime;

        if (timer > 0f)
        {
            // Getar peringatan — makin dekat runtuh makin kencang.
            float t = 1f - timer / crumbleDelay;
            transform.position = originalPosition + Random.insideUnitSphere * (shakeAmount * t);
        }
        else
        {
            Fall();
            enabled = false;
        }
    }

    void Fall()
    {
        transform.position = originalPosition;

        // Jatuh kena gravitasi; collider dibiarkan hidup biar puingnya masih nabrak.
        var rb = gameObject.GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = true;

        ImpactParticles.Spawn(transform.position, new Color(0.6f, 0.55f, 0.5f), 20, 2f, 0.16f, 0.7f);
        Destroy(gameObject, destroyAfterFall);
    }
}
