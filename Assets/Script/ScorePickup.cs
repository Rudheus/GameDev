using UnityEngine;

// Barang pungut (kunci/berkas/uang — temakan lewat modelnya): berputar pelan,
// player menyentuh → skor bertambah, kilau partikel, suara opsional, hilang.
[RequireComponent(typeof(Collider))]
public class ScorePickup : MonoBehaviour
{
    public int value = 250;
    public float spinSpeed = 90f;
    [Tooltip("Suara saat dipungut (opsional).")]
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
        if (other.GetComponentInParent<PlayerController>() == null) return;

        if (ScoreManager.Instance != null) ScoreManager.Instance.AddBonus(value);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayOneShot(pickupClip);
        ImpactParticles.Spawn(transform.position, new Color(1f, 0.85f, 0.3f), 18, 2.5f, 0.12f, 0.5f);

        Destroy(gameObject);
    }
}
