using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Horcrux.Editor.Common
{
    /// <summary>
    /// Reverse dependency index — trả lời "asset nào đang tham chiếu targetGuid".
    ///
    /// Unity chỉ cung cấp forward map (<c>AssetDatabase.GetDependencies</c> = "asset này dùng gì").
    /// Index này đảo ngược forward map của toàn project một lần, cache ra Library/, và giữ đồng bộ
    /// bằng incremental update qua <see cref="AssetRefIndexPostprocessor"/> — không full rebuild mỗi lần save.
    ///
    /// SRP: chỉ duy trì reverse map GUID → referencers. Không biết UI, không xử lý AssetReference
    /// (Addressables không nằm trong dependency graph — đó là việc của AssetReferenceScanner, Usage Finder).
    ///
    /// ⚠️ Chỉ index reference dạng hard-dependency (direct, recursive:false). AssetReference
    /// (m_AssetGUID) KHÔNG xuất hiện ở đây.
    /// </summary>
    [InitializeOnLoad]
    public static class AssetReferenceIndex
    {
        // ──────────────── Constants ────────────────

        private const int    FormatVersion = 1;
        private const string AssetsPrefix  = "Assets/";

        // ──────────────── State ────────────────

        // forward: referencerPath → direct dependency GUIDs (dùng để patch/gỡ chính xác khi asset đổi)
        private static readonly Dictionary<string, string[]> _forward = new(2048);

        // reverse: targetGuid → referencer paths (kết quả query — derive từ _forward)
        private static readonly Dictionary<string, List<string>> _reverse = new(2048);

        private static bool _built;
        private static bool _cacheDirty;
        private static bool _saveScheduled;

        private static readonly IReadOnlyList<string> EmptyList = Array.Empty<string>();

        // Reusable buffer cho việc thu thập dep guids khi quét 1 asset (non-alloc giữa các lần gọi)
        private static readonly List<string> DepGuidBuffer = new(16);

        // ──────────────── Cache file path ────────────────

        private static string CacheDir =>
            Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "Library", "Horcrux");

        private static string CachePath => Path.Combine(CacheDir, "AssetRefIndex.json");

        // ──────────────── Static ctor ────────────────

        static AssetReferenceIndex()
        {
            // Không build khi Editor khởi động (tránh chậm startup). Chỉ thử load cache;
            // nếu không có/lỗi → build lazy ở query đầu tiên.
            TryLoadCache();
        }

        // ──────────────── Public API ────────────────

        public static bool IsBuilt => _built;

        /// <summary>
        /// Build nếu chưa sẵn sàng; no-op nếu đã built.
        /// Trả <c>true</c> nếu index sẵn sàng dùng, <c>false</c> nếu build bị user hủy giữa chừng
        /// (khi đó index rỗng — caller KHÔNG được diễn giải query rỗng là "không ai tham chiếu").
        /// </summary>
        public static bool EnsureBuilt()
        {
            if (_built)
                return true;
            return BuildFull(showProgress: true);
        }

        /// <summary>
        /// Force full rebuild (nút "Rebuild Index" trên UI, hoặc khi cache stale).
        /// Trả <c>true</c> nếu hoàn tất, <c>false</c> nếu user hủy.
        /// </summary>
        public static bool Rebuild(bool showProgress = true)
        {
            return BuildFull(showProgress);
        }

        /// <summary>
        /// Core query — referencer paths của <paramref name="targetGuid"/>.
        /// Trả list rỗng (không alloc) nếu không ai tham chiếu.
        ///
        /// ⚠️ Rỗng CHỈ đáng tin khi <see cref="IsBuilt"/> == true. Nếu build bị hủy giữa chừng,
        /// query cũng trả rỗng — kiểm <see cref="IsBuilt"/> (hoặc dùng giá trị trả về của
        /// <see cref="EnsureBuilt"/>) trước khi kết luận "không ai dùng".
        ///
        /// ⚠️ Trả trực tiếp list nội bộ (không copy) để zero-alloc. Caller CHỈ được đọc, không sửa,
        /// và không giữ tham chiếu qua nhiều frame — postprocessor có thể mutate list này khi asset đổi.
        /// </summary>
        public static IReadOnlyList<string> GetReferencers(string targetGuid)
        {
            EnsureBuilt();
            if (string.IsNullOrEmpty(targetGuid))
                return EmptyList;
            return _reverse.TryGetValue(targetGuid, out List<string> list) ? list : EmptyList;
        }

        public static int ReferencerCount(string targetGuid)
        {
            EnsureBuilt();
            if (string.IsNullOrEmpty(targetGuid))
                return 0;
            return _reverse.TryGetValue(targetGuid, out List<string> list) ? list.Count : 0;
        }

        // ──────────────── Full build ────────────────

        /// <summary>Trả <c>true</c> nếu build xong, <c>false</c> nếu user hủy (index để rỗng, _built=false).</summary>
        private static bool BuildFull(bool showProgress)
        {
            _forward.Clear();
            _reverse.Clear();

            string[] allPaths = AssetDatabase.GetAllAssetPaths();
            try
            {
                for (int i = 0; i < allPaths.Length; i++)
                {
                    string path = allPaths[i];
                    if (!IsProjectAsset(path))
                        continue;

                    if (showProgress && (i & 0x7F) == 0) // update mỗi 128 asset — tránh spam progress bar
                    {
                        bool cancel = EditorUtility.DisplayCancelableProgressBar(
                            "Building Asset Reference Index",
                            path,
                            (float)i / allPaths.Length);
                        if (cancel)
                        {
                            _forward.Clear();
                            _reverse.Clear();
                            _built = false;
                            return false;
                        }
                    }

                    IndexAsset(path);
                }
            }
            finally
            {
                if (showProgress)
                    EditorUtility.ClearProgressBar();
            }

            _built = true;
            MarkCacheDirty();
            return true;
        }

        /// <summary>
        /// Quét 1 asset → ghi forward entry + cập nhật reverse (dùng cho build & incremental).
        /// Idempotent: tự gỡ entry cũ trước khi ghi lại → an toàn gọi nhiều lần cho cùng path.
        /// Nhờ đó giữ invariant "mỗi (path, targetGuid) chỉ tồn tại 1 lần trong reverse",
        /// cho phép <see cref="AddReverse"/> bỏ check duplicate (tránh O(n²) trên target phổ biến).
        /// </summary>
        private static void IndexAsset(string path)
        {
            RemoveReferencer(path); // đảm bảo idempotent (no-op nếu path chưa có — O(1))

            DepGuidBuffer.Clear();
            string[] deps = AssetDatabase.GetDependencies(path, recursive: false);
            for (int d = 0; d < deps.Length; d++)
            {
                string dep = deps[d];
                if (dep == path) // self-dependency
                    continue;

                string depGuid = AssetDatabase.AssetPathToGUID(dep);
                if (string.IsNullOrEmpty(depGuid))
                    continue;

                DepGuidBuffer.Add(depGuid);
                AddReverse(depGuid, path);
            }

            _forward[path] = DepGuidBuffer.ToArray();
        }

        // ──────────────── Incremental patch (called by postprocessor) ────────────────

        /// <summary>Quét lại dependency của path và cập nhật index. O(k). IndexAsset tự gỡ entry cũ.</summary>
        internal static void RefreshReferencer(string path)
        {
            if (!_built) return;
            if (!IsProjectAsset(path)) return;

            IndexAsset(path);
            MarkCacheDirty();
        }

        /// <summary>Asset bị xóa: gỡ nó khỏi mọi reverse list + xóa reverse entry của chính nó (nếu là target).</summary>
        internal static void RemoveAsset(string path, string deletedGuid)
        {
            if (!_built) return;

            RemoveReferencer(path);

            if (!string.IsNullOrEmpty(deletedGuid))
                _reverse.Remove(deletedGuid); // không còn ai có thể tham chiếu asset đã biến mất

            MarkCacheDirty();
        }

        /// <summary>Asset di chuyển: remap path trong forward key + trong mọi reverse list chứa path cũ.</summary>
        internal static void MoveReferencer(string fromPath, string toPath)
        {
            if (!_built) return;

            if (_forward.TryGetValue(fromPath, out string[] deps))
            {
                _forward.Remove(fromPath);
                _forward[toPath] = deps;

                // Cập nhật path trong reverse list của từng target mà nó trỏ tới
                for (int i = 0; i < deps.Length; i++)
                {
                    if (_reverse.TryGetValue(deps[i], out List<string> list))
                    {
                        int idx = list.IndexOf(fromPath);
                        if (idx >= 0) list[idx] = toPath;
                    }
                }
            }
            else if (IsProjectAsset(toPath))
            {
                // Chưa có trong forward (vd path cũ ngoài Assets/) → index như asset mới
                IndexAsset(toPath);
            }

            MarkCacheDirty();
        }

        // ──────────────── Reverse map helpers ────────────────

        private static void AddReverse(string targetGuid, string referencerPath)
        {
            if (!_reverse.TryGetValue(targetGuid, out List<string> list))
            {
                list = new List<string>(2); // đa số target có ít referencer
                _reverse[targetGuid] = list;
            }
            // Không check duplicate: IndexAsset idempotent + DepGuidBuffer đã unique theo path
            // (deps từ GetDependencies không lặp) → mỗi (path,guid) chỉ add đúng 1 lần. Tránh O(n²).
            list.Add(referencerPath);
        }

        /// <summary>Gỡ <paramref name="path"/> khỏi reverse list của mọi target nó từng trỏ (dùng forward map).</summary>
        private static void RemoveReferencer(string path)
        {
            if (!_forward.TryGetValue(path, out string[] oldDeps))
                return;

            for (int i = 0; i < oldDeps.Length; i++)
            {
                if (_reverse.TryGetValue(oldDeps[i], out List<string> list))
                {
                    list.Remove(path);
                    if (list.Count == 0)
                        _reverse.Remove(oldDeps[i]);
                }
            }
            _forward.Remove(path);
        }

        // ──────────────── Path filter ────────────────

        private static bool IsProjectAsset(string path)
        {
            // Chỉ index Assets/ — bỏ Packages/, ProjectSettings/,... Bỏ folder (không có extension).
            return !string.IsNullOrEmpty(path)
                   && path.StartsWith(AssetsPrefix, StringComparison.Ordinal)
                   && Path.HasExtension(path);
        }

        // ──────────────── Persistence (JSON — chỉ lưu forward, derive reverse khi load) ────────────────

        // Newtonsoft serialize thẳng Dictionary → không cần struct/class trung gian.
        private class CacheData
        {
            public int                          formatVersion;
            public string                       unityVersion;
            public Dictionary<string, string[]> forward;
        }

        private static void MarkCacheDirty()
        {
            _cacheDirty = true;
            if (_saveScheduled) return;
            _saveScheduled = true;
            // Debounce: gộp nhiều import lẻ thành 1 lần ghi disk
            EditorApplication.delayCall += FlushCache;
        }

        private static void FlushCache()
        {
            _saveScheduled = false;
            if (!_cacheDirty || !_built)
                return;
            _cacheDirty = false;

            var data = new CacheData
            {
                formatVersion = FormatVersion,
                unityVersion  = Application.unityVersion,
                forward       = _forward,
            };

            try
            {
                Directory.CreateDirectory(CacheDir);
                File.WriteAllText(CachePath, JsonConvert.SerializeObject(data, Formatting.None));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AssetReferenceIndex] Failed to write cache: {e.Message}");
            }
        }

        private static void TryLoadCache()
        {
            try
            {
                if (!File.Exists(CachePath))
                    return;

                var data = JsonConvert.DeserializeObject<CacheData>(File.ReadAllText(CachePath));
                if (data == null
                    || data.formatVersion != FormatVersion
                    || data.unityVersion != Application.unityVersion
                    || data.forward == null)
                    return; // stale/lỗi → để build lazy

                _forward.Clear();
                _reverse.Clear();
                foreach (KeyValuePair<string, string[]> kv in data.forward)
                {
                    if (string.IsNullOrEmpty(kv.Key)) continue;

                    string[] deps = kv.Value ?? Array.Empty<string>();
                    _forward[kv.Key] = deps;
                    for (int d = 0; d < deps.Length; d++)
                        AddReverse(deps[d], kv.Key);
                }

                _built = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AssetReferenceIndex] Failed to load cache: {e.Message}");
                _forward.Clear();
                _reverse.Clear();
                _built = false;
            }
        }
    }

    // ──────────────── Incremental hook ────────────────

    /// <summary>Giữ index đồng bộ khi asset import/delete/move — không full rebuild (SKILL #1).</summary>
    internal sealed class AssetRefIndexPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            // Nếu index chưa build → khỏi patch, để build lazy khi query đầu tiên
            if (!AssetReferenceIndex.IsBuilt)
                return;

            for (int i = 0; i < deletedAssets.Length; i++)
            {
                string path = deletedAssets[i];
                // Asset đã xóa → path không còn GUID nên AssetPathToGUID trả "" (không lookup được).
                // Vai trò referencer: gỡ ngay qua path (chính xác). Vai trò target: entry _reverse[guidCũ]
                // không xóa được ở đây, để lại như rác — NHƯNG tự lành: khi từng referencer của asset
                // đã xóa được reimport, RefreshReferencer gỡ path đó khỏi list, list rỗng → _reverse.Remove.
                // Không sai kết quả query (không ai còn query được guid đã mất). Chấp nhận có chủ đích.
                AssetReferenceIndex.RemoveAsset(path, AssetDatabase.AssetPathToGUID(path));
            }

            for (int i = 0; i < movedAssets.Length; i++)
                AssetReferenceIndex.MoveReferencer(movedFromAssetPaths[i], movedAssets[i]);

            for (int i = 0; i < importedAssets.Length; i++)
                AssetReferenceIndex.RefreshReferencer(importedAssets[i]);
        }
    }
}
