using UnityEngine;

// Polisi/penjaga dengan state machine: Patroli → Deteksi → Kejar → Cari → balik Patroli.
// Tetap pakai physics (Rigidbody, rb.linearVelocity dengan Y dijaga) biar konsisten sama PlayerController.
[RequireComponent(typeof(Rigidbody))]
public class EnemyChaser : MonoBehaviour
{
    enum State { Patrol, Chase, Search }

    [Header("Target")]
    [Tooltip("Kosongkan untuk auto-cari PlayerController di scene saat Start.")]
    public Transform target;

    [Header("Speeds")]
    [Tooltip("Kecepatan jalan saat patroli / mencari.")]
    public float patrolSpeed = 2f;
    [Tooltip("Kecepatan lari saat mengejar.")]
    public float moveSpeed = 3.5f;
    public float rotationSpeed = 10f;

    [Header("Vision")]
    [Tooltip("Jarak pandang maksimum.")]
    public float viewDistance = 15f;
    [Tooltip("Lebar sudut pandang (derajat). 360 = melihat ke segala arah.")]
    [Range(0f, 360f)] public float viewAngle = 200f;
    [Tooltip("Layer tembok yang menghalangi pandangan. Kosong = tembok tidak menghalangi.")]
    public LayerMask obstacleMask;
    [Tooltip("Tinggi 'mata' dari pivot, buat raycast garis pandang.")]
    public float eyeHeight = 1.5f;

    [Header("Chase / Search")]
    [Tooltip("Setelah kehilangan pandangan, berapa lama polisi masih memburu ke titik terakhir sebelum menyerah.")]
    public float loseSightTime = 3f;

    [Header("Catch")]
    [Tooltip("Player dianggap tertangkap begitu jaraknya di bawah nilai ini → respawn.")]
    public float catchDistance = 1.2f;

    [Header("Patrol")]
    [Tooltip("Titik-titik patroli (opsional). Kosong = diam di posisi start sebagai penjaga.")]
    public Transform[] patrolPoints;
    public float waypointTolerance = 0.6f;
    [Tooltip("Berhenti sebentar di tiap titik patroli (detik) sambil menoleh ke titik berikutnya, baru lanjut.")]
    public float waypointWaitTime = 1.5f;

    private Rigidbody rb;
    private PlayerRespawn playerRespawn;

    private Vector3 startPosition;
    private Quaternion startRotation;

    private State state = State.Patrol;
    private int patrolIndex;
    private float waypointWaitTimer;
    private Vector3 lastKnownPosition;
    private float searchTimer;

    public bool IsChasing => state == State.Chase;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        startPosition = transform.position;
        startRotation = transform.rotation;

        if (target == null)
        {
            var player = FindFirstObjectByType<PlayerController>();
            if (player != null) target = player.transform;
        }

