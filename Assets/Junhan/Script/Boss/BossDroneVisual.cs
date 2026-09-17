using UnityEngine;

namespace Vampire
{
    public sealed class BossDroneVisual : MonoBehaviour
    {
        private BossHomingMissile missile;
        private BossSpriteSequence art;
        private BossHomingMissileState previous = BossHomingMissileState.Destroyed;
        private float stateStarted;
        private void Start()
        {
            missile = GetComponent<BossHomingMissile>();
            art = BossPatternArt.Replace(gameObject, "DroneFlight", .95f);
        }
        private void LateUpdate()
        {
            if (missile == null || art == null) return;
            var state = missile.CurrentState;
            if (previous != state)
            {
                previous = state; stateStarted = Time.time;
                art.Play(state == BossHomingMissileState.Tracking ? "DroneFlight" : "DroneGround", state == BossHomingMissileState.Embedded ? .76f : .95f, 12, true);
            }
            float height = .22f, angle = 0;
            if (state == BossHomingMissileState.Falling)
            {
                float t = Mathf.Clamp01((Time.time - stateStarted) / Mathf.Max(.001f, missile.VisualFallingDuration));
                height *= 1 - t * t; angle = -720 * t;
            }
            else if (state == BossHomingMissileState.Embedded) height = 0;
            art.transform.position = transform.position + Vector3.up * height;
            art.transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }
}
