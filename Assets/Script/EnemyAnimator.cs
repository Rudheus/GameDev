using UnityEngine;

// Jembatan animasi musuh: baca laju horizontal Rigidbody → parameter Speed.
// Blend Idle/Walk/Run di controller ngikut otomatis (patroli pelan = jalan, kejar = lari).
// Taruh di GameObject musuh (root, yang ada EnemyChaser + Rigidbody-nya).
[RequireComponent(typeof(Rigidbody))]
public class EnemyAnimator : MonoBehaviour
{
    [Tooltip("Animator di model polisi (child). Kosongkan untuk auto-cari di children.")]
    public Animator animator;
    [Tooltip("Damping blend Speed biar transisi idle/jalan/lari halus.")]
    public float speedDampTime = 0.1f;

    private Rigidbody rb;

    static readonly int SpeedHash = Animator.StringToHash("Speed");

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null)
            Debug.LogWarning("EnemyAnimator: Animator tidak ditemukan di children — model polisi belum terpasang?", this);
    }

    void Update()
    {
        if (animator == null) return;

        Vector3 v = rb.linearVelocity;
        v.y = 0f;
        animator.SetFloat(SpeedHash, v.magnitude, speedDampTime, Time.deltaTime);
    }
}
