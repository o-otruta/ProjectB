using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectB.EditorScripts.UiKit
{
    /// <summary>
    /// Procedural pixel-art definitions of the UI icons (Sprint 13.1) and the redrawn main-menu tab icons
    /// (replacing the AI-generated Play / Abilities / Upgrade / Achievements / Bonus pictures).
    /// Mono icons are white so they can be tinted by the theme; currency / stat icons are coloured.
    /// </summary>
    public static class UiKitIcons
    {
        // Palette (voxel-toy, matches UiTheme accents).
        private static readonly Color32 W = new Color32(255, 255, 255, 255);
        private static readonly Color32 Wd = new Color32(214, 216, 232, 255);
        private static readonly Color32 Y = new Color32(255, 201, 60, 255);
        private static readonly Color32 YL = new Color32(255, 240, 160, 255);
        private static readonly Color32 YD = new Color32(206, 128, 30, 255);
        private static readonly Color32 R = new Color32(240, 70, 92, 255);
        private static readonly Color32 RL = new Color32(255, 156, 170, 255);
        private static readonly Color32 RD = new Color32(156, 30, 56, 255);
        private static readonly Color32 B = new Color32(61, 155, 255, 255);
        private static readonly Color32 BL = new Color32(168, 214, 255, 255);
        private static readonly Color32 C = new Color32(63, 224, 208, 255);
        private static readonly Color32 CL = new Color32(196, 255, 246, 255);
        private static readonly Color32 CD = new Color32(22, 138, 150, 255);
        private static readonly Color32 V = new Color32(150, 80, 240, 255);
        private static readonly Color32 VL = new Color32(214, 170, 255, 255);
        private static readonly Color32 VD = new Color32(86, 38, 160, 255);
        private static readonly Color32 N = new Color32(69, 212, 131, 255);
        private static readonly Color32 NL = new Color32(176, 255, 204, 255);
        private static readonly Color32 ND = new Color32(26, 128, 72, 255);
        private static readonly Color32 T = new Color32(150, 92, 48, 255);
        private static readonly Color32 TD = new Color32(96, 56, 26, 255);
        private static readonly Color32 S = new Color32(210, 218, 234, 255);
        private static readonly Color32 SD = new Color32(120, 132, 158, 255);
        private static readonly Color32 O = new Color32(255, 140, 46, 255);
        private static readonly Color32 K = PixelCanvas.OutlineColor;
        private static readonly Color32 X = PixelCanvas.Clear;

        public struct IconDef
        {
            public string name;
            public int grid;
            public bool mono;
            public Action<PixelCanvas> draw;
        }

        public static IEnumerable<IconDef> All()
        {
            // ---- Coloured 16×16 ----
            yield return Def("coin", false, p =>
            {
                p.Circle(8, 8, 6.6f, YD);
                p.Circle(8, 8, 5.7f, Y);
                p.Ring(8, 8, 3.2f, 4.2f, YD);
                p.Rect(7, 5, 8, 10, YD);
                p.Set(5, 4, YL); p.Set(4, 5, YL); p.Set(4, 6, YL); p.Set(6, 3, YL);
            });
            yield return Def("shard", false, p =>
            {
                p.Poly(C, V2(8, 1.2f), V2(13.6f, 7), V2(8, 14.8f), V2(2.4f, 7));
                p.Poly(CL, V2(8, 1.2f), V2(8, 7), V2(2.4f, 7));
                p.Poly(CD, V2(8, 7), V2(13.6f, 7), V2(8, 14.8f));
            });
            yield return Def("heart", false, p =>
            {
                p.Circle(5.4f, 6, 3.3f, R);
                p.Circle(10.6f, 6, 3.3f, R);
                p.Poly(R, V2(2.2f, 7), V2(13.8f, 7), V2(8, 14));
                p.Set(4, 4, RL); p.Set(5, 4, RL); p.Set(4, 5, RL);
            });
            yield return Def("speed", false, p =>
            {
                p.Poly(Y, V2(9.8f, 1), V2(3.2f, 9.2f), V2(7.4f, 9.2f), V2(5.8f, 15), V2(12.8f, 6.6f), V2(8.6f, 6.6f), V2(10.8f, 1));
            });
            yield return Def("sword", false, p =>
            {
                p.Line(4.5f, 11.5f, 13.2f, 2.8f, 2.4f, S);
                p.Line(6, 10, 12.6f, 3.4f, 0.8f, W);
                p.Line(2.6f, 9.4f, 6.6f, 13.4f, 2f, YD);
                p.Line(1.8f, 14.2f, 4.2f, 11.8f, 2f, T);
            });
            yield return Def("clock", false, p =>
            {
                p.Circle(8, 8, 6.6f, B);
                p.Circle(8, 8, 5.1f, W);
                p.Line(8, 8, 8, 4.2f, 1.3f, K);
                p.Line(8, 8, 10.8f, 8, 1.3f, K);
            });
            yield return Def("skull", false, p =>
            {
                p.Circle(8, 7, 5.6f, W);
                p.Rect(5, 10, 10, 13, W);
                p.Circle(6, 7.6f, 1.5f, K);
                p.Circle(10, 7.6f, 1.5f, K);
                p.Set(7, 10, K); p.Set(8, 10, K);
                p.Set(6, 12, K); p.Set(8, 12, K);
            });
            yield return Def("star", false, p =>
            {
                p.Star(8, 8.6f, 7.2f, 3.1f, Y);
                p.Set(7, 4, YL); p.Set(6, 7, YL);
            });
            yield return Def("flame", false, p =>
            {
                p.Poly(O, V2(8, 1), V2(12.8f, 7), V2(12.8f, 11), V2(10.6f, 14.4f), V2(5.4f, 14.4f), V2(3.2f, 11), V2(3.2f, 7.2f), V2(6, 4.4f), V2(7, 7));
                p.Circle(8, 11, 2.6f, Y);
            });
            yield return Def("ad", false, p =>
            {
                p.Rect(2, 3, 13, 12, B);
                p.Set(2, 3, X); p.Set(13, 3, X); p.Set(2, 12, X); p.Set(13, 12, X);
                p.Poly(W, V2(6, 5.2f), V2(11.6f, 8), V2(6, 10.8f));
            });

            // ---- Mono (white, tintable) 16×16 ----
            yield return Def("gear", true, p =>
            {
                for (int i = 0; i < 8; i++)
                {
                    float a = i * 45f * Mathf.Deg2Rad;
                    p.Line(8, 8, 8 + Mathf.Cos(a) * 6.4f, 8 + Mathf.Sin(a) * 6.4f, 2.8f, W);
                }
                p.Circle(8, 8, 4.8f, W);
                p.Circle(8, 8, 1.9f, X);
            });
            yield return Def("pause", true, p =>
            {
                p.Rect(4, 3, 6, 12, W);
                p.Rect(9, 3, 11, 12, W);
            });
            yield return Def("lock", true, p =>
            {
                p.Arc(8, 6.6f, 2.4f, 4.1f, -180f, 0f, W);
                p.Rect(4, 6, 5, 7, W);
                p.Rect(11, 6, 12, 7, W);
                p.Rect(3, 7, 12, 13, W);
                p.Rect(7, 9, 8, 11, K);
            });
            yield return Def("check", true, p =>
            {
                p.Line(3, 8.5f, 6.6f, 12, 2.6f, W);
                p.Line(6.6f, 12, 13, 4.2f, 2.6f, W);
            });
            yield return Def("chevron_left", true, p =>
            {
                p.Line(10.4f, 3, 5.6f, 8, 2.8f, W);
                p.Line(5.6f, 8, 10.4f, 13, 2.8f, W);
            });
            yield return Def("chevron_right", true, p =>
            {
                p.Line(5.6f, 3, 10.4f, 8, 2.8f, W);
                p.Line(10.4f, 8, 5.6f, 13, 2.8f, W);
            });
            yield return Def("plus", true, p =>
            {
                p.Rect(7, 3, 8, 12, W);
                p.Rect(3, 7, 12, 8, W);
            });
            yield return Def("minus", true, p => p.Rect(3, 7, 12, 8, W));
            yield return Def("target", true, p =>
            {
                p.Ring(8, 8, 3.6f, 5.2f, W);
                p.Rect(7, 1, 8, 3, W);
                p.Rect(7, 12, 8, 14, W);
                p.Rect(1, 7, 3, 8, W);
                p.Rect(12, 7, 14, 8, W);
                p.Circle(8, 8, 1.5f, W);
            });
            yield return Def("close", true, p =>
            {
                p.Line(4, 4, 12, 12, 2.6f, W);
                p.Line(12, 4, 4, 12, 2.6f, W);
            });
            yield return Def("play", true, p => p.Poly(W, V2(5, 2.8f), V2(13.2f, 8), V2(5, 13.2f)));
            yield return Def("reroll", true, p =>
            {
                p.Arc(8, 8, 3.8f, 5.6f, -60f, 220f, W);
                p.Poly(W, V2(9.5f, 1.6f), V2(14.2f, 3.8f), V2(10.4f, 7.4f));
            });
            yield return Def("music", true, p =>
            {
                p.Rect(10, 2, 11, 10, W);
                p.Circle(8, 11.4f, 2.6f, W);
                p.Line(11, 2.4f, 14, 4.6f, 1.6f, W);
            });
            yield return Def("sound", true, p =>
            {
                p.Poly(W, V2(1.8f, 5.6f), V2(5, 5.6f), V2(8.8f, 2.4f), V2(8.8f, 13.6f), V2(5, 10.4f), V2(1.8f, 10.4f));
                p.Arc(8.6f, 8, 2.6f, 3.8f, -55f, 55f, W);
                p.Arc(8.6f, 8, 5f, 6.2f, -55f, 55f, W);
            });
            yield return Def("globe", true, p =>
            {
                p.Ring(8, 8, 5.2f, 6.6f, W);
                p.Line(8, 1.6f, 8, 14.4f, 1.2f, W);
                p.Line(1.6f, 8, 14.4f, 8, 1.2f, W);
                p.Arc(8, 8, 2.6f, 3.6f, -180f, 180f, W);
            });

            // ---- Main menu tabs, 24×24, coloured ----
            yield return Tab("tab_upgrades", p =>
            {
                p.Poly(N, V2(12, 1.6f), V2(21.6f, 11.2f), V2(16.2f, 11.2f), V2(16.2f, 21.4f), V2(7.8f, 21.4f), V2(7.8f, 11.2f), V2(2.4f, 11.2f));
                p.Poly(NL, V2(12, 1.6f), V2(12, 4.4f), V2(5.2f, 11.2f), V2(2.4f, 11.2f));
                p.Rect(8, 12, 9, 20, NL);
                p.Rect(15, 12, 16, 21, ND);
                p.Star(19.4f, 18.2f, 3.6f, 1.5f, Y);
            });
            yield return Tab("tab_abilities", p =>
            {
                p.Rect(5, 19, 20, 21, Wd);
                p.Rect(4, 2, 19, 19, V);
                p.Rect(4, 2, 6, 20, VD);
                p.Rect(7, 3, 18, 3, VL);
                p.Star(12.8f, 10.6f, 5.4f, 2.3f, Y);
                p.Set(12, 8, YL); p.Set(11, 10, YL);
                p.Rect(16, 2, 18, 4, YD);
                p.Rect(16, 17, 18, 19, YD);
            });
            yield return Tab("tab_play", p =>
            {
                p.Line(4.2f, 19.8f, 19.4f, 4.6f, 2.8f, S);
                p.Line(19.8f, 19.8f, 4.6f, 4.6f, 2.8f, S);
                p.Line(6, 18, 18.6f, 5.4f, 0.9f, W);
                p.Line(18, 18, 5.4f, 5.4f, 0.9f, W);
                p.Line(5.2f, 14, 10, 18.8f, 2.4f, YD);
                p.Line(18.8f, 14, 14, 18.8f, 2.4f, YD);
                p.Line(2.6f, 21.4f, 5.8f, 18.2f, 2.4f, T);
                p.Line(21.4f, 21.4f, 18.2f, 18.2f, 2.4f, T);
                p.Circle(2.4f, 21.6f, 1.5f, Y);
                p.Circle(21.6f, 21.6f, 1.5f, Y);
            });
            yield return Tab("tab_achievements", p =>
            {
                p.Arc(5.6f, 7, 2.2f, 3.6f, 80f, 290f, Y);
                p.Arc(18.4f, 7, 2.2f, 3.6f, -110f, 100f, Y);
                p.Poly(Y, V2(5.6f, 2.6f), V2(18.4f, 2.6f), V2(17.4f, 10.4f), V2(14.2f, 13.6f), V2(9.8f, 13.6f), V2(6.6f, 10.4f));
                p.Line(8.6f, 4.4f, 9.2f, 9.4f, 1.3f, YL);
                p.Rect(11, 13, 12, 16, YD);
                p.Rect(8, 16, 15, 17, YD);
                p.Rect(6, 18, 17, 21, T);
                p.Rect(9, 19, 14, 19, Y);
                p.Star(13.6f, 7.4f, 2.8f, 1.2f, W);
            });
            yield return Tab("tab_bonus", p =>
            {
                p.Circle(8.8f, 5, 3f, Y);
                p.Circle(15.2f, 5, 3f, Y);
                p.Circle(8.8f, 5, 1.3f, YD);
                p.Circle(15.2f, 5, 1.3f, YD);
                p.Rect(4, 11, 19, 21, R);
                p.Rect(3, 7, 20, 10, RL);
                p.Rect(3, 10, 20, 10, RD);
                p.Rect(11, 7, 12, 21, Y);
                p.Rect(10, 5, 13, 7, YD);
                p.Rect(5, 12, 6, 19, RL);
            });
        }

        private static IconDef Def(string name, bool mono, Action<PixelCanvas> draw)
        {
            return new IconDef { name = name, grid = 16, mono = mono, draw = draw };
        }

        private static IconDef Tab(string name, Action<PixelCanvas> draw)
        {
            return new IconDef { name = name, grid = 24, mono = false, draw = draw };
        }

        private static Vector2 V2(float x, float y) => new Vector2(x, y);
    }
}
