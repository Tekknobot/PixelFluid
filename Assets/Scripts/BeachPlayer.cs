using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PixelOcean
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    public sealed class BeachPlayer : MonoBehaviour
    {
        private enum State { Walking, Swimming, Paddling, Surfing }
        private BeachGameController game;
        private Rigidbody2D body;
        private SpriteRenderer renderer;
        private State state;
        private bool hasBoard;
        private float balance;
        private float horizontal;
        private bool interactPressed;
        private bool jumpPressed;
        private bool paddleHeld;

        public bool HasBoard => hasBoard;
        public bool InteractPressed => interactPressed;

        public void Initialise(BeachGameController controller)
        {
            game = controller;
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 1.8f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var collider = GetComponent<CapsuleCollider2D>();
            collider.size = new Vector2(0.30f, 0.72f);
            renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimePixelArt.CreateSurferSprite();
            renderer.sortingOrder = 20;
        }

        private void Update()
        {
            ReadInput();
            float seabed = game.Water.GetSeabedHeightAtWorldX(transform.position.x);
            float waterSurface = game.Water.GetGameplaySurfaceHeight(transform.position.x);
            bool inWater = transform.position.x < game.Water.TankMaximum.x - 0.15f && transform.position.y < waterSurface + 0.18f;

            if (!inWater)
                state = State.Walking;
            else if (hasBoard && state == State.Walking)
                state = State.Paddling;
            else if (!hasBoard)
                state = State.Swimming;

            if (hasBoard && inWater && jumpPressed && state != State.Surfing)
            {
                state = State.Surfing;
                balance = 0f;
                game.SetMessage("Ride the pocket — use A/D to balance");
            }

            renderer.flipX = horizontal < -0.05f;
            renderer.transform.localScale = state == State.Surfing ? new Vector3(1.3f, 0.82f, 1f) : Vector3.one;
            renderer.color = state == State.Swimming ? new Color(0.80f, 0.92f, 1f) : Color.white;

            if (transform.position.y < game.Water.TankMinimum.y - 1f)
                Respawn();
        }

        private void FixedUpdate()
        {
            float surface = game.Water.GetGameplaySurfaceHeight(transform.position.x);
            switch (state)
            {
                case State.Walking:
                    body.gravityScale = 1.8f;
                    body.linearVelocity = new Vector2(horizontal * 3.2f, body.linearVelocity.y);
                    game.SetRide(false, 0f);
                    break;
                case State.Swimming:
                    body.gravityScale = 0.12f;
                    body.AddForce(new Vector2(horizontal * 5f, (surface - transform.position.y) * 5f));
                    body.linearVelocity *= 0.96f;
                    game.SetRide(false, 0f);
                    break;
                case State.Paddling:
                    body.gravityScale = 0.05f;
                    float paddle = paddleHeld ? 8.5f : 3.5f;
                    body.AddForce(new Vector2(horizontal * paddle, (surface + 0.02f - transform.position.y) * 9f));
                    body.linearVelocity *= 0.985f;
                    game.SetRide(false, 0f);
                    break;
                case State.Surfing:
                    body.gravityScale = 0.05f;
                    Vector2 waveVelocity = game.Water.GetGameplayWaveVelocity(transform.position.x);
                    balance += horizontal * Time.fixedDeltaTime * 1.8f;
                    balance -= balance * Time.fixedDeltaTime * 0.75f;
                    body.AddForce(new Vector2(waveVelocity.x * 1.65f, (surface + 0.12f - transform.position.y) * 18f + waveVelocity.y * 2f));
                    body.linearVelocity = Vector2.ClampMagnitude(body.linearVelocity, 12f);
                    transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Clamp(-body.linearVelocity.y * 3f - balance * 10f, -22f, 22f));
                    game.SetRide(Mathf.Abs(surface - transform.position.y) < 0.55f, body.linearVelocity.magnitude);
                    if (Mathf.Abs(balance) > 1.15f || transform.position.y < surface - 0.6f)
                    {
                        state = State.Paddling;
                        transform.rotation = Quaternion.identity;
                        game.SetMessage("Wipeout — paddle back into position");
                    }
                    break;
            }
        }

        public void GiveBoard()
        {
            hasBoard = true;
            game.SetMessage("Board equipped — enter the water and press Space to paddle");
        }

        private void Respawn()
        {
            transform.position = new Vector3(game.Water.TankMaximum.x + 1.2f,
                game.Water.GetSeabedHeightAtWorldX(game.Water.TankMaximum.x) + 0.7f, -0.5f);
            body.linearVelocity = Vector2.zero;
            state = State.Walking;
        }

        private void ReadInput()
        {
            interactPressed = false;
            jumpPressed = false;
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null) { horizontal = 0f; return; }
            horizontal = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f) -
                         (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
            interactPressed = keyboard.eKey.wasPressedThisFrame;
            jumpPressed = keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame;
            paddleHeld = keyboard.spaceKey.isPressed;
#else
            horizontal = Input.GetAxisRaw("Horizontal");
            interactPressed = Input.GetKeyDown(KeyCode.E);
            jumpPressed = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
            paddleHeld = Input.GetKey(KeyCode.Space);
#endif
        }
    }
}
