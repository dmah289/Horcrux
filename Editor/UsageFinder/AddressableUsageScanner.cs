using System;
using System.Collections.Generic;
using Horcrux.Editor.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Horcrux.Editor.UsageFinder
{
    /// <summary>
    /// Tab "Addressable Usages" — tìm mọi <c>AssetReference</c> (Addressables) đang trỏ tới target.
    ///
    /// ⚠️ Bắt buộc là cơ chế riêng: <c>AssetReference</c> lưu qua <c>m_AssetGUID</c> và KHÔNG phải
    /// hard dependency → không nằm trong dependency graph, <see cref="AssetReferenceIndex"/> bỏ sót.
    /// Ta quét thẳng serialized data toàn project qua <see cref="SerializedPropertyWalker"/>.
    ///
    /// Phạm vi: Prefab + mọi file .asset (main + sub-asset) toàn project + các Scene đang mở.
    /// Quét cả sub-asset vì collection ScriptableObject dùng cho Addressable thường gom AssetReference
    /// vào các SO con nested bên trong một .asset. (Quét mọi scene trên disk phải load/unload —
    /// destructive — nên để ngoài phạm vi.)
    /// </summary>
    public static class AddressableUsageScanner
    {
        // ──────────────── Reusable buffer (non-alloc) ────────────────

        private static readonly List<MonoBehaviour> BehaviourBuffer = new(64);

        // Dedupe path giữa 2 nguồn FindAssets (t:ScriptableObject ∪ mọi .asset) — reuse giữa các lần scan.
        private static readonly HashSet<string> AssetPathSet = new(512);

        // ──────────────── Visitor: match m_AssetGUID == targetGuid ────────────────

        private struct AssetRefMatchVisitor : IReferencePropertyVisitor
        {
            public string        TargetGuid;
            public string        OwnerLabel;   // "ComponentName" hoặc SO type — để build detail label
            public List<string>  Matches;      // detail labels tích lũy; lazy alloc

            public void Visit(SerializedObject so, SerializedProperty p, RefPropertyKind kind)
            {
                if (kind != RefPropertyKind.AssetReference)
                    return;

                SerializedProperty guid = p.FindPropertyRelative("m_AssetGUID");
                if (guid == null || guid.stringValue != TargetGuid)
                    return;

                Matches ??= new List<string>();

                var sb = UsageStringHelper.SB;
                sb.Clear();
                sb.Append(OwnerLabel).Append(" > ")
                  .Append(SerializedPropertyWalker.BuildDisplayPath(so, p.propertyPath));
                Matches.Add(sb.ToString());
            }
        }

        // ──────────────── Public entry ────────────────

        /// <summary>
        /// Tìm mọi asset/scene chứa AssetReference trỏ tới <paramref name="target"/>.
        /// <paramref name="cancelled"/> = true nếu user hủy giữa chừng → <paramref name="results"/>
        /// chỉ là một phần; caller KHÔNG được kết luận "không ai dùng" khi cancelled.
        /// </summary>
        public static List<UsageEntry> Scan(Object target, out bool cancelled)
        {
            cancelled = false;
            var results = new List<UsageEntry>();
            if (target == null)
                return results;

            string path = AssetDatabase.GetAssetPath(target);
            if (string.IsNullOrEmpty(path))
                return results;

            string targetGuid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(targetGuid))
                return results;

            try
            {
                // Short-circuit: hủy ở phase nào thì dừng luôn, không chạy phase sau (ScanAssetFiles
                // là phase nặng nhất — user bấm Cancel phải dừng thật, không chờ nốt).
                if (ScanPrefabs(targetGuid, results))    { cancelled = true; return results; }
                if (ScanAssetFiles(targetGuid, results)) { cancelled = true; return results; }
                if (ScanOpenScenes(targetGuid, results)) { cancelled = true; return results; }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return results;
        }

        // ──────────────── Prefabs ────────────────

        /// <summary>Trả true nếu user hủy.</summary>
        private static bool ScanPrefabs(string targetGuid, List<UsageEntry> results)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (ReportProgress("Scanning prefabs", assetPath, i, guids.Length))
                    return true;

                var go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (go == null) continue;

                List<string> matches = ScanGameObjectHierarchy(go, targetGuid);
                if (matches != null)
                    results.Add(new UsageEntry(assetPath, go, matches));
            }
            return false;
        }

        // ──────────────── Asset files (.asset — main + sub-asset) ────────────────

        /// <summary>
        /// Quét mọi file .asset toàn project, walk cả main asset lẫn sub-asset (nested).
        /// Gộp 2 nguồn để không lọt: FindAssets("t:ScriptableObject") (bắt SO subclass ở mọi extension)
        /// ∪ mọi file *.asset qua GetAllAssetPaths (bắt container mà main asset không phải SO
        /// nhưng chứa sub-asset SO). AssetReference (Addressables) chỉ khai báo trong class C# →
        /// chỉ nằm trong Object managed (SO/MonoBehaviour), nên bỏ qua asset không load được là an toàn.
        /// </summary>
        /// <summary>Trả true nếu user hủy.</summary>
        private static bool ScanAssetFiles(string targetGuid, List<UsageEntry> results)
        {
            AssetPathSet.Clear();
            CollectByType("t:ScriptableObject", AssetPathSet);  // SO subclass ở mọi extension
            CollectDotAssetFiles(AssetPathSet);                 // mọi file .asset (bắt cả container)

            int i = 0;
            int total = AssetPathSet.Count;
            foreach (string assetPath in AssetPathSet)
            {
                if (ReportProgress("Scanning assets", assetPath, i++, total))
                    return true;

                // Load tất cả object trong file (main + sub-asset) — một lần I/O cho cả file.
                Object[] all = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                if (all == null || all.Length == 0) continue;

                List<string> matches = null;
                for (int a = 0; a < all.Length; a++)
                {
                    Object obj = all[a];
                    // Chỉ Object managed mới có thể khai báo AssetReference field.
                    if (obj is not (ScriptableObject or MonoBehaviour)) continue;

                    matches = ScanObject(obj, BuildAssetOwnerLabel(obj), targetGuid, matches);
                }

                if (matches != null)
                {
                    Object main = AssetDatabase.LoadMainAssetAtPath(assetPath);
                    results.Add(new UsageEntry(assetPath, main, matches));
                }
            }
            return false;
        }

        /// <summary>Chuyển kết quả FindAssets(filter) thành path, gộp vào set (dedupe).</summary>
        private static void CollectByType(string filter, HashSet<string> into)
        {
            string[] guids = AssetDatabase.FindAssets(filter);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!string.IsNullOrEmpty(path))
                    into.Add(path);
            }
        }

        /// <summary>
        /// Mọi file .asset trong Assets/ — không lệ thuộc search index. Bắt cả container mà
        /// main asset không phải SO nhưng chứa sub-asset SO (t:ScriptableObject có thể bỏ sót).
        /// </summary>
        private static void CollectDotAssetFiles(HashSet<string> into)
        {
            string[] all = AssetDatabase.GetAllAssetPaths();
            for (int i = 0; i < all.Length; i++)
            {
                string path = all[i];
                if (path.StartsWith("Assets/", StringComparison.Ordinal)
                    && path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                {
                    into.Add(path);
                }
            }
        }

        /// <summary>Nhãn owner cho sub-asset: "AssetName (Type)" — phân biệt SO con trong cùng file.</summary>
        private static string BuildAssetOwnerLabel(Object obj)
        {
            var sb = UsageStringHelper.SB;
            sb.Clear();
            sb.Append(string.IsNullOrEmpty(obj.name) ? obj.GetType().Name : obj.name)
              .Append(" (").Append(obj.GetType().Name).Append(')');
            return sb.ToString();
        }

        // ──────────────── Open scenes ────────────────

        /// <summary>Không có progress/cancel (thao tác nhanh trên scene đã load) → luôn trả false.</summary>
        private static bool ScanOpenScenes(string targetGuid, List<UsageEntry> results)
        {
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;

                List<string> sceneMatches = null;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int r = 0; r < roots.Length; r++)
                {
                    List<string> m = ScanGameObjectHierarchy(roots[r], targetGuid);
                    if (m != null)
                    {
                        sceneMatches ??= new List<string>();
                        sceneMatches.AddRange(m);
                    }
                }

                if (sceneMatches != null)
                {
                    // scene.path rỗng nếu scene chưa lưu → dùng nhãn tạm
                    string scenePath = string.IsNullOrEmpty(scene.path) ? scene.name : scene.path;
                    Object sceneAsset = string.IsNullOrEmpty(scene.path)
                        ? null
                        : AssetDatabase.LoadAssetAtPath<Object>(scene.path);
                    results.Add(new UsageEntry(scenePath, sceneAsset, sceneMatches));
                }
            }
            return false;
        }

        // ──────────────── Traversal helpers ────────────────

        /// <summary>Quét mọi MonoBehaviour trên GO + con cháu. Trả matches (null nếu không có).</summary>
        private static List<string> ScanGameObjectHierarchy(GameObject root, string targetGuid)
        {
            List<string> matches = null;
            // Non-alloc: fill vào buffer dùng chung (bao gồm inactive). Không đệ quy nên an toàn reuse.
            BehaviourBuffer.Clear();
            root.GetComponentsInChildren(true, BehaviourBuffer);
            for (int i = 0; i < BehaviourBuffer.Count; i++)
            {
                MonoBehaviour mb = BehaviourBuffer[i];
                if (mb == null) continue; // missing script

                string ownerLabel = BuildOwnerLabel(mb);
                List<string> m = ScanObject(mb, ownerLabel, targetGuid, matches);
                if (m != null) matches = m;
            }
            return matches;
        }

        /// <summary>Walk 1 Object qua visitor; gộp matches vào <paramref name="accumulator"/>.</summary>
        private static List<string> ScanObject(Object obj, string ownerLabel, string targetGuid,
                                                List<string> accumulator)
        {
            using var so = new SerializedObject(obj);
            var visitor = new AssetRefMatchVisitor { TargetGuid = targetGuid, OwnerLabel = ownerLabel };
            SerializedPropertyWalker.Walk(so, ref visitor);

            if (visitor.Matches == null)
                return accumulator;

            if (accumulator == null)
                return visitor.Matches;

            accumulator.AddRange(visitor.Matches);
            return accumulator;
        }

        /// <summary>"GOName/ChildName (ComponentType)" để định vị trong prefab/scene.</summary>
        private static string BuildOwnerLabel(MonoBehaviour mb)
        {
            var sb = UsageStringHelper.SB;
            sb.Clear();
            sb.Append(mb.gameObject.name).Append(" (").Append(mb.GetType().Name).Append(')');
            return sb.ToString();
        }

        // ──────────────── Progress ────────────────

        /// <summary>Trả true nếu user cancel.</summary>
        private static bool ReportProgress(string title, string info, int i, int total)
        {
            if ((i & 0x1F) != 0) return false; // update mỗi 32 item
            return EditorUtility.DisplayCancelableProgressBar(title, info,
                total == 0 ? 1f : (float)i / total);
        }
    }
}
