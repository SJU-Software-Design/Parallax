using UnityEngine;

namespace ShadeLink
{
    // One nonspatial voice per player, using the already selected UIError145 clip.
    // Called only by the explicit pickup refusal branches, never by hover/aim.
    public sealed class PickupDeniedFeedback : MonoBehaviour
    {
        AudioSource source;
        public int PlayCount { get; private set; }
        public AudioSource Source => source;

        public static void Play(Component player)
        {
            if (!Application.isPlaying || player == null || !player.gameObject.activeInHierarchy) return;
            var feedback = player.GetComponent<PickupDeniedFeedback>();
            if (feedback == null) feedback = player.gameObject.AddComponent<PickupDeniedFeedback>();
            feedback.PlaySelectedCue();
        }

        void PlaySelectedCue()
        {
            if (source == null)
            {
                var clip = Resources.Load<AudioClip>("Preview/UI/Denied");
                if (clip == null) return;
                var node = new GameObject("Pickup denied feedback");
                node.transform.SetParent(transform, false);
                source = node.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0;
                source.dopplerLevel = 0;
                source.bypassReverbZones = true;
                source.priority = 100;
                source.volume = .20f;
                source.clip = clip;
            }
            // Route through the same Sfx mixer as the other in-game effects.
            GameShellSettingsRuntime.Route(source);
            source.Play();
            PlayCount++;
        }
    }
}