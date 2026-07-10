using UnityEngine;

// Volume checkpoint: player lewat → titik respawn pindah ke sini.
// Taruh di GameObject dengan collider (Is Trigger otomatis dicentang saat komponen
// ditambahkan). Sekali aktif nggak bisa mundur — lewat checkpoint lama nggak
// nge-reset progress ke belakang.
[RequireComponent(typeof(Collider))]
public class CheckpointTrigger : MonoBehaviour
{
    [Tooltip("Titik respawn persisnya. Kosong = pakai posisi & arah object ini.")]
    public Transform spawnPoint;

    private bool activated;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (activated) return;

        var respawn = other.GetComponentInParent<PlayerRespawn>();
        if (respawn == null) return;

        Transform point = spawnPoint != null ? spawnPoint : transform;
        respawn.SetCheckpoint(point.position, point.rotation);
        activated = true;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Transform point = spawnPoint != null ? spawnPoint : transform;
        Gizmos.DrawWireSphere(point.position, 0.4f);
        Gizmos.DrawRay(point.position, point.forward); // arah hadap saat respawn
    }
}
