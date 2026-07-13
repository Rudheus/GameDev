using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    [Tooltip("Akselerasi horizontal saat di udara (m/s^2). Grounded = kontrol penuh.")]
    public float airAcceleration = 25f;

    [Header("Sprint / Stamina")]
    public KeyCode sprintKey = KeyCode.LeftShift;
    public float sprintSpeed = 8f;
    public float maxStamina = 100f;
    [Tooltip("Stamina terkuras per detik selama sprint (hanya saat benar-benar bergerak).")]
    public float staminaDrainRate = 25f;
    [Tooltip("Stamina pulih per detik saat tidak sprint.")]
    public float staminaRegenRate = 15f;
    [Tooltip("Setelah stamina habis, sprint terkunci sampai pulih minimal segini (anti kedip on/off di angka 0).")]
    public float staminaRecoverThreshold = 20f;

    [Header("Jump")]
    public float jumpForce = 7f;
    public KeyCode jumpKey = KeyCode.Space;
    [Tooltip("Masih boleh jump sesaat setelah lepas dari platform.")]
    public float coyoteTime = 0.1f;
    [Tooltip("Input jump sesaat sebelum mendarat tetap dieksekusi.")]
    public float jumpBufferTime = 0.1f;

    [Header("Dash")]
    public KeyCode dashKey = KeyCode.Mouse1;
    public float dashSpeed = 20f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;
    [Tooltip("Tembok masih bisa didobrak kalau kena sesaat setelah dash berakhir (detik). Bikin dobrak lebih forgiving terhadap timing.")]
    public float dashCarveGrace = 0.15f;

    [Header("Slide")]
    public KeyCode slideKey = KeyCode.LeftControl;
    [Tooltip("Kecepatan awal slide (harus > sprintSpeed biar kerasa meluncur).")]
    public float slideSpeed = 10f;
    [Tooltip("Kecepatan di akhir slide (melambat ke sini sepanjang durasi).")]
    public float slideEndSpeed = 4f;
    public float slideDuration = 0.8f;
    public float slideCooldown = 0.5f;
    [Tooltip("Tinggi capsule collider saat slide. Balik normal saat berdiri.")]
    public float slideColliderHeight = 1f;
    [Tooltip("Layer yang menghalangi berdiri (cek atas kepala saat mau bangun). Set: Ground + Obstacle.")]
    public LayerMask standBlockMask;

    [Header("Ground Check")]
    public Vector3 groundCheckOffset = new Vector3(0f, -0.5f, 0f);
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;

    private Rigidbody rb;
    private Transform cam;
    private bool isGrounded;

    private float coyoteTimer;
    private float jumpBufferTimer;
    private float dashTimer;
    private float dashCooldownTimer;
    private float dashGraceTimer;
    private Vector3 dashDirection;

    private float stamina;
    private bool exhausted;   // stamina habis → sprint terkunci sampai pulih ke threshold
    private bool sprinting;

    private bool sliding;
    private float slideTimer;
    private float slideCooldownTimer;
    private Vector3 slideDirection;
    private CapsuleCollider capsule;
    private float standingHeight;
    private Vector3 standingCenter;

    public bool IsDashing => dashTimer > 0f;
    public bool IsSprinting => sprinting;
    public bool IsGrounded => isGrounded;
    public bool IsSliding => sliding;
    // Buat UI: 0..1
    public float Stamina01 => stamina / maxStamina;
    public bool IsExhausted => exhausted;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        cam = Camera.main.transform;
        stamina = maxStamina;

        capsule = GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            standingHeight = capsule.height;
            standingCenter = capsule.center;

            // Tanpa gesekan: nempel ke sisi tembok saat lompat bikin player "menggantung"
            // (friction menahan gravitasi). Gerakan darat aman — velocity di-set langsung.
            capsule.material = new PhysicsMaterial("PlayerFrictionless")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
        }
        else
        {
            Debug.LogWarning("PlayerController: CapsuleCollider tidak ditemukan — slide tidak akan memendekkan collider.", this);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        isGrounded = CheckGrounded();

        // Coyote: timer di-reset selama grounded, jalan mundur begitu lepas dari platform.
        if (isGrounded) coyoteTimer = coyoteTime;
        else coyoteTimer -= Time.deltaTime;

        // Buffer: simpan input jump, konsumsi begitu (masih) boleh jump.
        jumpBufferTimer -= Time.deltaTime;
        if (Input.GetKeyDown(jumpKey)) jumpBufferTimer = jumpBufferTime;

        // Slide-jump dibolehkan (batalkan slide → lompat), tapi cuma kalau ada ruang berdiri.
        if (jumpBufferTimer > 0f && coyoteTimer > 0f && !IsDashing && (!sliding || CanStandUp()))
        {
            if (sliding) EndSlide();
            Jump();
            jumpBufferTimer = 0f;
            coyoteTimer = 0f; // biar nggak double jump dari sisa coyote
        }

        dashCooldownTimer -= Time.deltaTime;
        if (Input.GetKeyDown(dashKey) && dashCooldownTimer <= 0f && !IsDashing && !sliding)
        {
            StartDash();
        }

        // Slide cuma dari sprint di darat (sesuai GDD: merunduk saat lari cepat).
        slideCooldownTimer -= Time.deltaTime;
        if (Input.GetKeyDown(slideKey) && sprinting && isGrounded
            && !sliding && !IsDashing && slideCooldownTimer <= 0f)
        {
            StartSlide();
        }

        UpdateSprint();
    }

    void UpdateSprint()
    {
        // Sprint cuma aktif kalau: tombol ditahan, ada input gerak, belum exhausted, dan bukan lagi dash/slide.
        bool wantSprint = Input.GetKey(sprintKey)
                          && MoveDirection().sqrMagnitude > 0.01f
                          && !exhausted
                          && !IsDashing
                          && !sliding;

        sprinting = wantSprint && stamina > 0f;

        if (sprinting)
        {
            stamina -= staminaDrainRate * Time.deltaTime;
            if (stamina <= 0f)
            {
                stamina = 0f;
                exhausted = true; // kehabisan napas — kunci sprint sampai pulih
                sprinting = false;
            }
        }
        else
        {
            stamina = Mathf.Min(stamina + staminaRegenRate * Time.deltaTime, maxStamina);
            if (exhausted && stamina >= staminaRecoverThreshold) exhausted = false;
        }
    }

    void Jump()
    {
        // Zero-kan Y velocity dulu biar tinggi jump konsisten (nggak ke-counter sisa fall/rise speed).
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    void StartSlide()
    {
        // Meluncur searah gerak sekarang; fallback ke arah hadap.
        Vector3 dir = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        if (dir.sqrMagnitude > 0.01f) dir.Normalize();
        else { dir = transform.forward; dir.y = 0f; dir.Normalize(); }

        slideDirection = dir;
        sliding = true;
        slideTimer = slideDuration;

        // Pendekkan capsule; dasar collider dijaga tetap (biar nggak nyangkut/ngambang).
        if (capsule != null)
        {
            capsule.height = slideColliderHeight;
            capsule.center = new Vector3(
                standingCenter.x,
                standingCenter.y - (standingHeight - slideColliderHeight) * 0.5f,
                standingCenter.z);
        }
    }

    void EndSlide()
    {
        sliding = false;
        slideCooldownTimer = slideCooldown;

        if (capsule != null)
        {
            capsule.height = standingHeight;
            capsule.center = standingCenter;
        }
    }

    // Dipanggil dari luar (mis. respawn) untuk memaksa berdiri tanpa cek apa pun.
    public void CancelSlide()
    {
        if (sliding) EndSlide();
    }

    // Ruang berdiri kosong? Cek sphere di posisi kepala saat berdiri penuh.
    bool CanStandUp()
    {
        if (capsule == null || standBlockMask.value == 0) return true;

        float radius = capsule.radius * 0.9f;
        Vector3 head = transform.position + standingCenter
                       + Vector3.up * (standingHeight * 0.5f - radius);
        return !Physics.CheckSphere(head, radius, standBlockMask, QueryTriggerInteraction.Ignore);
    }

    void StartDash()
    {
        // Dash ke arah input; kalau diam, ke arah hadap karakter.
        Vector3 dir = MoveDirection();
        if (dir.sqrMagnitude < 0.01f)
        {
            dir = transform.forward;
            dir.y = 0f;
            dir.Normalize();
        }

        dashDirection = dir;
        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;
    }

    // Arah input WASD relatif kamera, di-flatten ke bidang XZ (normalized, atau zero kalau nggak ada input).
    Vector3 MoveDirection()
{
    float moveX = Input.GetAxis("Horizontal");
    float moveZ = Input.GetAxis("Vertical");

    // FIX: Pastikan memori kamera selalu mengambil yang aktif saat ini jika cam bernilai null
    if (cam == null)
    {
        if (Camera.main != null) cam = Camera.main.transform;
        else return Vector3.zero; // Cari aman jika tidak ada kamera di scene
    }

    // Ambil arah depan dan kanan kamera secara dinamis
    Vector3 camForward = cam.forward; 
    Vector3 camRight   = cam.right;   

    // Flatten / Nol-kan sumbu Y agar gerakan tetap di bidang datar (Horizontal)
    camForward.y = 0f; 
    camRight.y   = 0f; 

    // Satukan kembali arahnya setelah Y di-nol-kan
    camForward.Normalize();
    camRight.Normalize();

    // Hitung arah gerak berdasarkan input WASD dan posisi kamera terbaru
    Vector3 dir = camForward * moveZ + camRight * moveX;
    
    return dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.zero;
}

    bool CheckGrounded()
    {
        return Physics.CheckSphere(transform.position + groundCheckOffset, groundCheckRadius, groundLayer, QueryTriggerInteraction.Ignore);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Application.isPlaying && isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position + groundCheckOffset, groundCheckRadius);
    }

    void OnCollisionEnter(Collision collision)
    {
        // Dobrak boleh selama dash ATAU sesaat setelahnya (grace window) biar timing forgiving.
        if (!IsDashing && dashGraceTimer <= 0f) return;

        // Collider yang kena adalah voxel child dari tembok — komponennya di root.
        var wall = collision.collider.GetComponentInParent<BreakableWall>();
        if (wall != null)
        {
            // Lubangi tembok di titik tabrak searah dash: voxel di jalur dash terlempar
            // jadi puing (terowongan tembus), sisa tembok tetap berdiri.
            wall.Carve(collision.GetContact(0).point, dashDirection * dashSpeed);
        }
        else if (collision.gameObject.CompareTag("Breakable"))
        {
            Destroy(collision.gameObject);
        }
    }

    void FixedUpdate()
    {
        if (IsDashing)
        {
            dashTimer -= Time.fixedDeltaTime;

            // Selama dash, grace di-top up terus; mulai berkurang begitu dash habis.
            dashGraceTimer = dashCarveGrace;

            // Y velocity di-nol-kan supaya dash lurus/datar, termasuk saat di udara.
            rb.linearVelocity = dashDirection * dashSpeed;

            FaceToward(dashDirection);
            return; // input movement diabaikan selama dash
        }

        // Window kecil setelah dash: dobrak tembok masih dibolehkan (lihat OnCollisionEnter).
        dashGraceTimer -= Time.fixedDeltaTime;

        if (sliding)
        {
            slideTimer -= Time.fixedDeltaTime;

            // Meluncur lurus searah awal slide, melambat dari slideSpeed ke slideEndSpeed.
            float t = Mathf.Clamp01(1f - slideTimer / slideDuration);
            float speed = Mathf.Lerp(slideSpeed, slideEndSpeed, t);
            rb.linearVelocity = new Vector3(slideDirection.x * speed, rb.linearVelocity.y, slideDirection.z * speed);
            FaceToward(slideDirection);

            // Durasi habis → bangun, TAPI cuma kalau atas kepala kosong.
            // Kalau masih di bawah rintangan, terus meluncur pelan sampai lolos.
            if (slideTimer <= 0f && CanStandUp()) EndSlide();
            return; // input movement diabaikan selama slide
        }

        Vector3 desired = MoveDirection() * (sprinting ? sprintSpeed : moveSpeed);

        if (isGrounded)
        {
            rb.linearVelocity = new Vector3(desired.x, rb.linearVelocity.y, desired.z);
        }
        else
        {
            // Air control terbatas: dorong velocity horizontal ke arah input, jangan langsung set.
            Vector3 horizontal = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            horizontal = Vector3.MoveTowards(horizontal, desired, airAcceleration * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector3(horizontal.x, rb.linearVelocity.y, horizontal.z);
        }

        if (desired.sqrMagnitude > 0.01f)
        {
            FaceToward(desired);
        }
    }

    void FaceToward(Vector3 direction)
    {
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
    }
}
