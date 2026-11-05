using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LightSide;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Horcrux.Editor.TmpToUniText
{
    /// <summary>
    /// Mang "kiểu chữ" (material preset viền, bóng của TMP) sang StylePreset của UniText. Tool Migration của UniText
    /// thay component nhưng bỏ material, nên chụp bảng trước khi migrate rồi gán preset sau.
    /// </summary>
    public static class TmpLookMigration
    {
        [Serializable]
        private sealed class LookRecord
        {
            public string asset;
            public string path;
            public string materialGuid;
            public string materialName;
            public bool vertexGradient;
            public Color topLeft, topRight, bottomLeft, bottomRight;
        }

        [Serializable]
        private sealed class LookTable
        {
            public List<LookRecord> records = new();
        }

        private const string PresetPrefix = "Text ";

        #region API

        /// <summary>
        /// Ghi mọi chữ TMP mà material, gradient được author ngay ở asset đó (không thừa hưởng từ prefab lồng) vào
        /// <paramref name="jsonPath"/>. Trả số bản ghi mỗi asset.
        /// </summary>
        public static string Snapshot(IEnumerable<string> assetPaths, string jsonPath)
        {
            var table = new LookTable();
            var report = new StringBuilder();
            foreach (var assetPath in assetPaths)
            {
                var before = table.records.Count;
                ForEachRoot(assetPath, false, root =>
                {
                    foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                        if (IsLookAuthoredHere(text))
                            table.records.Add(Record(assetPath, root.transform, text));
                });
                report.Append(assetPath).Append(": ").Append(table.records.Count - before).Append('\n');
            }
            File.WriteAllText(jsonPath, JsonUtility.ToJson(table, true));
            report.Append("total ").Append(table.records.Count).Append(" -> ").Append(jsonPath);
            return report.ToString();
        }

        /// <summary>
        /// Mỗi material trong bảng (trừ material mặc định của font) thành một StylePreset <c>Text &lt;tên&gt;.asset</c>
        /// trong <paramref name="folder"/>. Đã có file thì giữ nguyên. Trả dòng chuyển đổi và phần không mang sang.
        /// </summary>
        public static string CreatePresets(string jsonPath, string folder)
        {
            var table = Load(jsonPath);
            var report = new StringBuilder();
            var done = new HashSet<string>();
            Directory.CreateDirectory(folder);
            foreach (var record in table.records)
            {
                if (string.IsNullOrEmpty(record.materialGuid) || !done.Add(record.materialGuid)) continue;
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(record.materialGuid));
                var font = FontOf(material);
                if (material == null || font == null || IsDefaultMaterial(material, font)) continue;

                var path = PresetPath(folder, material, font);
                if (AssetDatabase.LoadAssetAtPath<StylePreset>(path) != null)
                {
                    report.Append("keep ").Append(path).Append('\n');
                    continue;
                }
                var preset = ScriptableObject.CreateInstance<StylePreset>();
                var notes = new List<string>();
                foreach (var style in ToStyles(material, font.faceInfo.pointSize, notes))
                    preset.Styles.Add(style);
                AssetDatabase.CreateAsset(preset, path);
                report.Append("create ").Append(path).Append(notes.Count > 0 ? " | " + string.Join("; ", notes) : "").Append('\n');
            }
            AssetDatabase.SaveAssets();
            return report.ToString();
        }

        /// <summary>
        /// Sau khi tool Migration của UniText đã thay component: gán preset của từng bản ghi vào ô Style Presets của
        /// UniText cùng GameObject (material mặc định thì để trống). Trả dòng thiếu và gradient cần dựng tay.
        /// </summary>
        public static string Apply(string jsonPath, string folder)
        {
            var table = Load(jsonPath);
            var report = new StringBuilder();
            var byAsset = new Dictionary<string, List<LookRecord>>();
            foreach (var record in table.records)
            {
                if (!byAsset.TryGetValue(record.asset, out var list)) byAsset[record.asset] = list = new List<LookRecord>();
                list.Add(record);
            }

            var applied = new HashSet<LookRecord>();
            foreach (var pair in byAsset)
            {
                ForEachRoot(pair.Key, true, root =>
                {
                    foreach (var record in pair.Value)
                    {
                        var target = Find(root.transform, record.path);
                        var text = target != null ? target.GetComponent<UniTextBase>() : null;
                        if (text == null) continue;
                        SetPreset(text, LoadPreset(folder, record));
                        applied.Add(record);
                        if (record.vertexGradient)
                            report.Append("gradient by hand ").Append(record.asset).Append(" | ").Append(record.path)
                                .Append(" ").Append(ColorUtility.ToHtmlStringRGBA(record.topLeft)).Append(' ')
                                .Append(ColorUtility.ToHtmlStringRGBA(record.bottomLeft)).Append('\n');
                    }
                });
            }
            foreach (var record in table.records)
                if (!applied.Contains(record))
                    report.Append("missing ").Append(record.asset).Append(" | ").Append(record.path).Append('\n');
            report.Append("applied ").Append(applied.Count).Append(" / ").Append(table.records.Count);
            return report.ToString();
        }

        #endregion

        #region Class Methods

        /// <summary>
        /// Đổi thông số shader SDF của TMP sang lớp sơn UniText, đơn vị em theo point size của atlas. TMP đo mọi lớp
        /// bằng khoảng cách SDF: 1 đơn vị shader = <c>_GradientScale</c> px atlas sau khi nhân scale ratio.
        /// </summary>
        private static List<Style> ToStyles(Material material, float pointSize, List<string> notes)
        {
            var styles = new List<Style>();
            var gradientScale = material.GetFloat("_GradientScale");
            var ratioA = material.GetFloat("_ScaleRatioA");
            var ratioC = material.GetFloat("_ScaleRatioC");
            Func<float, float, float> toEm = (value, ratio) => value * ratio * gradientScale / pointSize;

            var faceDilate = material.GetFloat("_FaceDilate");
            var outlineWidth = material.GetFloat("_OutlineWidth");
            if (outlineWidth > 0f)
            {
                // Viền TMP nằm giữa mép đã nở theo FaceDilate: mép ngoài cách mép gốc dilate + nửa bề dày.
                var stroke = new StrokeModifier
                {
                    Paint = PaintRef.Solid(material.GetColor("_OutlineColor")),
                    Width = UnitValue.Em(toEm(faceDilate, ratioA) + toEm(outlineWidth, ratioA) * 0.5f),
                    Align = 1f,
                    Softness = UnitValue.Em(toEm(material.GetFloat("_OutlineSoftness"), ratioA)),
                };
                styles.Add(Style.WholeText(stroke));
            }
            else if (faceDilate > 0f)
                notes.Add("FaceDilate " + faceDilate + " not carried");

            if (material.IsKeywordEnabled("UNDERLAY_ON") || material.IsKeywordEnabled("UNDERLAY_INNER"))
            {
                var color = material.GetColor("_UnderlayColor");
                var offset = new Vector2(material.GetFloat("_UnderlayOffsetX"), material.GetFloat("_UnderlayOffsetY"));
                var dilate = toEm(material.GetFloat("_UnderlayDilate"), ratioC);
                var softness = toEm(material.GetFloat("_UnderlaySoftness"), ratioC);
                if (offset == Vector2.zero)
                {
                    // Underlay không lệch là viền giả: vẽ thành stroke ngoài để cùng dạng với viền thật.
                    styles.Add(Style.WholeText(new StrokeModifier
                    {
                        Paint = PaintRef.Solid(color),
                        Width = UnitValue.Em(dilate),
                        Align = 1f,
                        Softness = UnitValue.Em(softness),
                    }));
                }
                else
                {
                    styles.Add(Style.WholeText(new ShadowModifier
                    {
                        Paint = PaintRef.Solid(color),
                        Offset = new UnitVector2(new Vector2(toEm(offset.x, ratioC), toEm(offset.y, ratioC)), UnitKind.Em),
                        Blur = UnitValue.Em(softness),
                        Spread = UnitValue.Em(dilate),
                    }));
                }
            }

            foreach (var keyword in new[] { "BEVEL_ON", "GLOW_ON" })
                if (material.IsKeywordEnabled(keyword)) notes.Add(keyword + " not carried");
            if (material.GetColor("_FaceColor") != Color.white) notes.Add("FaceColor not white");
            return styles;
        }

        private static bool IsLookAuthoredHere(TMP_Text text)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(text)) return true;
            var serialized = new SerializedObject(text);
            foreach (var name in new[] { "m_sharedMaterial", "m_enableVertexGradient", "m_fontColorGradient" })
            {
                var property = serialized.FindProperty(name);
                if (property != null && property.prefabOverride) return true;
            }
            return false;
        }

        private static LookRecord Record(string assetPath, Transform root, TMP_Text text)
        {
            var material = text.fontSharedMaterial;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(material, out var guid, out long _);
            var gradient = text.colorGradient;
            return new LookRecord
            {
                asset = assetPath,
                path = PathOf(root, text.transform),
                materialGuid = material != null && AssetDatabase.GetAssetPath(material).EndsWith(".mat") ? guid : "",
                materialName = material != null ? material.name : "",
                vertexGradient = text.enableVertexGradient,
                topLeft = gradient.topLeft,
                topRight = gradient.topRight,
                bottomLeft = gradient.bottomLeft,
                bottomRight = gradient.bottomRight,
            };
        }

        private static void SetPreset(UniTextBase text, StylePreset preset)
        {
            var serialized = new SerializedObject(text);
            var list = serialized.FindProperty("stylePresets.items");
            list.arraySize = preset != null ? 1 : 0;
            if (preset != null) list.GetArrayElementAtIndex(0).objectReferenceValue = preset;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static StylePreset LoadPreset(string folder, LookRecord record)
        {
            if (string.IsNullOrEmpty(record.materialGuid)) return null;
            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(record.materialGuid));
            var font = FontOf(material);
            if (material == null || font == null || IsDefaultMaterial(material, font)) return null;
            return AssetDatabase.LoadAssetAtPath<StylePreset>(PresetPath(folder, material, font));
        }

        private static string PresetPath(string folder, Material material, TMP_FontAsset font)
        {
            var name = material.name.StartsWith(font.name) ? material.name.Substring(font.name.Length).Trim() : material.name;
            return folder.TrimEnd('/') + "/" + PresetPrefix + name + ".asset";
        }

        private static bool IsDefaultMaterial(Material material, TMP_FontAsset font) => material == font.material;

        private static TMP_FontAsset FontOf(Material material)
        {
            if (material == null || !material.HasProperty("_MainTex")) return null;
            var atlas = material.GetTexture("_MainTex");
            foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (font != null && font.atlasTexture == atlas) return font;
            }
            return null;
        }

        /// <summary>
        /// Đường dẫn từ root (gồm cả root) theo tên và thứ tự anh em: tên trùng trong cùng cha là chuyện thường ở prefab
        /// UI, còn scene có nhiều root.
        /// </summary>
        private static string PathOf(Transform root, Transform target)
        {
            var parts = new List<string>();
            for (var t = target; t != null; t = t.parent)
            {
                parts.Insert(0, t.name + "#" + t.GetSiblingIndex());
                if (t == root) break;
            }
            return string.Join("/", parts);
        }

        /// <summary>Null khi đường dẫn không thuộc root này hoặc cây đã đổi.</summary>
        private static Transform Find(Transform root, string path)
        {
            var parts = path.Split('/');
            if (!Matches(root, parts[0])) return null;
            var current = root;
            for (var i = 1; i < parts.Length; i++)
            {
                var index = int.Parse(parts[i].Substring(parts[i].LastIndexOf('#') + 1));
                if (index >= current.childCount || !Matches(current.GetChild(index), parts[i])) return null;
                current = current.GetChild(index);
            }
            return current;
        }

        private static bool Matches(Transform t, string part) => part == t.name + "#" + t.GetSiblingIndex();

        private static LookTable Load(string jsonPath) => JsonUtility.FromJson<LookTable>(File.ReadAllText(jsonPath));

        /// <summary>Prefab: mở nội dung. Scene: mở cộng thêm rồi đóng nếu trước đó chưa mở. Mỗi root của scene gọi một lần.</summary>
        private static void ForEachRoot(string assetPath, bool save, Action<GameObject> visit)
        {
            if (assetPath.EndsWith(".prefab"))
            {
                var root = PrefabUtility.LoadPrefabContents(assetPath);
                try
                {
                    visit(root);
                    if (save) PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                return;
            }

            var scene = SceneManager.GetSceneByPath(assetPath);
            var openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(assetPath, OpenSceneMode.Additive);
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                    visit(root);
                if (save) EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        #endregion
    }
}
