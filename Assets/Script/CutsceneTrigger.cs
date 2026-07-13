using UnityEngine;
using UnityEngine.Events;

// Trigger cutscene di lokasi tertentu: player masuk collider → onEnter (sekali saja).
// Wiring khas untuk cutscene tengah-gameplay (di event onEnter, urutannya penting):
//   1. GameUI.FreezeForCutscene   (bekukan game tanpa panel pause)
//   2. VideoCutscene.Play / StoryCutscene.Play
// dan di On Finished cutscene-nya: GameUI.ResumeFromCutscene.
[RequireComponent(typeof(Collider))]
public class CutsceneTrigger : MonoBehaviour
{
    [Tooltip("Dipanggil sekali saat player pertama kali masuk.")]
    public UnityEvent onEnter;

    private bool used;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (used) return;
        if (other.GetComponentInParent<PlayerController>() == null) return;

        used = true;
        onEnter.Invoke();
    }
}
