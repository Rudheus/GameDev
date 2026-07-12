using UnityEngine;

// SFX gerakan player: langkah kaki (temponya ngikut kecepatan — jalan pelan,
// sprint rapat), lompat, mendarat, slide, dan dash. Taruh di GameObject player.
// Semua clip opsional — yang kosong cuma di-skip.
[RequireComponent(typeof(PlayerController))]
public class PlayerFootsteps : MonoBehaviour
{
    [Header("Langkah kaki")]
    [Tooltip("Beberapa variasi suara langkah (dipilih acak biar nggak monoton).")]
    public AudioClip[] footstepClips;
    [Tooltip("Jarak tempuh antar langkah (meter). Kecil = langkah makin rapat.")]
    public float strideLength = 1.8f;
    [Range(0f, 1f)] public float footstepVolume = 0.45f;

    [Header("Aksi")]
    public AudioClip jumpClip;
    public AudioClip landClip;
    public AudioClip slideClip;
    public AudioClip dashClip;
    [Range(0f, 1f)] public float actionVolume = 0.6f;

    private PlayerController controller;
    private Rigidbody rb;
    private AudioSource source;

    private float distanceSinceStep;
    private bool wasGrounded = true;
    private bool wasSliding;
    private bool wasDashing;

    void Start()
    {
        controller = GetComponent<PlayerController>();
        rb = GetComponent<Rigidbody>();

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
    }

    void Update()
    {
        bool grounded = controller.IsGrounded;

        // Langkah kaki: akumulasi jarak horizontal saat di darat; tiap strideLength
        // meter bunyikan satu langkah — otomatis pas untuk jalan maupun sprint.
        Vector3 v = rb.linearVelocity;
        v.y = 0f;
        if (grounded && !controller.IsSliding && !controller.IsDashing && v.magnitude > 0.5f)
        {
            distanceSinceStep += v.magnitude * Time.deltaTime;
            if (distanceSinceStep >= strideLength)
            {
                distanceSinceStep = 0f;
                PlayFootstep();
            }
        }
        else
        {
            distanceSinceStep = strideLength * 0.5f; // langkah pertama cepat keluar pas mulai jalan
        }

        // Lompat: baru lepas dari tanah dengan kecepatan ke atas.
        if (wasGrounded && !grounded && rb.linearVelocity.y > 1f)
            PlayClip(jumpClip);

        // Mendarat: baru menyentuh tanah lagi — suara + debu kecil di kaki.
        if (!wasGrounded && grounded)
        {
            PlayClip(landClip);
            ImpactParticles.Spawn(transform.position + controller.groundCheckOffset,
                new Color(0.72f, 0.7f, 0.64f, 0.85f), 12, 1.6f, 0.14f, 0.5f);
        }

        // Slide & dash: bunyikan di frame pertama mulainya (dash + semburan debu).
        if (!wasSliding && controller.IsSliding) PlayClip(slideClip);
        if (!wasDashing && controller.IsDashing)
        {
            PlayClip(dashClip);
            ImpactParticles.Spawn(transform.position + controller.groundCheckOffset,
                new Color(0.85f, 0.85f, 0.9f, 0.7f), 15, 2.2f, 0.12f, 0.45f);
        }

        wasGrounded = grounded;
        wasSliding = controller.IsSliding;
        wasDashing = controller.IsDashing;
    }

    void PlayFootstep()
    {
        if (footstepClips == null || footstepClips.Length == 0) return;
        var clip = footstepClips[Random.Range(0, footstepClips.Length)];
        // Pitch sedikit acak biar tiap langkah nggak terdengar identik.
        source.pitch = Random.Range(0.92f, 1.08f);
        source.PlayOneShot(clip, footstepVolume);
    }

    void PlayClip(AudioClip clip)
    {
        if (clip == null) return;
        source.pitch = 1f;
        source.PlayOneShot(clip, actionVolume);
    }
}
