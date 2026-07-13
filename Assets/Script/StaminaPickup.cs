using UnityEngine;

// Isi ulang stamina (gimmick chase Level 2): saat dikejar terus-menerus, sprint =
// nyawa — pickup ini yang bikin pemain bisa terus lari kalau ngambil rutenya.
// Sebar di sepanjang rute pelarian. Pola sama dengan ScorePickup.
[RequireComponent(typeof(Collider))]
public class StaminaPickup : MonoBehaviour
{
    [Tooltip("Berapa stamina yang diisi (max player default 100).")]
    public float amount = 50f;
    public float spinSpeed = 90f;
    public AudioClip pickupClip;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void Update()
    {
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
    }

    void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        player.RefillStamina(amount);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayOneShot(pickupClip);
        ImpactParticles.Spawn(transform.position, new Color(0.35f, 0.85f, 0.4f), 18, 2.5f, 0.12f, 0.5f);

        Destroy(gameObject);
    }
}
