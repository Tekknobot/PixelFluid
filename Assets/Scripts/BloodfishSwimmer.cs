using System.Collections;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(InterWaveRenderItem))]
    public sealed class BloodfishSwimmer : MonoBehaviour
    {
        [Header("Formation")]
        [SerializeField, Min(0.1f)] private float formationResponsiveness = 4.2f;
        [SerializeField, Min(0f)] private float weaveStrength = 0.09f;
        [SerializeField, Min(0.1f)] private float weaveFrequency = 2.3f;

        [Header("Combat")]
        [SerializeField, Min(0.1f)] private float attackDistance = 0.72f;
        [SerializeField, Min(0.1f)] private float contactRadius = 0.13f;
        [SerializeField, Min(0.1f)] private float damageCooldown = 1.15f;
        [SerializeField, Min(1)] private int hitsToDefeat = 1;

        [Header("Animation")]
        [SerializeField, Min(1f)] private float moveFramesPerSecond = 12f;
        [SerializeField, Min(1f)] private float attackFramesPerSecond = 18f;

        private SpriteRenderer spriteRenderer;
        private InterWaveRenderItem renderItem;
        private Rigidbody2D body;
        private BloodfishSchoolController school;
        private Sprite[] moveFrames;
        private Sprite[] attackFrames;
        private Vector2 formationOffset;
        private float phase;
        private float animationTime;
        private float nextDamageTime;
        private int hitCount;
        private bool initialised;
        private bool removalReported;
        private Color baseColor = Color.white;

        private void OnDestroy()
        {
            if (removalReported) return;
            removalReported = true;
            BloodfishSchoolSpawner owner = school != null
                ? school.GetComponent<BloodfishSchoolSpawner>()
                : GetComponentInParent<BloodfishSchoolSpawner>();
            if (owner != null) owner.NotifyBloodfishRemoved(gameObject);
        }

        public void Initialise(BloodfishSchoolController controller, Vector2 offset, Sprite[] moving, Sprite[] attacking, float animationOffset)
        {
            ResolveReferences();
            school = controller;
            if (school == null || school.WaterLayers.Count < 2) { enabled = false; return; }
            school.Register(this);
            formationOffset = offset;
            moveFrames = moving;
            attackFrames = attacking;
            animationTime = animationOffset;
            phase = Random.Range(0f, Mathf.PI * 2f);
            renderItem.SetLane(school.Lane);
            float x = school.AnchorX + formationOffset.x;
            SetInitialPosition(new Vector2(x, school.GetLaneCentreY(x) + formationOffset.y));
            initialised = true;
        }

        private void Awake() => ResolveReferences();

        private void ResolveReferences()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            renderItem = GetComponent<InterWaveRenderItem>();
            if (spriteRenderer != null) baseColor = spriteRenderer.color;
            body = GetComponent<Rigidbody2D>();
            if (body == null) body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            CircleCollider2D col = GetComponent<CircleCollider2D>();
            if (col == null) col = gameObject.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = contactRadius;
        }

        private void Update()
        {
            if (spriteRenderer == null || school == null) return;
            bool attacking = school.IsHunting && school.Target != null &&
                Vector2.Distance(transform.position, school.Target.transform.position) <= attackDistance;
            Sprite[] frames = attacking && attackFrames != null && attackFrames.Length > 0 ? attackFrames : moveFrames;
            if (frames == null || frames.Length == 0) return;
            animationTime += Time.deltaTime * (attacking ? attackFramesPerSecond : moveFramesPerSecond);
            spriteRenderer.sprite = frames[Mathf.FloorToInt(animationTime) % frames.Length];
        }

        private void FixedUpdate()
        {
            if (!initialised || school == null || body == null) return;
            float time = Time.time;
            float desiredX = school.AnchorX + formationOffset.x + Mathf.Sin(time * weaveFrequency + phase) * weaveStrength;
            float desiredY = school.GetLaneCentreY(desiredX) + formationOffset.y + Mathf.Cos(time * weaveFrequency * 0.8f + phase) * weaveStrength;

            if (school.IsHunting && school.Target != null)
            {
                Vector2 toward = (Vector2)school.Target.transform.position - new Vector2(desiredX, desiredY);
                if (toward.sqrMagnitude > 0.01f)
                {
                    Vector2 pull = toward.normalized * Mathf.Clamp01(1.4f / Mathf.Max(0.2f, toward.magnitude)) * 0.22f;
                    desiredX += pull.x;
                    desiredY += pull.y;
                }
            }

            float follow = 1f - Mathf.Exp(-formationResponsiveness * Time.fixedDeltaTime);
            SetPosition(Vector2.Lerp(body.position, new Vector2(desiredX, desiredY), follow));
            spriteRenderer.flipX = school.Direction < 0f;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (Time.time < nextDamageTime || other == null) return;
            TinyWaveSurfer surfer = other.GetComponentInParent<TinyWaveSurfer>();
            if (surfer == null || surfer.IsDead || surfer.HasObstacleClearance) return;
            if (Vector2.Distance(transform.position, surfer.transform.position) > contactRadius + 0.28f) return;
            if (surfer.TakeSharkHit(transform.position)) nextDamageTime = Time.time + damageCooldown;
        }

        public void TakeThrownItemHit(Vector2 hitPosition)
        {
            hitCount++;
            if (hitCount >= hitsToDefeat) { Destroy(gameObject); return; }
            StartCoroutine(HitFlash());
        }

        private IEnumerator HitFlash()
        {
            if (spriteRenderer == null) yield break;
            spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(0.12f);
            if (spriteRenderer != null) spriteRenderer.color = baseColor;
        }

        public void ApplySectionShift(float horizontalDistance)
        {
            if (Mathf.Abs(horizontalDistance) <= Mathf.Epsilon) return;
            Vector2 shifted = new(transform.position.x + horizontalDistance, transform.position.y);
            transform.position = new Vector3(shifted.x, shifted.y, transform.position.z);
            if (body != null) { body.position = shifted; body.linearVelocity = Vector2.zero; body.angularVelocity = 0f; }
        }

        private void SetInitialPosition(Vector2 position)
        {
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            if (body != null) { body.position = position; body.linearVelocity = Vector2.zero; body.angularVelocity = 0f; }
        }

        private void SetPosition(Vector2 position)
        {
            if (body != null) body.MovePosition(position);
            else transform.position = position;
        }
    }
}
