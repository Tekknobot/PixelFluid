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
