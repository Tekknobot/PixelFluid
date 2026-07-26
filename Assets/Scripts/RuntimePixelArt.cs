using UnityEngine;

namespace PixelOcean
{
    public static class RuntimePixelArt
    {
        public static Sprite CreateSurferSprite()
        {
            var tex = NewTexture(12, 22);
            Fill(tex, 4, 14, 4, 6, new Color32(34, 45, 55, 255));
            Fill(tex, 4, 18, 4, 4, new Color32(128, 78, 48, 255));
            Fill(tex, 3, 9, 6, 6, new Color32(242, 122, 58, 255));
            Fill(tex, 2, 4, 3, 6, new Color32(45, 55, 68, 255));
            Fill(tex, 7, 4, 3, 6, new Color32(45, 55, 68, 255));
            tex.Apply(); return Sprite.Create(tex, new Rect(0,0,12,22), new Vector2(.5f,.1f), 24f);
        }
        public static Sprite CreateBoardSprite()
        {
            var tex = NewTexture(26, 6);
            Fill(tex, 2, 1, 22, 4, new Color32(250, 210, 72, 255));
            Fill(tex, 5, 2, 15, 1, new Color32(255, 112, 70, 255));
            tex.Apply(); return Sprite.Create(tex, new Rect(0,0,26,6), new Vector2(.5f,.5f), 24f);
        }
        public static Sprite CreateShellSprite()
        {
            var tex = NewTexture(8, 7);
            Fill(tex, 2, 1, 4, 5, new Color32(255, 226, 185, 255));
            Fill(tex, 1, 1, 6, 2, new Color32(226, 153, 121, 255));
            tex.Apply(); return Sprite.Create(tex, new Rect(0,0,8,7), new Vector2(.5f,.1f), 24f);
        }
        public static Sprite CreateSharkSprite()
        {
            var tex = NewTexture(24, 10);
            Color32 body = new(72, 102, 122, 255);
            Fill(tex, 4, 3, 15, 4, body);
            Fill(tex, 1, 4, 5, 2, body);
            Fill(tex, 18, 4, 5, 2, body);
            Fill(tex, 10, 7, 5, 3, body);
            Fill(tex, 17, 3, 1, 1, new Color32(245, 245, 220, 255));
            tex.Apply(); return Sprite.Create(tex, new Rect(0,0,24,10), new Vector2(.5f,.5f), 32f);
        }
        public static Sprite CreateWhaleSprite()
        {
            var tex = NewTexture(30, 14);
            Color32 body = new(48, 82, 112, 255);
            Fill(tex, 5, 3, 20, 8, body);
            Fill(tex, 2, 5, 5, 5, body);
            Fill(tex, 24, 5, 5, 4, body);
            Fill(tex, 1, 9, 5, 4, body);
            Fill(tex, 23, 7, 1, 1, new Color32(235, 240, 230, 255));
            Fill(tex, 9, 3, 11, 2, new Color32(88, 126, 148, 255));
            tex.Apply(); return Sprite.Create(tex, new Rect(0,0,30,14), new Vector2(.5f,.5f), 32f);
        }
        public static Sprite CreateBuoySprite()
        {
            var tex = NewTexture(8, 14);
            Fill(tex, 2, 2, 4, 8, new Color32(232, 74, 52, 255));
            Fill(tex, 1, 5, 6, 3, new Color32(245, 235, 210, 255));
            Fill(tex, 3, 10, 2, 4, new Color32(65, 66, 70, 255));
            tex.Apply(); return Sprite.Create(tex, new Rect(0,0,8,14), new Vector2(.5f,.2f), 32f);
        }
        public static Sprite CreateCrateSprite()
        {
            var tex = NewTexture(12, 12);
            Fill(tex, 1, 1, 10, 10, new Color32(145, 91, 46, 255));
            Fill(tex, 2, 2, 8, 2, new Color32(205, 143, 72, 255));
            Fill(tex, 2, 8, 8, 2, new Color32(205, 143, 72, 255));
            Fill(tex, 5, 2, 2, 8, new Color32(92, 57, 34, 255));
            tex.Apply(); return Sprite.Create(tex, new Rect(0,0,12,12), new Vector2(.5f,.5f), 32f);
        }
        public static Sprite CreateBottleSprite()
        {
            var tex = NewTexture(7, 13);
            Fill(tex, 2, 1, 3, 8, new Color32(57, 145, 119, 230));
            Fill(tex, 3, 9, 1, 3, new Color32(57, 145, 119, 230));
            Fill(tex, 2, 12, 3, 1, new Color32(175, 112, 61, 255));
            Fill(tex, 2, 4, 3, 2, new Color32(230, 220, 170, 255));
            tex.Apply(); return Sprite.Create(tex, new Rect(0,0,7,13), new Vector2(.5f,.5f), 32f);
        }
        public static Sprite CreateTreasureSprite()
        {
            var tex = NewTexture(14, 11);
            Fill(tex, 1, 1, 12, 7, new Color32(124, 67, 35, 255));
            Fill(tex, 2, 7, 10, 3, new Color32(186, 116, 52, 255));
            Fill(tex, 6, 3, 2, 5, new Color32(244, 201, 56, 255));
            tex.Apply(); return Sprite.Create(tex, new Rect(0,0,14,11), new Vector2(.5f,.5f), 32f);
        }
        private static Texture2D NewTexture(int w, int h)
        {
            var tex = new Texture2D(w,h,TextureFormat.RGBA32,false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var clear = new Color32[w*h]; tex.SetPixels32(clear); return tex;
        }
        private static void Fill(Texture2D tex, int x, int y, int w, int h, Color32 c)
        {
            for(int iy=y; iy<y+h; iy++) for(int ix=x; ix<x+w; ix++) if(ix>=0&&iy>=0&&ix<tex.width&&iy<tex.height) tex.SetPixel(ix,iy,c);
        }
    }
}
