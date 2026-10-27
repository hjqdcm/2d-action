using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SlashDemo.Editor
{
    // Offline texture preparation only. These methods never rebuild or save the game scene.
    public static class ManorArtPreparation
    {
        const string LayoutPath = "Assets/SlashDemo/ArtNotes/PrivateManor-layout-v4.png";
        const string DetailFolder = "Assets/SlashDemo/ArtNotes/ManorDetail";
        public static void Prepare()
        {
            var source = AssetDatabase.LoadAssetAtPath<Texture2D>(LayoutPath);
            if (source == null) throw new Exception("Missing registered manor layout.");
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Verification/ArtSource"));
            foreach (var tile in new[] {
                (name: "NW", offset: new Vector2(0, 0.5f)),
                (name: "NE", offset: new Vector2(0.5f, 0.5f)),
                (name: "SW", offset: new Vector2(0, 0)),
                (name: "SE", offset: new Vector2(0.5f, 0)) })
            {
                var target = RenderTexture.GetTemporary(source.width / 2, source.height / 2, 0,
                    RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                try
                {
                    Graphics.Blit(source, target, Vector2.one * 0.5f, tile.offset);
                    Save(target, Path.Combine(folder, "Manor-" + tile.name + "-source.png"));
                }
                finally { RenderTexture.ReleaseTemporary(target); }
            }
            Debug.Log("MANOR_ART_SOURCES_PASS: four pixel-registered quadrant references saved to " + folder);
        }

        public static void Assemble()
        {
            var layout = AssetDatabase.LoadAssetAtPath<Texture2D>(LayoutPath);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/SlashDemo/Editor/ManorAssembly.shader");
            if (layout == null || shader == null || ShaderUtil.ShaderHasError(shader))
                throw new Exception("Manor assembly inputs or shader are invalid.");
            var material = new Material(shader);
            var target = RenderTexture.GetTemporary(3072, 2048, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            try
            {
                foreach (var tile in new[] { ("NW", "_NorthWest"), ("NE", "_NorthEast"), ("SW", "_SouthWest"), ("SE", "_SouthEast") })
                {
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(DetailFolder + "/Manor-" + tile.Item1 + ".png");
                    if (texture == null || texture.width != 1536 || texture.height != 1024)
                        throw new Exception("Detail quadrant must be 1536 x 1024: " + tile.Item1);
                    material.SetTexture(tile.Item2, texture);
                }
                Graphics.Blit(layout, target, material);
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Verification/ArtSource/PrivateManor-v4.png"));
                Save(target, path);
                Debug.Log("MANOR_HD_ASSEMBLY_PASS: 3072 x 2048 composite from four independently detailed tiles: " + path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
                RenderTexture.ReleaseTemporary(target);
            }
        }

        static void Save(RenderTexture target, string path)
        {
            var previous = RenderTexture.active;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
    }
}
