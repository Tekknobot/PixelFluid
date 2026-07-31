using UnityEngine;

namespace PixelOcean
{
    [RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(Rigidbody2D))]
    public sealed class SodaCanProjectile : MonoBehaviour
    {
        private static AudioClip canThrowClip;
        private static AudioClip sharkHitClip;

        private Vector2 velocity;
        private float gravity = 5.2f;
        private float life = 5f;
        private bool bounced;
        private Transform lockedTarget;
        private GodzillaLaneSwimmer lockedBoss;

        public void Launch(
            Vector2 start,
            Transform target,
            Sprite sprite,
            float direction,
            bool precisionShot = false)
        {
            transform.position = start;
            lockedTarget = target;
            lockedBoss = target != null
                ? target.GetComponentInParent<GodzillaLaneSwimmer>()
                : null;
            SpriteRenderer renderer = GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;

            // Ocean-item artwork can have different source dimensions. Normalize
            // the visible projectile to a consistent world-space size.
            float largestSide = sprite != null
                ? Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y)
                : 1f;
            float normalizedScale = 0.34f / Mathf.Max(0.01f, largestSide);
            transform.localScale = Vector3.one * normalizedScale;

            CircleCollider2D canCollider = GetComponent<CircleCollider2D>();
            canCollider.isTrigger = true;
            canCollider.radius = largestSide * 0.38f;

            Rigidbody2D body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            LoadSfx();
            PlaySfx(canThrowClip, 0.9f);

            Vector2 aim = target != null
                ? (Vector2)target.position
                : start + Vector2.right * direction * 4f;

            // Throws have no random miss spread. Once a target is selected, the
            // item is aimed directly at it.

            float shotSpeed = precisionShot ? 8.5f : 5.5f;
            float travelTime = Mathf.Clamp(
                Vector2.Distance(start, aim) / shotSpeed,
                precisionShot ? 0.32f : 0.38f,
                precisionShot ? 1.35f : 0.85f);

            velocity = new Vector2(
                (aim.x - start.x) / travelTime,
                (aim.y - start.y + 0.5f * gravity * travelTime * travelTime) /
                travelTime);
        }

        private void Update()
        {
            life -= Time.deltaTime;

            if (life <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            // Gently correct toward the locked moving target. This preserves the
            // thrown arc while preventing a valid target from escaping because it
            // changed direction after launch.
            if (!bounced && lockedTarget != null)
            {
                Vector2 toTarget = (Vector2)lockedTarget.position - (Vector2)transform.position;
                if (toTarget.sqrMagnitude <= 0.16f)
                {
                    HitTarget(lockedTarget);
                    return;
                }

                Vector2 desiredVelocity = toTarget.normalized * Mathf.Max(5.5f, velocity.magnitude);
                velocity = Vector2.Lerp(velocity, desiredVelocity,
                    1f - Mathf.Exp(-4.5f * Time.deltaTime));
            }

            velocity.y -= gravity * Time.deltaTime;
            transform.position += (Vector3)(velocity * Time.deltaTime);
            transform.Rotate(0f, 0f, -760f * Time.deltaTime);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (bounced || other == null)
                return;

            // Floating collectibles are triggers too, but they must never block,
            // deflect, collect, or consume a thrown item travelling through them.
            if (other.GetComponentInParent<OceanItemBehaviour>() != null ||
                other.GetComponentInParent<SodaCanPickup>() != null ||
                other.GetComponentInParent<HeartLaneDrifter>() != null)
            {
                return;
            }

            // When a boss was selected, ordinary sea creatures become transparent
            // to this projectile. The shot remains locked to the boss instead of
            // being intercepted by a shark, squid, or jellyfish in front of it.
            if (lockedBoss != null && !lockedBoss.IsDefeated)
            {
                GodzillaLaneSwimmer hitBoss =
                    other.GetComponentInParent<GodzillaLaneSwimmer>();

                if (hitBoss != lockedBoss)
                    return;
            }

            HitTarget(other.transform);
        }

        private void HitTarget(Transform hitTransform)
        {
            if (bounced || hitTransform == null)
                return;

            GodzillaLaneSwimmer boss =
                hitTransform.GetComponentInParent<GodzillaLaneSwimmer>();

            if (boss != null && !boss.IsDefeated)
            {
                LoadSfx();
                PlaySfx(sharkHitClip, 1f);
                boss.TakeThrownItemHit(1, transform.position);
                Bounce(boss.transform.position);
                return;
            }

            AlienUfoController ufo =
                hitTransform.GetComponentInParent<AlienUfoController>();

            if (ufo != null)
            {
                LoadSfx();
                PlaySfx(sharkHitClip, 1f);
                ufo.TakeSodaCanHit(transform.position);
                Bounce(ufo.transform.position);
                return;
            }

            SharkLaneSwimmer shark =
                hitTransform.GetComponentInParent<SharkLaneSwimmer>();

            if (shark != null)
            {
                LoadSfx();
                PlaySfx(sharkHitClip, 1f);
                shark.TakeSodaCanHit(transform.position);
                Bounce(shark.transform.position);
                return;
            }

            BloodSharkLaneSwimmer bloodShark =
                hitTransform.GetComponentInParent<BloodSharkLaneSwimmer>();

            if (bloodShark != null)
            {
                LoadSfx();
                PlaySfx(sharkHitClip, 1f);
                bloodShark.TakeSodaCanHit(transform.position);
                Bounce(bloodShark.transform.position);
                return;
            }

            TransparentSquidLaneSwimmer transparentSquid =
                hitTransform.GetComponentInParent<TransparentSquidLaneSwimmer>();

            if (transparentSquid != null)
            {
                LoadSfx();
                PlaySfx(sharkHitClip, 1f);
                transparentSquid.TakeSodaCanHit(transform.position);
                Bounce(transparentSquid.transform.position);
                return;
            }

            GiantSquidLaneSwimmer squid =
                hitTransform.GetComponentInParent<GiantSquidLaneSwimmer>();

            if (squid != null)
            {
                LoadSfx();
                PlaySfx(sharkHitClip, 1f);
                squid.TakeSodaCanHit(transform.position);
                Bounce(squid.transform.position);
            }

            JellyfishSwimmer jellyfish =
                hitTransform.GetComponentInParent<JellyfishSwimmer>();

            if (jellyfish != null)
            {
                LoadSfx();
                PlaySfx(sharkHitClip, 0.85f);
                jellyfish.TakeThrownItemHit(transform.position);
                Bounce(jellyfish.transform.position);
                return;
            }

            // Ignore every unrelated collider. Only valid combat targets can
            // interrupt the projectile's trajectory.
        }

        private void Bounce(Vector2 from)
        {
            bounced = true;

            Vector2 away = ((Vector2)transform.position - from).normalized;

            if (away.sqrMagnitude < 0.1f)
                away = Vector2.up;

            velocity = away * 2.2f + Vector2.up * 1.5f;
            gravity = 7f;
            life = Mathf.Min(life, 1.75f);
            GetComponent<CircleCollider2D>().enabled = false;
        }

        private static void LoadSfx()
        {
            if (canThrowClip == null)
            {
                canThrowClip =
                    Resources.Load<AudioClip>("Audio/SFX/can_throw");
            }

            if (sharkHitClip == null)
            {
                sharkHitClip =
                    Resources.Load<AudioClip>("Audio/SFX/shark_hit");
            }
        }

        private static void PlaySfx(AudioClip clip, float volume)
        {
            if (clip == null)
                return;

            GameObject soundObject = new GameObject($"SFX - {clip.name}");
            AudioSource source = soundObject.AddComponent<AudioSource>();

            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.Play();

            Destroy(soundObject, clip.length + 0.1f);
        }
    }
}