        if (target != null) playerRespawn = target.GetComponent<PlayerRespawn>();
    }

    void FixedUpdate()
    {
        if (target == null)
        {
            StopHorizontal();
            return;
        }

        // Tangkap berlaku di state manapun selama cukup dekat.
        float flatDist = FlatDistance(target.position);
        if (flatDist <= catchDistance)
        {
            if (playerRespawn != null) playerRespawn.Respawn();
            StopHorizontal();
            return;
        }

        bool sees = CanSeePlayer(flatDist);

        switch (state)
        {
            case State.Patrol: PatrolUpdate(sees); break;
            case State.Chase:  ChaseUpdate(sees); break;
            case State.Search: SearchUpdate(sees); break;
        }
    }

    void PatrolUpdate(bool sees)
    {
        if (sees) { state = State.Chase; waypointWaitTimer = 0f; return; }

        // Tanpa waypoint: diam sebagai penjaga.
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            StopHorizontal();
            return;
        }

        Transform wp = patrolPoints[patrolIndex];
        if (wp == null) { AdvanceWaypoint(); return; }

        // Sampai di titik: berhenti sebentar sambil menoleh ke titik berikutnya
        // (cone pandangannya ikut menyapu!), baru lanjut jalan.
        if (FlatDistance(wp.position) <= waypointTolerance)
        {
            StopHorizontal();
            waypointWaitTimer += Time.fixedDeltaTime;

            Transform next = patrolPoints[(patrolIndex + 1) % patrolPoints.Length];
            if (next != null)
            {
                Vector3 toNext = next.position - transform.position;
                toNext.y = 0f;
                if (toNext.sqrMagnitude > 0.01f) FaceToward(toNext.normalized);
            }

            if (waypointWaitTimer >= waypointWaitTime)
            {
                waypointWaitTimer = 0f;
                AdvanceWaypoint();
            }
            return;
        }

        MoveToward(wp.position, patrolSpeed);
    }

    void ChaseUpdate(bool sees)
    {
        if (sees)
        {
            lastKnownPosition = target.position; // ingat posisi terakhir terlihat
            MoveToward(target.position, moveSpeed);
        }
        else
        {
            // Hilang dari pandangan → mulai mencari ke titik terakhir.
            state = State.Search;
            searchTimer = loseSightTime;
        }
    }

    void SearchUpdate(bool sees)
    {
        if (sees) { state = State.Chase; return; }

        searchTimer -= Time.fixedDeltaTime;

        // Sampai di titik terakhir atau waktu habis → menyerah, balik patroli.
        if (FlatDistance(lastKnownPosition) <= waypointTolerance || searchTimer <= 0f)
        {
            state = State.Patrol;
            StopHorizontal();
            return;
        }

        MoveToward(lastKnownPosition, patrolSpeed);
    }

    void AdvanceWaypoint()
    {
        if (patrolPoints.Length == 0) return;
        patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
    }

    // Player terlihat kalau: dalam jarak pandang, dalam sudut pandang, dan garis pandang tak terhalang tembok.
    bool CanSeePlayer(float flatDist)
    {
        if (flatDist > viewDistance) return false;

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 toTarget = (target.position + Vector3.up * eyeHeight) - eye;

        Vector3 flatDir = new Vector3(toTarget.x, 0f, toTarget.z);
        if (Vector3.Angle(transform.forward, flatDir) > viewAngle * 0.5f) return false;

        if (obstacleMask.value != 0 &&
            Physics.Raycast(eye, toTarget.normalized, toTarget.magnitude, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            return false; // ada tembok di antara mata dan player
        }

        return true;
    }

    // Dipanggil sistem lain (lampu sorot, dsb.): polisi datang menyelidiki posisi ini.
    // Kalau lagi ngejar, abaikan — target langsung lebih penting daripada laporan.
    public void Alert(Vector3 position)
    {
        if (state == State.Chase) return;
        lastKnownPosition = position;
        searchTimer = loseSightTime;
        state = State.Search;
    }

    // Balik ke posisi & state awal setelah player tertangkap & respawn.
    public void ResetToStart()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(startPosition, startRotation);
        state = State.Patrol;
        patrolIndex = 0;
        waypointWaitTimer = 0f;
    }

    float FlatDistance(Vector3 worldPos)
    {
        Vector3 to = worldPos - transform.position;
        to.y = 0f;
        return to.magnitude;
    }

    void MoveToward(Vector3 destination, float speed)
    {
        Vector3 to = destination - transform.position;
        to.y = 0f;
        float d = to.magnitude;
        if (d < 0.05f) { StopHorizontal(); return; }

        Vector3 dir = to / d;
        rb.linearVelocity = new Vector3(dir.x * speed, rb.linearVelocity.y, dir.z * speed);
        FaceToward(dir);
    }

    // Nol-kan velocity horizontal, Y tetap dibiarkan buat gravitasi.
    void StopHorizontal()
    {
        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
    }

    void FaceToward(Vector3 direction)
    {
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
    }

    void OnDrawGizmosSelected()
    {
        // Warna sesuai state saat play; kuning saat idle di editor.
        Color c = Color.yellow;
        if (Application.isPlaying)
        {
            if (state == State.Chase) c = Color.red;
            else if (state == State.Search) c = new Color(1f, 0.6f, 0f);
        }
        Gizmos.color = c;
        Gizmos.DrawWireSphere(transform.position, viewDistance);

        // Cone pandang.
        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 left  = Quaternion.Euler(0f, -viewAngle * 0.5f, 0f) * transform.forward;
        Vector3 right = Quaternion.Euler(0f,  viewAngle * 0.5f, 0f) * transform.forward;
        Gizmos.DrawLine(eye, eye + left  * viewDistance);
        Gizmos.DrawLine(eye, eye + right * viewDistance);

        // Radius tangkap.
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, catchDistance);

        // Titik patroli.
        if (patrolPoints != null)
        {
            Gizmos.color = Color.cyan;
            foreach (var p in patrolPoints)
                if (p != null) Gizmos.DrawWireSphere(p.position, 0.3f);
        }
    }
}
