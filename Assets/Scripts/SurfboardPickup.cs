using UnityEngine;

namespace PixelOcean
{
    public sealed class SurfboardPickup : MonoBehaviour
    {
        private BeachGameController game;
        private SpriteRenderer renderer;
        public void Initialise(BeachGameController controller)
        {
            game = controller;
            renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimePixelArt.CreateBoardSprite();
            renderer.sortingOrder = 15;
            transform.rotation = Quaternion.Euler(0f, 0f, -12f);
        }
        private void Update()
        {
            BeachPlayer player = FindAnyObjectByType<BeachPlayer>();
            if (player == null || player.HasBoard) { if (player != null) Destroy(gameObject); return; }
            if (Vector2.Distance(transform.position, player.transform.position) < 0.85f)
            {
                game.SetMessage("Press E to pick up the surfboard");
                if (player.InteractPressed) { player.GiveBoard(); Destroy(gameObject); }
            }
        }
    }
}
