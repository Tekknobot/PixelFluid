using System;
using System.Linq;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    public sealed class WaterSlashProjectile : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private Sprite[] frames;
        private float speed;
        private float lifetime;
        private float age;
        private float frameRate;
        private int direction;
        private bool finisher;
        private Vector3 baseScale;

        public void Launch(Vector3 position, int travelDirection, bool isFinisher,
            float projectileSpeed, float projectileLifetime, float animationFps, int sortingOrder,
            Vector2 projectileScale, Color projectileTint)
        {
            direction = travelDirection >= 0 ? 1 : -1;
            finisher = isFinisher;
            speed = Mathf.Max(0.1f, projectileSpeed);
            lifetime = Mathf.Max(0.1f, projectileLifetime);
            frameRate = Mathf.Max(1f, animationFps);
            transform.position = position;

            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = sortingOrder;
            spriteRenderer.flipX = direction < 0;
            spriteRenderer.color = projectileTint;
            frames = Resources.LoadAll<Sprite>("VFX/water_slash")
                .OrderBy(s => FrameNumber(s.name)).ToArray();
            if (frames.Length > 0) spriteRenderer.sprite = frames[0];

            baseScale = new Vector3(
                Mathf.Max(0.05f, projectileScale.x),
                Mathf.Max(0.05f, projectileScale.y),
                1f);
            transform.localScale = baseScale;

            CircleCollider2D hitbox = gameObject.AddComponent<CircleCollider2D>();
            hitbox.isTrigger = true;
            // Collider scales with the transform, so keep a consistent local radius.
            hitbox.radius = finisher ? 0.62f : 0.48f;
            Rigidbody2D body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
        }

        private static int FrameNumber(string value)
        {
            int split = value.LastIndexOf('_');
            return split >= 0 && int.TryParse(value.Substring(split + 1), out int n) ? n : 0;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            transform.position += Vector3.right * (direction * speed * dt);
            if (frames != null && frames.Length > 0)
                spriteRenderer.sprite = frames[Mathf.FloorToInt(age * frameRate) % frames.Length];
            if (age >= lifetime) Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null || other.transform == transform) return;
            Vector2 hit = transform.position;
            bool hitSomething = false;

            if (other.GetComponent<SharkLaneSwimmer>() is { } shark) { shark.TakeSodaCanHit(hit); hitSomething = true; }
            else if (other.GetComponent<GiantSquidLaneSwimmer>() is { } squid) { squid.TakeSodaCanHit(hit); hitSomething = true; }
            else if (other.GetComponent<JellyfishSwimmer>() is { } jelly) { jelly.TakeThrownItemHit(hit); hitSomething = true; }
            else if (other.GetComponent<BloodfishSwimmer>() is { } fish) { fish.TakeThrownItemHit(hit); hitSomething = true; }
            else if (other.GetComponent<StingrayLaneSwimmer>() is { } ray) { ray.TakeSodaCanHit(hit); hitSomething = true; }
            else if (other.GetComponent<RubberDucklingSwimmer>() is { } duckling) { duckling.TakeThrownItemHit(hit); hitSomething = true; }
            else if (other.GetComponent<RubberDuckBossSwimmer>() is { } duckBoss) { duckBoss.TakeThrownItemHit(finisher ? 3 : 1, hit); hitSomething = true; }
            else if (other.GetComponent<GodzillaLaneSwimmer>() is { } godzilla) { godzilla.TakeThrownItemHit(finisher ? 3 : 1, hit); hitSomething = true; }
            else if (other.GetComponent<DayTwoHelicopterMissile>() is { } missile) { missile.Intercept(hit); hitSomething = true; }
            else if (other.GetComponent<DayTwoHelicopterController>() is { } helicopter) { helicopter.TakeThrownItemHit(hit); hitSomething = true; }

            if (hitSomething && !finisher)
                Destroy(gameObject);
        }
    }
}
