using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Horcrux.Editor.Probe
{
    /// <summary>
    /// In cây object ra text, mỗi object một dòng: transform local, sprite và bounds trong hệ toạ độ root, sorting, RectTransform,
    /// component còn lại. Đọc trên object đã dựng nên thấy cả override của prefab lồng nhau, thứ YAML không cho thấy.
    /// </summary>
    public static class PrefabDump
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        /// <param name="pathFilter">Chỉ in object có đường dẫn chứa chuỗi này; null in hết.</param>
        public static string Dump(GameObject root, string pathFilter = null)
        {
            var sb = new StringBuilder();
            var rootFromWorld = root.transform.worldToLocalMatrix;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var path = t == root.transform ? root.name : AnimationUtility.CalculateTransformPath(t, root.transform);
                if (pathFilter != null && !path.Contains(pathFilter))
                    continue;
                AppendObject(sb, t, path, rootFromWorld);
            }
            return sb.ToString();
        }

        /// <summary>Prefab asset nạp qua AssetDatabase, không instantiate: bounds tính từ sprite nên không cần renderer chạy.</summary>
        public static string DumpAsset(string assetPath, string pathFilter = null) =>
            Dump(AssetDatabase.LoadAssetAtPath<GameObject>(assetPath), pathFilter);

        private static void AppendObject(StringBuilder sb, Transform t, string path, Matrix4x4 rootFromWorld)
        {
            sb.Append(path).Append(t.gameObject.activeSelf ? " on" : " OFF");
            sb.Append(" p=").Append(Vec(t.localPosition)).Append(" s=").Append(Vec(t.localScale));
            sb.Append(" r=").Append(Num(t.localEulerAngles.z));

            if (t is RectTransform rt)
                sb.Append(" | rt a=").Append(Vec(rt.anchoredPosition)).Append(" sd=").Append(Vec(rt.sizeDelta))
                    .Append(" anc=").Append(Vec(rt.anchorMin)).Append('-').Append(Vec(rt.anchorMax)).Append(" pv=").Append(Vec(rt.pivot));

            if (t.TryGetComponent<SpriteRenderer>(out var sr))
                AppendSprite(sb, sr, rootFromWorld);

            if (t.TryGetComponent<SortingGroup>(out var group))
                sb.Append(" | sg ").Append(group.sortingLayerName).Append('#').Append(group.sortingOrder);

            var first = true;
            foreach (var c in t.GetComponents<Component>())
            {
                if (c is Transform || c is SpriteRenderer || c is SortingGroup)
                    continue;
                sb.Append(first ? " | " : ",").Append(c == null ? "Missing" : c.GetType().Name);
                first = false;
            }
            sb.Append('\n');
        }

        private static void AppendSprite(StringBuilder sb, SpriteRenderer sr, Matrix4x4 rootFromWorld)
        {
            sb.Append(" | sr ").Append(sr.enabled ? "" : "OFF ").Append(sr.sprite != null ? sr.sprite.name : "null");
            sb.Append(' ').Append(sr.sortingLayerName).Append('#').Append(sr.sortingOrder).Append(" a=").Append(Num(sr.color.a));
            if (sr.drawMode != SpriteDrawMode.Simple)
                sb.Append(' ').Append(sr.drawMode).Append(" size=").Append(Vec(sr.size));
            if (sr.sprite == null)
                return;

            var local = LocalBounds(sr);
            var rootFromLocal = rootFromWorld * sr.transform.localToWorldMatrix;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (var i = 0; i < 4; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? local.min.x : local.max.x, (i & 2) == 0 ? local.min.y : local.max.y, 0f);
                Vector2 p = rootFromLocal.MultiplyPoint3x4(corner);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            sb.Append(" root c=").Append(Vec((min + max) * 0.5f)).Append(" size=").Append(Vec(max - min));
        }

        // Sliced, tiled vẽ theo sr.size quanh pivot của sprite; simple vẽ đúng sprite.bounds.
        private static Bounds LocalBounds(SpriteRenderer sr)
        {
            if (sr.drawMode == SpriteDrawMode.Simple)
                return sr.sprite.bounds;
            var pivot = sr.sprite.pivot / sr.sprite.rect.size;
            return new Bounds((Vector2.one * 0.5f - pivot) * sr.size, sr.size);
        }

        private static string Num(float value) => value.ToString("0.###", Invariant);

        private static string Vec(Vector2 v) => Num(v.x) + "," + Num(v.y);

        private static string Vec(Vector3 v) => Num(v.x) + "," + Num(v.y) + "," + Num(v.z);
    }
}
