using UnityEngine;

namespace Vampire
{
    [DisallowMultipleComponent]
    public sealed class CharacterMotionTrail : MonoBehaviour
    {
        Character owner;
        SpriteRenderer body;
        Sprite motif;
        Vector3 previous;
        float nextEmission;
        int sequence;

        public static void Bind(Character character, SpriteRenderer renderer, ParticleSystem legacyDust)
        {
            if (legacyDust != null)
            {
                legacyDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var emission = legacyDust.emission;
                emission.enabled = false;
                // Also prevents play-on-awake when the character is re-enabled.
                legacyDust.gameObject.SetActive(false);
            }
            var trail = character.GetComponent<CharacterMotionTrail>() ?? character.gameObject.AddComponent<CharacterMotionTrail>();
            trail.owner = character;
            trail.body = renderer;
            trail.motif = MotionVfx.CharacterSprite(character.Blueprint);
            trail.previous = character.transform.position;
            trail.nextEmission = Time.time;
        }

        void OnEnable() { previous = transform.position; nextEmission = Time.time; }

        void LateUpdate()
        {
            var displacement = transform.position - previous;
            previous = transform.position;
            if (owner == null || body == null || motif == null || Time.deltaTime <= 0 ||
                !owner.IsAlive || owner.IsPortalTravelling || owner.IsPortalPoseHeld ||
                !body.enabled || !body.gameObject.activeInHierarchy) return;
            // Actual displacement prevents particles when walking against a wall;
            // reject teleports, and never fill the space between old/new positions.
            if (displacement.sqrMagnitude < .000001f || displacement.sqrMagnitude > 4f ||
                owner.EffectiveMoveDirection.sqrMagnitude < .0001f && !owner.IsDashing ||
                Time.time < nextEmission) return;
            nextEmission = Time.time + .21f;
            var heading = new Vector2(displacement.x, displacement.y).normalized;
            var feet = body.bounds.center + Vector3.down * (body.bounds.extents.y - .07f);
            MotionVfx.PlayTrail(motif, feet - (Vector3)heading * .28f, heading, body, sequence++);
        }
    }
}
