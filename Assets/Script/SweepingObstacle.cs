using UnityEngine;

// Rintangan bergerak bolak-balik (gimmick chase): palang/kontainer yang menyapu
// jalur — pemain harus nge-time lewatnya (atau slide di kolongnya kalau tinggi).
// Gerak pakai MovePosition di FixedUpdate biar dorongan ke player dihitung physics.
[RequireComponent(typeof(Rigidbody))]
public class SweepingObstacle : MonoBehaviour
{
    [Tooltip("Arah gerak (lokal ke orientasi object). Contoh (1,0,0) = kiri-kanan.")]
    public Vector3 moveAxis = Vector3.right;
    [Tooltip("Jarak ayunan dari titik awal ke tiap sisi (meter).")]
    public float distance = 3f;
    [Tooltip("Kecepatan ayunan (siklus per detik-ish; 0.5 = pelan, 1.5 = cepat).")]
    public float speed = 0.8f;
    [Tooltip("Geser fase biar beberapa obstacle nggak seirama (0..1).")]
    [Range(0f, 1f)] public float phaseOffset = 0f;

    private Rigidbody rb;
    private Vector3 startPosition;
    private Vector3 worldAxis;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true; // digerakkan script, nggak kena gravitasi/dorongan

        startPosition = transform.position;
        worldAxis = transform.TransformDirection(moveAxis.normalized);
    }

    void FixedUpdate()
    {
        float wave = Mathf.Sin((Time.time * speed + phaseOffset) * Mathf.PI * 2f);
        rb.MovePosition(startPosition + worldAxis * (wave * distance));
    }

    void OnDrawGizmosSelected()
    {
        Vector3 axis = Application.isPlaying ? worldAxis : transform.TransformDirection(moveAxis.normalized);
        Vector3 origin = Application.isPlaying ? startPosition : transform.position;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(origin - axis * distance, origin + axis * distance);
    }
}
