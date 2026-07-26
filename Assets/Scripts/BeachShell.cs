using UnityEngine;

namespace PixelOcean
{
    public sealed class BeachShell : MonoBehaviour
    {
        private BeachGameController game;
        public void Initialise(BeachGameController controller)
        {
            game = controller;
            var sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = RuntimePixelArt.CreateShellSprite();
            sr.sortingOrder = 14;
        }
        private void Update()
        {
            var player = FindAnyObjectByType<BeachPlayer>();
            if (player != null && Vector2.Distance(transform.position, player.transform.position) < 0.38f)
            {
                game.AddShell();
                Destroy(gameObject);
            }
        }
    }
}
