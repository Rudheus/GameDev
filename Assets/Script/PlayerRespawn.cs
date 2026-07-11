using UnityEngine;

// Respawn teleport sederhana: simpan checkpoint, pindahkan player ke sana saat tertangkap.
// Bukan reload scene — instan, enak buat iterasi, dan tembok breakable yang sudah hancur tetap hancur.
[RequireComponent(typeof(Rigidbody))]
public class PlayerRespawn : MonoBehaviour
{
    private Rigidbody rb;
    private Vector3 checkpointPosition;
    private Quaternion checkpointRotation;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // Checkpoint awal = posisi start player.
        checkpointPosition = transform.position;
        checkpointRotation = transform.rotation;
    }

    // Dipanggil nanti oleh trigger volume saat player melewati checkpoint baru.
    public void SetCheckpoint(Vector3 position, Quaternion rotation)
    {
        checkpointPosition = position;
        checkpointRotation = rotation;
    }

    public void Respawn()
    {
        // Batalkan slide dulu biar collider balik normal di titik spawn.
        if (TryGetComponent(out PlayerController controller)) controller.CancelSlide();

        // Nol-kan velocity dulu biar momentum nggak kebawa ke titik spawn.
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(checkpointPosition, checkpointRotation);

        // Kembalikan semua polisi ke posisi patroli awalnya.
        foreach (var enemy in FindObjectsByType<EnemyChaser>(FindObjectsSortMode.None))
        {
            enemy.ResetToStart();
        }

        // Feedback UI & suara (opsional — aman kalau manager-nya nggak ada di scene).
        if (GameUI.Instance != null) GameUI.Instance.ShowCaught();
        if (AudioManager.Instance != null) AudioManager.Instance.PlayCaught();
    }
}
