using UnityEngine;
using UnityEngine.Events;

// Titik tujuan level (pintu keluar penjara / rumah sakit). Player menyentuh → menang.
// Butuh Collider dengan "Is Trigger" dicentang.
[RequireComponent(typeof(Collider))]
public class GoalTrigger : MonoBehaviour
{
    [Tooltip("Dipanggil sekali saat player sampai. Hook UI menang / load level berikutnya di sini nanti.")]
    public UnityEvent onReached;

    private bool reached;

    void Reset()
    {
        // Pas komponen ditambahkan di editor, collider-nya langsung di-set jadi trigger.
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (reached) return;

        // Player dikenali lewat komponennya, bukan tag (player belum di-tag).
        if (other.GetComponentInParent<PlayerController>() == null) return;

        reached = true;
        Debug.Log("MENANG! Player sampai di tujuan.");

        // Panel menang, suara, & skor akhir (opsional — aman kalau manager-nya nggak ada di scene).
        if (ScoreManager.Instance != null) ScoreManager.Instance.OnWin();
        if (GameUI.Instance != null) GameUI.Instance.ShowWin();
        if (AudioManager.Instance != null) AudioManager.Instance.PlayWin();

        onReached.Invoke();
    }
}
