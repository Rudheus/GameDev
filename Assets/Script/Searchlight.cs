using UnityEngine;

// Lampu sorot penjaga (obstacle Level 1 di GDD): berputar bolak-balik / keliling,
// dan kalau sinarnya kena player → semua polisi dalam alertRadius datang menyelidiki
// posisi player (EnemyChaser.Alert) — bukan langsung ngejar, jadi masih bisa kabur.
//
// Setup: GameObject kosong sebagai pivot (taruh di atas menara) → child Spot Light
// yang menghadap serong ke bawah → komponen ini di pivot-nya.
public class Searchlight : MonoBehaviour
{
    [Header("Rotasi")]
    [Tooltip("Kecepatan putar (derajat/detik).")]
    public float rotateSpeed = 30f;
    [Tooltip("Sudut ayunan bolak-balik dari arah awal (derajat). 0 = berputar penuh terus-menerus.")]
    public float swingAngle = 60f;

    [Header("Deteksi")]
    [Tooltip("Spot Light-nya. Kosongkan untuk auto-cari di children. Jangkauan & lebar deteksi ngikut Range + Spot Angle lampu ini.")]
    public Light spot;
    [Tooltip("Layer penghalang sinar (tembok). Player di balik tembok tidak terdeteksi.")]
    public LayerMask obstacleMask;
    [Tooltip("Polisi dalam radius ini dipanggil menyelidiki saat player tertangkap sinar.")]
    public float alertRadius = 25f;
    [Tooltip("Jeda antar laporan (detik) biar polisi nggak di-spam tiap frame.")]
    public float alertCooldown = 2f;

    private Transform playerT;
    private EnemyChaser[] enemies;
    private Quaternion baseRotation;
    private float cooldownTimer;

    void Start()
    {
        if (spot == null) spot = GetComponentInChildren<Light>();
        if (spot == null)
            Debug.LogWarning("Searchlight: Spot Light tidak ditemukan di children.", this);

        var player = FindFirstObjectByType<PlayerController>();
        if (player != null) playerT = player.transform;

        enemies = FindObjectsByType<EnemyChaser>(FindObjectsSortMode.None);
        baseRotation = transform.rotation;
    }

    void Update()
    {
        // Putar pivot: ayun sinus bolak-balik, atau spin penuh kalau swingAngle 0.
        if (swingAngle > 0f)
        {
            float yaw = Mathf.Sin(Time.time * rotateSpeed * Mathf.Deg2Rad) * swingAngle;
            transform.rotation = baseRotation * Quaternion.Euler(0f, yaw, 0f);
        }
        else
        {
            transform.Rotate(0f, rotateSpeed * Time.deltaTime, 0f, Space.World);
        }

        cooldownTimer -= Time.deltaTime;
        if (cooldownTimer <= 0f && PlayerInBeam())
        {
            cooldownTimer = alertCooldown;
            AlertNearbyPolice();
        }
    }

    // Player kena sinar? Cek kerucut lampu: jarak < range, sudut < spotAngle/2, LOS bebas.
    bool PlayerInBeam()
    {
        if (spot == null || playerT == null) return false;

        // Sasar badan, bukan kaki, biar raycast nggak keserempet lantai.
        Vector3 targetPoint = playerT.position + Vector3.up * 1f;
        Vector3 toPlayer = targetPoint - spot.transform.position;
        float dist = toPlayer.magnitude;

        if (dist > spot.range) return false;
        if (Vector3.Angle(spot.transform.forward, toPlayer) > spot.spotAngle * 0.5f) return false;

        if (obstacleMask.value != 0 &&
            Physics.Raycast(spot.transform.position, toPlayer / dist, dist, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            return false; // terhalang tembok/atap
        }

        return true;
    }

    void AlertNearbyPolice()
    {
        foreach (var e in enemies)
        {
            if (e == null) continue;
            if ((e.transform.position - playerT.position).sqrMagnitude <= alertRadius * alertRadius)
            {
                e.Alert(playerT.position);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, alertRadius);

        var l = spot != null ? spot : GetComponentInChildren<Light>();
        if (l != null)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawRay(l.transform.position, l.transform.forward * l.range);
        }
    }
}
