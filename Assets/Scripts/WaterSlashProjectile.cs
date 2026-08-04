using System;
using System.Collections.Generic;
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
        private Color baseColour;
        private CircleCollider2D hitbox;
        private ParticleSystem trailParticles;
        private bool ending;
        private float endingAge;
        private float endingDuration;
        private float endingStartSpeed;
        private readonly HashSet<Collider2D> hitObjects = new();

        private const float NormalImpactDuration = 0.32f;
        private const float LifetimeFadePortion = 0.24f;
        private const float NormalSinkSpeed = 0.48f;
        private const float FinisherSinkSpeed = 0.30f;

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
            baseColour = projectileTint;
            spriteRenderer.color = baseColour;
            frames = Resources.LoadAll<Sprite>("VFX/water_slash_mac")
                .OrderBy(s => FrameNumber(s.name)).ToArray();
            if (frames.Length > 0)
                spriteRenderer.sprite = frames[0];

            baseScale = new Vector3(
                Mathf.Max(0.05f, projectileScale.x),
                Mathf.Max(0.05f, projectileScale.y),
                1f);
            transform.localScale = baseScale;

            hitbox = gameObject.AddComponent<CircleCollider2D>();
            hitbox.isTrigger = true;
            hitbox.radius = finisher ? 0.62f : 0.48f;

            Rigidbody2D body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;

            CreateTrailParticles(sortingOrder - 1);
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

            if (ending)
            {
                UpdateImpactEnding(dt);
                return;
            }

            transform.position += Vector3.right * (direction * speed * dt);

            if (frames != null && frames.Length > 0)
                spriteRenderer.sprite = frames[Mathf.FloorToInt(age * frameRate) % frames.Length];

            // During the final part of its lifetime, the slash loses speed,
            // settles toward the water, expands slightly and fades naturally.
            float fadeStart = lifetime * (1f - LifetimeFadePortion);
            if (age >= fadeStart)
            {
                float fade01 = Mathf.InverseLerp(fadeStart, lifetime, age);
                float eased = fade01 * fade01 * (3f - 2f * fade01);
                speed = Mathf.Lerp(speed, 0f, eased * dt * 8f);
                transform.position += Vector3.down * ((finisher ? FinisherSinkSpeed : NormalSinkSpeed) * dt * eased);
                transform.localScale = Vector3.Lerp(baseScale, baseScale * (finisher ? 1.16f : 1.10f), eased);

                Color colour = baseColour;
                colour.a *= 1f - eased;
                spriteRenderer.color = colour;

                if (trailParticles != null)
                {
                    ParticleSystem.EmissionModule emission = trailParticles.emission;
                    emission.rateOverTime = Mathf.Lerp(finisher ? 42f : 22f, 0f, eased);
                }
            }

            if (age >= lifetime)
            {
                CreateImpactBurst(transform.position, finisher, baseColour,
                    spriteRenderer != null ? spriteRenderer.sortingOrder + 1 : 20,
                    finisher ? 0.72f : 0.42f);
                Destroy(gameObject);
            }
        }

        private void BeginImpactEnding()
        {
            if (ending)
                return;

            ending = true;
            endingAge = 0f;
            endingDuration = NormalImpactDuration;
            endingStartSpeed = speed;
            if (hitbox != null)
                hitbox.enabled = false;
            speed = 0f;

            if (trailParticles != null)
            {
                ParticleSystem.EmissionModule emission = trailParticles.emission;
                emission.enabled = false;
            }
        }

        private void UpdateImpactEnding(float dt)
        {
            endingAge += dt;
            float t = Mathf.Clamp01(endingAge / Mathf.Max(0.01f, endingDuration));
            float easeOut = 1f - Mathf.Pow(1f - t, 3f);

            // Keep a tiny amount of carry-through so the impact never freezes flat.
            transform.position += Vector3.right * (direction * endingStartSpeed * 0.12f * dt * (1f - t));
            transform.position += Vector3.down * (NormalSinkSpeed * dt * (0.3f + t));
            transform.localScale = Vector3.Lerp(baseScale * 1.12f, baseScale * 1.46f, easeOut);

            Color colour = Color.Lerp(Color.white, baseColour, Mathf.Clamp01(t * 2.5f));
            colour.a = 1f - easeOut;
            spriteRenderer.color = colour;

            if (endingAge >= endingDuration)
                Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null || other.transform == transform || ending)
                return;

            if (finisher && !hitObjects.Add(other))
                return;

            Vector2 hit = transform.position;
            bool hitSomething = false;

            if (other.GetComponent<SharkLaneSwimmer>() is { } shark) { shark.TakeSodaCanHit(hit); hitSomething = true; }
            else if (other.GetComponent<GiantSquidLaneSwimmer>() is { } squid) { squid.TakeSodaCanHit(hit); hitSomething = true; }
            else if (other.GetComponent<JellyfishSwimmer>() is { } jelly) { jelly.TakeThrownItemHit(hit); hitSomething = true; }
            else if (other.GetComponent<BloodfishSwimmer>() is { } fish) { fish.TakeThrownItemHit(hit); hitSomething = true; }
            else if (other.GetComponent<StingrayLaneSwimmer>() is { } ray) { ray.TakeSodaCanHit(hit); hitSomething = true; }
            else if (other.GetComponentInParent<GodzillaSkullSwimmer>() is { CanBeHit: true } skull)
            {
                hitSomething = skull.TakeThrownItemHit(hit);
            }
            else if (other.GetComponent<RubberDucklingSwimmer>() is { } duckling) { duckling.TakeThrownItemHit(hit); hitSomething = true; }
            else if (other.GetComponent<RubberDuckBossSwimmer>() is { } duckBoss) { duckBoss.TakeThrownItemHit(finisher ? 3 : 1, hit); hitSomething = true; }
            else if (other.GetComponent<GodzillaLaneSwimmer>() is { } godzilla) { godzilla.TakeThrownItemHit(finisher ? 3 : 1, hit); hitSomething = true; }
            else if (other.GetComponent<DayTwoHelicopterMissile>() is { } missile) { missile.Intercept(hit); hitSomething = true; }
            else if (other.GetComponent<DayTwoHelicopterController>() is { } helicopter) { helicopter.TakeThrownItemHit(hit); hitSomething = true; }

            if (!hitSomething)
                return;

            CreateImpactBurst(transform.position, finisher, baseColour,
                spriteRenderer != null ? spriteRenderer.sortingOrder + 2 : 20,
                finisher ? 1f : 0.62f);

            // White hit flash. The finisher keeps travelling; an ordinary slash
            // blooms, sinks and fades instead of vanishing immediately.
            if (spriteRenderer != null)
                spriteRenderer.color = Color.white;

            if (!finisher)
                BeginImpactEnding();
        }

        private void CreateTrailParticles(int sortingOrder)
        {
            GameObject trailObject = new GameObject(finisher ? "Flow Finisher Foam Trail" : "Water Slash Foam Trail");
            trailObject.transform.SetParent(transform, false);
            trailObject.transform.localPosition = new Vector3(-direction * 0.20f, 0f, 0.01f);

            trailParticles = trailObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = trailParticles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = finisher ? new ParticleSystem.MinMaxCurve(0.18f, 0.42f) : new ParticleSystem.MinMaxCurve(0.14f, 0.30f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, finisher ? 0.55f : 0.34f);
            main.startSize = finisher ? new ParticleSystem.MinMaxCurve(0.025f, 0.095f) : new ParticleSystem.MinMaxCurve(0.018f, 0.060f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, baseColour);
            main.gravityModifier = 0.12f;
            main.maxParticles = finisher ? 140 : 70;

            ParticleSystem.EmissionModule emission = trailParticles.emission;
            emission.rateOverTime = finisher ? 42f : 22f;

            ParticleSystem.ShapeModule shape = trailParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = finisher ? new Vector3(0.18f, 0.26f, 0.01f) : new Vector3(0.12f, 0.16f, 0.01f);

            ParticleSystem.VelocityOverLifetimeModule velocity = trailParticles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;

            velocity.x = -direction * (finisher ? 0.45f : 0.25f);
            velocity.y = 0.12f;

            ParticleSystem.ColorOverLifetimeModule colour = trailParticles.colorOverLifetime;
            colour.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(baseColour, 1f) },
                new[] { new GradientAlphaKey(0.90f, 0f), new GradientAlphaKey(0.45f, 0.55f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;

            ParticleSystem.SizeOverLifetimeModule size = trailParticles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.55f, 0.72f), new Keyframe(1f, 0f)));

            ConfigureParticleRenderer(trailObject.GetComponent<ParticleSystemRenderer>(), sortingOrder);
            trailParticles.Play();
        }

        private static void CreateImpactBurst(Vector3 position, bool isFinisher, Color tint,
            int sortingOrder, float scale)
        {
            GameObject burstObject = new GameObject(isFinisher ? "Flow Finisher Impact Burst" : "Water Slash Impact Burst");
            burstObject.transform.position = position;

            ParticleSystem particles = burstObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = isFinisher ? new ParticleSystem.MinMaxCurve(0.30f, 0.75f) : new ParticleSystem.MinMaxCurve(0.22f, 0.55f);
            main.startSpeed = isFinisher ? new ParticleSystem.MinMaxCurve(0.55f, 2.30f) : new ParticleSystem.MinMaxCurve(0.35f, 1.35f);
            main.startSize = isFinisher ? new ParticleSystem.MinMaxCurve(0.045f, 0.20f) : new ParticleSystem.MinMaxCurve(0.030f, 0.115f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, tint);
            main.gravityModifier = isFinisher ? 0.30f : 0.22f;
            main.maxParticles = isFinisher ? 180 : 90;
            main.stopAction = ParticleSystemStopAction.Destroy;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = (isFinisher ? 0.34f : 0.20f) * Mathf.Max(0.25f, scale);

            ParticleSystem.ColorOverLifetimeModule colour = particles.colorOverLifetime;
            colour.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(tint, 0.45f), new GradientColorKey(tint * 0.72f, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.82f, 0.25f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;

            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 0.45f), new Keyframe(0.16f, 1.25f), new Keyframe(1f, 0f)));

            ParticleSystem.RotationOverLifetimeModule rotation = particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-4f, 4f);

            ConfigureParticleRenderer(burstObject.GetComponent<ParticleSystemRenderer>(), sortingOrder);

            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
            int count = isFinisher ? 52 : 28;
            for (int i = 0; i < count; i++)
            {
                float angle = UnityEngine.Random.Range(18f, 162f) * Mathf.Deg2Rad;
                if (UnityEngine.Random.value < 0.42f)
                    angle += Mathf.PI;
                float particleSpeed = UnityEngine.Random.Range(isFinisher ? 0.55f : 0.35f, isFinisher ? 2.30f : 1.35f) * Mathf.Max(0.45f, scale);
                emit.velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * particleSpeed;
                emit.startLifetime = UnityEngine.Random.Range(isFinisher ? 0.30f : 0.22f, isFinisher ? 0.75f : 0.55f);
                emit.startSize = UnityEngine.Random.Range(isFinisher ? 0.045f : 0.030f, isFinisher ? 0.20f : 0.115f) * Mathf.Max(0.55f, scale);
                emit.startColor = Color.Lerp(tint, Color.white, UnityEngine.Random.Range(0.45f, 1f));
                particles.Emit(emit, 1);
            }

            particles.Play();
        }

        private static void ConfigureParticleRenderer(ParticleSystemRenderer renderer, int sortingOrder)
        {
            if (renderer == null)
                return;

            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = sortingOrder;

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return;

            Material material = new Material(shader)
            {
                name = "Runtime Water Slash Foam",
                hideFlags = HideFlags.HideAndDontSave
            };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", Color.white);
            renderer.sharedMaterial = material;
        }
    }
}
