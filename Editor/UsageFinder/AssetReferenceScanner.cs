using System;
using System.Collections.Generic;
using System.IO;
using Horcrux.Editor.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Horcrux.Editor.UsageFinder
{
    /// <summary>
    /// Tìm MỌI field trong project trỏ tới một ASSET target — không sót case nào (list, object
    /// nested trong list, AssetReference, ObjectReference, ExposedReference, [SerializeReference]).
    ///
    /// Cơ chế "cách B" — grep GUID trong text file, 2 pha:
    ///  • Pha 1 (nhanh, chỉ đọc text): quét text mọi asset YAML tìm chuỗi targetGuid → CANDIDATE.
    ///    Vì Unity serialize ForceText, MỌI reference (kể cả AssetReference m_AssetGUID và phần tử
    ///    list nested) đều ghi guid target dạng text → pha 1 là lưới an toàn, độc lập với việc
    ///    SerializedPropertyWalker có nhận diện đúng kind hay không.
    ///  • Pha 2 (chỉ candidate): load + walk SerializedProperty → field path clickable + navigation.
    ///    Nếu pha 2 không map được ra field (kind lạ / GUID nằm ngoài field walk tới) → fallback
    ///    1 hit mức-file để KHÔNG bao giờ mất một hit của pha 1.
    ///
    /// Scene đang mở: walk trực tiếp hierarchy (chính xác cả khi có thay đổi chưa lưu).
    /// Scene file trên disk (chưa mở): chỉ tạo hit mức-file; mở scene + định vị khi user click.
    /// </summary>
    public static class AssetReferenceScanner
    {
        // ──────────────── Extensions có thể chứa reference (YAML asset) ────────────────

        private static readonly HashSet<string> ReferenceCarryingExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".prefab", ".unity", ".asset", ".mat", ".anim", ".controller", ".overridecontroller",
            ".playable", ".preset", ".mask", ".physicmaterial", ".physicsmaterial2d", ".spriteatlas",
            ".spriteatlasv2", ".terrainlayer", ".shadervariants", ".lighting", ".giparams",
            ".rendertexture", ".flare", ".fontsettings", ".guiskin", ".mixer", ".brush", ".signal",
            ".scenetemplate", ".cubemap",
        };

        // ──────────────── Reusable buffers (non-alloc) ────────────────

        private static readonly List<string>        CandidateBuffer = new(256);
        private static readonly List<Component>      ComponentBuffer = new(64);
        private static readonly HashSet<string>      OpenScenePaths  = new();

        // ──────────────── Visitor: build field hit khi property khớp targetGuid ────────────────

        private struct FieldHitVisitor : IReferencePropertyVisitor
        {
            public string             TargetGuid;
            public string             OwnerLabel;
            public UsageNavKind       NavKind;
            public Object             NavObject;
            public Component          NavComponent;
            public List<UsageFieldHit> Hits;   // lazy alloc

            public void Visit(SerializedObject so, SerializedProperty p, RefPropertyKind kind)
            {
                if (!MatchesTarget(p, kind, TargetGuid))
                    return;

                Hits ??= new List<UsageFieldHit>();
                Hits.Add(new UsageFieldHit(
                    NavKind,
                    OwnerLabel,
                    SerializedPropertyWalker.BuildDisplayPath(so, p.propertyPath),
                    p.propertyPath,
                    NavObject,
                    NavComponent));
            }
        }

        /// <summary>Property này có thực sự trỏ tới targetGuid không (theo từng kind).</summary>
        private static bool MatchesTarget(SerializedProperty p, RefPropertyKind kind, string targetGuid)
        {
            switch (kind)
            {
                case RefPropertyKind.AssetReference:
                    SerializedProperty g = p.FindPropertyRelative("m_AssetGUID");
                    return g != null && g.stringValue == targetGuid;

                case RefPropertyKind.ObjectReference:
                    return ObjectRefGuid(p.objectReferenceValue) == targetGuid;

                case RefPropertyKind.ExposedReference:
                    SerializedProperty dv = p.FindPropertyRelative("defaultValue");
                    return dv != null && ObjectRefGuid(dv.objectReferenceValue) == targetGuid;

                // ManagedReference: walker đã descend vào field con → so từng field con riêng, không so ở đây.
                default:
                    return false;
            }
        }

        /// <summary>GUID của asset mà <paramref name="o"/> thuộc về (null nếu không phải asset / null).</summary>
        private static string ObjectRefGuid(Object o)
        {
            if (o == null)
                return null;
            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out string guid, out long _)
                ? guid
                : null;
        }

        // ──────────────── Public entry ────────────────

        /// <summary>
        /// Tìm mọi asset/scene chứa field trỏ tới <paramref name="target"/>.
        /// <paramref name="cancelled"/> = true nếu user hủy → kết quả một phần; caller KHÔNG được
        /// kết luận "không ai dùng".
        /// </summary>
        public static List<UsageEntry> Scan(Object target, out bool cancelled)
        {
            cancelled = false;
            var results = new List<UsageEntry>();
            if (target == null)
                return results;

            string targetPath = AssetDatabase.GetAssetPath(target);
            if (string.IsNullOrEmpty(targetPath))
                return results;

            string targetGuid = AssetDatabase.AssetPathToGUID(targetPath);
            if (string.IsNullOrEmpty(targetGuid))
                return results;

            // Set path của scene đang mở → tránh double-count với disk grep (live walk chính xác hơn).
            OpenScenePaths.Clear();
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene sc = SceneManager.GetSceneAt(s);
                if (sc.isLoaded && !string.IsNullOrEmpty(sc.path))
                    OpenScenePaths.Add(sc.path);
            }

            try
            {
                // Pha 1 + Pha 2 cho asset/scene-file trên disk.
                if (CollectCandidates(targetGuid, targetPath, CandidateBuffer)) { cancelled = true; return results; }
                if (ExtractHits(targetGuid, CandidateBuffer, results))          { cancelled = true; return results; }

                // Scene đang mở: walk live (field-level clickable, chính xác cả khi chưa lưu).
                if (ScanOpenScenes(targetGuid, results)) { cancelled = true; return results; }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return results;
        }

        // ──────────────── Pha 1 — candidate discovery (text grep) ────────────────

        /// <summary>Trả true nếu user hủy.</summary>
        private static bool CollectCandidates(string targetGuid, string targetPath, List<string> into)
        {
            into.Clear();
            string[] all = AssetDatabase.GetAllAssetPaths();
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;

            for (int i = 0; i < all.Length; i++)
            {
                string path = all[i];

                if ((i & 0x3F) == 0 && EditorUtility.DisplayCancelableProgressBar(
                        "Finding usages — scanning files", path, (float)i / all.Length))
                    return true;

                if (!IsScannableAsset(path) || path == targetPath)
                    continue;

                // Đọc text thô, tìm GUID target ở BẤT KỲ đâu (guid:, m_AssetGUID:, nested list...).
                // Over-match (vd guid trùng trong chuỗi lạ) là an toàn — pha 2 lọc lại.
                string full = Path.Combine(projectRoot, path);
                try
                {
                    string text = File.ReadAllText(full);
                    if (text.IndexOf(targetGuid, StringComparison.Ordinal) >= 0)
                        into.Add(path);
                }
                catch
                {
                    // File bị khóa / không đọc được → bỏ qua (hiếm). Không làm hỏng cả lần scan.
                }
            }

            return false;
        }

        private static bool IsScannableAsset(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal))
                return false;
            string ext = Path.GetExtension(path);
            return !string.IsNullOrEmpty(ext) && ReferenceCarryingExtensions.Contains(ext);
        }

        // ──────────────── Pha 2 — detail extraction (walk candidate) ────────────────

        /// <summary>Trả true nếu user hủy.</summary>
        private static bool ExtractHits(string targetGuid, List<string> candidates, List<UsageEntry> results)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                string path = candidates[i];

                if ((i & 0x0F) == 0 && EditorUtility.DisplayCancelableProgressBar(
                        "Finding usages — resolving fields", path, (float)i / candidates.Count))
                    return true;

                string ext = Path.GetExtension(path);

                if (ext.Equals(".unity", StringComparison.OrdinalIgnoreCase))
                {
                    // Scene đang mở → live walk lo (ScanOpenScenes); chỉ xử lý scene disk chưa mở ở đây.
                    if (!OpenScenePaths.Contains(path))
                        AddDiskSceneHit(path, targetGuid, results);
                    continue;
                }

                if (ext.Equals(".prefab", StringComparison.OrdinalIgnoreCase))
                    ExtractFromPrefab(path, targetGuid, results);
                else
                    ExtractFromAssetFile(path, targetGuid, results);
            }

            return false;
        }

        private static void AddDiskSceneHit(string scenePath, string targetGuid, List<UsageEntry> results)
        {
            var hits = new List<UsageFieldHit>(1)
            {
                new UsageFieldHit(scenePath, targetGuid,
                    "🎬 Scene chứa reference — click để mở scene & định vị field"),
            };
            Object sceneAsset = AssetDatabase.LoadAssetAtPath<Object>(scenePath);
            results.Add(new UsageEntry(scenePath, sceneAsset, hits));
        }

        private static void ExtractFromPrefab(string path, string targetGuid, List<UsageEntry> results)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null)
            {
                AddFallbackHit(path, targetGuid, results);
                return;
            }

            List<UsageFieldHit> hits = WalkHierarchy(go, targetGuid, UsageNavKind.AssetField);
            AddEntry(path, go, hits, targetGuid, results);
        }

        private static void ExtractFromAssetFile(string path, string targetGuid, List<UsageEntry> results)
        {
            Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
            List<UsageFieldHit> hits = null;

            if (all != null)
            {
                for (int a = 0; a < all.Length; a++)
                {
                    Object obj = all[a];
                    if (obj == null)
                        continue;
                    // Walk MỌI loại Object (SO, Material, AnimationClip, mixer,...) — không chỉ SO/MonoBehaviour,
                    // vì reference cần bắt gồm cả ObjectReference thường (vd Material → Texture/Shader).
                    // GameObject/Component đã đi qua đường prefab (ExtractFromPrefab) → bỏ ở đây tránh trùng.
                    if (obj is GameObject or Component)
                        continue;

                    hits = WalkObject(obj, BuildAssetOwnerLabel(obj), targetGuid,
                                      UsageNavKind.AssetField, obj, null, hits);
                }
            }

            Object main = AssetDatabase.LoadMainAssetAtPath(path);
            AddEntry(path, main, hits, targetGuid, results);
        }

        /// <summary>Thêm entry; nếu walk không ra hit nhưng pha 1 đã khớp → fallback mức-file (không mất hit).</summary>
        private static void AddEntry(string path, Object main, List<UsageFieldHit> hits,
                                     string targetGuid, List<UsageEntry> results)
        {
            if (hits == null || hits.Count == 0)
            {
                AddFallbackHit(path, targetGuid, results);
                return;
            }
            results.Add(new UsageEntry(path, main, hits));
        }

        private static void AddFallbackHit(string path, string targetGuid, List<UsageEntry> results)
        {
            Object main = AssetDatabase.LoadMainAssetAtPath(path);
            var hits = new List<UsageFieldHit>(1)
            {
                new UsageFieldHit(UsageNavKind.AssetField,
                    "⚠️ Chứa reference (không xác định được field cụ thể)", "", "", main, null),
            };
            results.Add(new UsageEntry(path, main, hits));
        }

        // ──────────────── Open scenes (live walk) ────────────────

        /// <summary>Trả true nếu user hủy.</summary>
        private static bool ScanOpenScenes(string targetGuid, List<UsageEntry> results)
        {
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;

                List<UsageFieldHit> sceneHits = null;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int r = 0; r < roots.Length; r++)
                {
                    List<UsageFieldHit> m = WalkHierarchy(roots[r], targetGuid, UsageNavKind.OpenSceneField);
                    if (m != null)
                    {
                        sceneHits ??= new List<UsageFieldHit>();
                        sceneHits.AddRange(m);
                    }
                }

                if (sceneHits != null)
                {
                    string scenePath = string.IsNullOrEmpty(scene.path) ? scene.name : scene.path;
                    Object sceneAsset = string.IsNullOrEmpty(scene.path)
                        ? null
                        : AssetDatabase.LoadAssetAtPath<Object>(scene.path);
                    results.Add(new UsageEntry(scenePath, sceneAsset, sceneHits));
                }
            }

            return false;
        }

        // ──────────────── Walk helpers ────────────────

        /// <summary>Walk mọi component (kể cả native — MeshRenderer.sharedMaterial,...) trên GO + con cháu.</summary>
        private static List<UsageFieldHit> WalkHierarchy(GameObject root, string targetGuid, UsageNavKind navKind)
        {
            List<UsageFieldHit> hits = null;
            ComponentBuffer.Clear();
            root.GetComponentsInChildren(true, ComponentBuffer);
            for (int i = 0; i < ComponentBuffer.Count; i++)
            {
                Component c = ComponentBuffer[i];
                if (c == null) continue; // missing script

                hits = WalkObject(c, BuildComponentOwnerLabel(c), targetGuid,
                                  navKind, c.gameObject, c, hits);
            }
            return hits;
        }

        /// <summary>Walk 1 Object qua visitor; gộp hit vào <paramref name="accumulator"/>.</summary>
        private static List<UsageFieldHit> WalkObject(Object obj, string ownerLabel, string targetGuid,
                                                      UsageNavKind navKind, Object navObject,
                                                      Component navComponent, List<UsageFieldHit> accumulator)
        {
            using var so = new SerializedObject(obj);
            var visitor = new FieldHitVisitor
            {
                TargetGuid   = targetGuid,
                OwnerLabel   = ownerLabel,
                NavKind      = navKind,
                NavObject    = navObject,
                NavComponent = navComponent,
            };
            SerializedPropertyWalker.Walk(so, ref visitor);

            if (visitor.Hits == null)
                return accumulator;
            if (accumulator == null)
                return visitor.Hits;

            accumulator.AddRange(visitor.Hits);
            return accumulator;
        }

        private static string BuildComponentOwnerLabel(Component c)
        {
            var sb = UsageStringHelper.SB;
            sb.Clear();
            sb.Append(c.gameObject.name).Append(" (").Append(c.GetType().Name).Append(')');
            return sb.ToString();
        }

        private static string BuildAssetOwnerLabel(Object obj)
        {
            var sb = UsageStringHelper.SB;
            sb.Clear();
            sb.Append(string.IsNullOrEmpty(obj.name) ? obj.GetType().Name : obj.name)
              .Append(" (").Append(obj.GetType().Name).Append(')');
            return sb.ToString();
        }

        // ──────────────── Disk-scene navigation (click DiskScene hit) ────────────────

        /// <summary>
        /// Mở scene file trên disk (hỏi lưu scene hiện tại nếu dirty), rồi walk tìm GameObject đầu tiên
        /// có field trỏ tới <paramref name="targetGuid"/> → select + expand component. Chạy khi user click.
        /// </summary>
        public static void OpenDiskSceneAndLocate(string scenePath, string targetGuid)
        {
            if (string.IsNullOrEmpty(scenePath)) return;
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return; // user cancel dialog lưu

            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            if (!scene.IsValid()) return;

            EditorApplication.delayCall += () =>
            {
                GameObject[] roots = scene.GetRootGameObjects();
                for (int r = 0; r < roots.Length; r++)
                {
                    Component comp = FindFirstReferencingComponent(roots[r], targetGuid, out string propertyPath);
                    if (comp != null)
                    {
                        NavigationHelper.SelectAndPingProperty(comp.gameObject, comp, propertyPath);
                        return;
                    }
                }
            };
        }

        /// <summary>Component đầu tiên trong hierarchy có field trỏ tới targetGuid (null nếu không có).</summary>
        private static Component FindFirstReferencingComponent(GameObject root, string targetGuid,
                                                               out string propertyPath)
        {
            propertyPath = null;
            ComponentBuffer.Clear();
            root.GetComponentsInChildren(true, ComponentBuffer);
            for (int i = 0; i < ComponentBuffer.Count; i++)
            {
                Component c = ComponentBuffer[i];
                if (c == null) continue;

                using var so = new SerializedObject(c);
                var visitor = new FieldHitVisitor
                {
                    TargetGuid = targetGuid,
                    OwnerLabel = "",
                    NavKind    = UsageNavKind.OpenSceneField,
                    NavObject  = c.gameObject,
                    NavComponent = c,
                };
                SerializedPropertyWalker.Walk(so, ref visitor);
                if (visitor.Hits != null && visitor.Hits.Count > 0)
                {
                    propertyPath = visitor.Hits[0].propertyPath;
                    return c;
                }
            }
            return null;
        }
    }
}
