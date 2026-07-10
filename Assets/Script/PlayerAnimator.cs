using UnityEngine;

// Jembatan gameplay → animasi: baca state dari PlayerController/Rigidbody,
// kirim parameter ke Animator di model karakter (child visual).
// Taruh di GameObject player (root, yang ada PlayerController-nya).
[RequireComponent(typeof(PlayerController))]
public class PlayerAnimator : MonoBehaviour
{
    [Tooltip("Animator di model karakter (child). Kosongkan untuk auto-cari di children.")]
    public Animator animator;
    [Tooltip("Damping blend Speed biar transisi idle/jalan/lari halus, nggak patah-patah.")]
    public float speedDampTime = 0.1f;

    private PlayerController controller;
    private Rigidbody rb;

    // Hash sekali di depan — lebih murah daripada string tiap frame.
    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int GroundedHash = Animator.StringToHash("IsGrounded");
    static readonly int DashHash = Animator.StringToHash("IsDashing");
    static readonly int SlideHash = Animator.StringToHash("IsSliding");

    void Start()
    {
        controller = GetComponent<PlayerController>();
        rb = GetComponent<Rigidbody>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null)
            Debug.LogWarning("PlayerAnimator: Animator tidak ditemukan di children — model karakter belum jadi child player?", this);
    }

    void Update()
    {
        if (animator == null) return;

        // Speed = laju horizontal aktual (bukan input), jadi blend idle/jalan/lari
        // otomatis benar termasuk saat sprint atau didorong sesuatu.
        Vector3 v = rb.linearVelocity;
        v.y = 0f;
        animator.SetFloat(SpeedHash, v.magnitude, speedDampTime, Time.deltaTime);

        animator.SetBool(GroundedHash, controller.IsGrounded);
        animator.SetBool(DashHash, controller.IsDashing);
        animator.SetBool(SlideHash, controller.IsSliding);
    }
}
