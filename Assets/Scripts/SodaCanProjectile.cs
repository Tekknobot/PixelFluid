using UnityEngine;

namespace PixelOcean
{
    [RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D))]
    public sealed class SodaCanProjectile : MonoBehaviour
    {
        private static AudioClip canThrowClip;
        private static AudioClip sharkHitClip;

        private Vector2 velocity;
        private float gravity = 5.2f;
        private float life = 5f;
        private bool bounced;

        public void Launch(
            Vector2 start,
            SharkLaneSwimmer target,
            Sprite sprite,
            float direction)
        {
            transform.position = start;
            GetComponent<SpriteRenderer>().sprite = sprite;
            transform.localScale = Vector3.one * 0.325f;

            CircleCollider2D canCollider = GetComponent<CircleCollider2D>();
            canCollider.isTrigger = true;
            canCollider.radius = 0.12f;

            LoadSfx();
            PlaySfx(canThrowClip, 0.9f);

            Vector2 aim = target != null
                ? (Vector2)target.transform.position
                : start + Vector2.right * direction * 4f;

            float miss = Random.value < 0.28f
                ? Random.Range(-0.85f, 0.85f)
                : Random.Range(-0.16f, 0.16f);

            aim += new Vector2(
                miss,
                Random.Range(-0.12f, 0.18f));

            float travelTime = Mathf.Clamp(
                Vector2.Distance(start, aim) / 5.5f,
                0.38f,
                0.85f);

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

            velocity.y -= gravity * Time.deltaTime;
            transform.position += (Vector3)(velocity * Time.deltaTime);
            transform.Rotate(0f, 0f, -760f * Time.deltaTime);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            SharkLaneSwimmer shark =
                other.GetComponentInParent<SharkLaneSwimmer>();

            if (shark != null)
            {
                LoadSfx();
                PlaySfx(sharkHitClip, 1f);

                shark.TakeSodaCanHit(transform.position);
                Bounce(shark.transform.position);
                return;
            }

            if (!bounced)
                Bounce(other.transform.position);
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
