using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Railrush.Content
{
    public static class ImportedArt
    {
        public const string Root = "RailrushAssets/";
        public enum Orientation { Original, LongAxisForward, Upright }

        private static readonly Dictionary<string, GameObject> Models = new();
        private static readonly Dictionary<string, Texture2D> Textures = new();
        private static readonly Dictionary<(string, Material), Material> Materials = new();
        private static Shader surface;

        public static GameObject LoadModel(string name)
        {
            if (!Models.TryGetValue(name, out var model))
            {
                model = Resources.Load<GameObject>(Root + "Models/" + name);
                Models.Add(name, model);
            }
            return model;
        }

        public static bool TryPlace(Transform parent, string name, Vector3 dimensions, Vector3 position,
            out Transform mount, out GameObject model, bool centered = false, bool uniform = false,
            Orientation orientation = Orientation.Original)
        {
            mount = null;
            model = null;
            var asset = LoadModel(name);
            if (surface == null) surface = Resources.Load<Shader>(Root + "RailrushImportedSurface");
            if (asset == null || surface == null) return false;

            var holder = new GameObject(name + " · imported");
            model = UnityEngine.Object.Instantiate(asset, holder.transform, false);
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                UnityEngine.Object.Destroy(holder);
                model = null;
                return false;
            }

            Bounds bounds = Measure(renderers);
            if ((orientation == Orientation.LongAxisForward && bounds.size.x > bounds.size.z) ||
                (orientation == Orientation.Upright && bounds.size.y < bounds.size.z))
            {
                model.transform.localRotation = orientation == Orientation.LongAxisForward
                    ? Quaternion.Euler(0, 90, 0) : Quaternion.Euler(90, 0, 0);
                bounds = Measure(renderers);
            }

            Vector3 size = bounds.size;
            if (!Valid(size))
            {
                UnityEngine.Object.Destroy(holder);
                model = null;
                return false;
            }

            foreach (var renderer in renderers)
            {
                Material[] source = renderer.sharedMaterials;
                if (source.Length == 0) source = new Material[1];
                for (int i = 0; i < source.Length; i++)
                    source[i] = GetSurface(name, source[i]);
                renderer.sharedMaterials = source;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            foreach (var collider in model.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;

            Vector3 origin = bounds.center;
            if (!centered) origin.y = bounds.min.y;
            model.transform.localPosition -= origin;
            mount = holder.transform;
            mount.SetParent(parent, false);
            mount.localPosition = position;
            mount.localScale = uniform
                ? Vector3.one * (dimensions.y / size.y)
                : new Vector3(dimensions.x / size.x, dimensions.y / size.y, dimensions.z / size.z);
            return true;
        }

        public static bool Place(Transform parent, string name, Vector3 dimensions, Vector3 position,
            bool centered = false, Orientation orientation = Orientation.Original)
        {
            return TryPlace(parent, name, dimensions, position, out _, out _, centered, false, orientation);
        }

        private static Bounds Measure(Renderer[] renderers)
        {
            Bounds result = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) result.Encapsulate(renderers[i].bounds);
            return result;
        }

        private static bool Valid(Vector3 v) =>
            v.x > .00001f && v.y > .00001f && v.z > .00001f &&
            !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);

        private static Material GetSurface(string key, Material source)
        {
            if (Materials.TryGetValue((key, source), out var material)) return material;
            Color tint = source != null && source.HasProperty("_Color") ? source.color : Color.white;
            string name = source != null ? source.name : "";
            Texture texture = source != null && source.HasProperty("_MainTex") ? source.mainTexture : null;
            string atlas = key.StartsWith("Building", StringComparison.Ordinal) ? "City"
                : key == "Coin" ? "Platformer" : key == "Boots" ? "Boots" : null;
            if (atlas != null)
            {
                if (!Textures.TryGetValue(atlas, out var downloaded))
                    Textures[atlas] = downloaded = Resources.Load<Texture2D>(Root + "Textures/" + atlas);
                if (downloaded != null) texture = downloaded;
            }
            float glow = .03f;
            if (key == "Courier" && name.Contains("Shirt")) tint = new Color(.17f, .40f, .88f);
            if (key == "Courier" && name.Contains("Pants")) tint = new Color(.14f, .20f, .36f);
            if (key.StartsWith("Building", StringComparison.Ordinal)) tint *= new Color(.57f, .68f, .90f);
            if (key.StartsWith("Train", StringComparison.Ordinal))
            {
                if (name.Contains("White")) tint = new Color(.70f, .76f, .87f);
                if (name.Contains("Blue")) tint = new Color(.17f, .41f, .90f);
                glow = .12f;
            }
            if (key == "Rail") tint = name.Contains("Steel") ? new Color(.56f, .70f, .86f) : new Color(.27f, .21f, .31f);
            if (key == "Hurdle" || key == "Gate")
            {
                tint = key == "Gate" ? new Color(.91f, .38f, .16f) : new Color(.94f, .67f, .19f);
                glow = .18f;
            }
            if ((key == "Lamp" || key == "Sign") &&
                (name.IndexOf("light", StringComparison.OrdinalIgnoreCase) >= 0 || tint.maxColorComponent > .6f))
            {
                tint = key == "Lamp" ? new Color(.23f, .88f, 1f) : new Color(.55f, .26f, .98f);
                glow = .42f;
            }
            if (key == "Coin") { tint = new Color(1f, .85f, .40f); glow = .38f; }
            if (key == "Magnet") { tint = new Color(1f, .22f, .31f); glow = .29f; }
            if (key == "Boots") { tint = new Color(.86f, 1f, .64f); glow = .18f; }
            material = new Material(surface) { name = key + " / " + name };
            material.SetColor("_Color", tint);
            if (texture != null) material.SetTexture("_MainTex", texture);
            material.SetFloat("_Glow", glow);
            Materials.Add((key, source), material);
            return material;
        }
    }
}
