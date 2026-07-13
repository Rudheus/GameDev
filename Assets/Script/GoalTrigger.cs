using UnityEngine;
using UnityEngine.Events;

// Titik tujuan level (pintu keluar penjara / rumah sakit). Player menyentuh → menang.
// Butuh Collider dengan "Is Trigger" dicentang.
[RequireComponent(typeof(Collider))]
public class GoalTrigger : MonoBehaviour
{
    [Tooltip("Nama scene level berikutnya (mis. Level2). KOSONGKAN di level terakhir → menang.")]
    public string nextSceneName;

    [Tooltip("Dipanggil sekali saat player sampai. Hook UI menang / cutscene transisi di sini.")]
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

        // Ada level berikutnya → langsung lanjut (tanpa layar menang).
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            Debug.Log($"Level selesai — lanjut ke {nextSceneName}.");
            onReached.Invoke();
            if (GameUI.Instance != null) GameUI.Instance.LoadLevel(nextSceneName);
            else UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName);
            return;
        }

        // Level terakhir → menang.
        Debug.Log("MENANG! Player sampai di tujuan.");
        if (ScoreManager.Instance != null) ScoreManager.Instance.OnWin();
        if (GameUI.Instance != null) GameUI.Instance.ShowWin();
        if (AudioManager.Instance != null) AudioManager.Instance.PlayWin();

        onReached.Invoke();
    }
}
