using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Empfaenger fuer die Animations-Events der Happy-Harvest-Figur (Schritte, Staub),
    /// damit die Zombies beim Schlurfen leise knirschen und Staub aufwirbeln.
    /// </summary>
    public class ZombieAnimEvents : MonoBehaviour
    {
        static float lastStep;

        public void PlayStepSound()
        {
            // viele Zombies gleichzeitig: Schritte drosseln, sonst wird es ein Rauschen
            if (Time.time - lastStep < 0.12f) return;
            lastStep = Time.time;
            Sfx.Play("crunch", Random.Range(0.45f, 0.6f), 0.18f);
        }

        public void Emit()
        {
            if (Random.value < 0.5f) return;
            Particles.I?.Spawn(transform.position + new Vector3(Random.Range(-0.1f, 0.1f), 0.02f, 0), "square", new Color(0.45f, 0.38f, 0.28f, 0.7f),
                new Vector3(Random.Range(-0.3f, 0.3f), 0.35f, 0), 0.5f, 0.12f, 0f, 0f);
        }
    }
}
