using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Horcrux.Runtime.Abstractions.Persistence;
using Horcrux.Runtime.Abstractions.UserSegmentation;
using UnityEditor;
using UnityEngine;

namespace Horcrux.Editor.UserSegmentation
{
    /// <summary>
    /// Thử camp, xem pool đã kế thừa và kiểm dữ liệu phân tập qua chính host đang chạy. Chỉ có dữ liệu trong Play: ngoài Play host
    /// chưa được InitArgs tiêm nguồn dữ liệu. Nút xoá save chạy ngoài Play.
    /// </summary>
    public sealed class UserSegmentationSimulatorWindow : EditorWindow
    {
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        private static readonly GUIContent HostLabel = new("Host", "AUserSegmentationHost trong scene đang Play");
        private static readonly GUIContent CampaignLabel = new("Camp", "Tên camp như Adjust trả về; chỉ chạy matcher, không ghi gì vào host");
        private static readonly GUIContent PrevLabel = new("Prev của Purchased", "Hàng pool làm prev khi vẽ hàng Purchased đã kế thừa");
        private static readonly GUIContent CheckLabel = new("Kiểm hợp lệ", "Luật boot của lõi cộng ba kiểm chỉ tool làm được");
        private static readonly GUIContent SaveCollectionLabel = new("Save collection", "Asset BasePersistenceDataCollection của dự án");
        private static readonly GUIContent ClearSaveLabel = new("Xoá save phân tập", "Xoá key PlayerPrefs của entry phân tập; chỉ ngoài Play");
        private static readonly GUILayoutOption[] CellWidth = { GUILayout.Width(110) };
        private static readonly GUILayoutOption[] IndexWidth = { GUILayout.Width(24) };
        private static readonly GUILayoutOption[] SegmentWidth = { GUILayout.Width(80) };
        private static readonly Color InheritedColor = new(0.45f, 0.75f, 1f);
        private static readonly Color HitRowColor = new(0.45f, 0.8f, 0.5f, 0.35f);
        private static readonly Color ErrorColor = new(0.95f, 0.4f, 0.4f);

        private static GUIStyle _sectionStyle;
        private static GUIStyle _richLabelStyle;

        // Reflection bound to the selected host; rebound when the host changes.
        private AUserSegmentationHost _boundHost;
        private PropertyInfo _rulesProperty;
        private PropertyInfo _defaultIdProperty;
        private PropertyInfo _purchasedIdProperty;
        private PropertyInfo _configsProperty;
        private Type _configType;
        private FieldInfo[] _columns;
        private FieldInfo _idField;

        // Snapshot of the host's data; rebuilt when the host hands out another list (a fetch replaces it).
        private IReadOnlyList<IUserSegmentConfig> _configs;
        private IReadOnlyList<IUserSegmentKeywordRule> _rules;
        private int _defaultId;
        private int _purchasedId;
        private int[] _prevIds = Array.Empty<int>();
        private string[] _prevNames = Array.Empty<string>();
        private int _prevIndex;
        private IUserSegmentConfig _purchasedRow;
        private IUserSegmentConfig _purchasedMerged;
        private IUserSegmentConfig _prevRow;

        private AUserSegmentationHost _host;
        private string _campaign = string.Empty;
        private string _matchLine;
        private int _hitRuleIndex = -1;
        private string _hitKeyword;
        private readonly List<string> _errors = new();
        private bool _hasChecked;
        private BasePersistenceDataCollection _saveCollection;
        private string _clearMessage;
        private Vector2 _scroll;

        [MenuItem("Horcrux/User Segmentation/Simulator")]
        private static void ShowWindow() => GetWindow<UserSegmentationSimulatorWindow>("Segmentation Simulator").Show();

        private void OnEnable()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(BasePersistenceDataCollection));

            if (_saveCollection == null && guids.Length == 1)
                _saveCollection = AssetDatabase.LoadAssetAtPath<BasePersistenceDataCollection>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private void OnGUI()
        {
            EnsureStyles();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            if (TryPrepareHost(out string blocker))
            {
                DrawCampaign();
                DrawPool();
                DrawCheck();
            }
            else
            {
                EditorGUILayout.HelpBox(blocker, MessageType.Info);
            }

            DrawClearSave();
            EditorGUILayout.EndScrollView();
        }

        private void OnInspectorUpdate()
        {
            if (EditorApplication.isPlaying)
                Repaint();
        }

        private static void EnsureStyles()
        {
            if (_sectionStyle != null)
                return;

            _sectionStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, margin = new RectOffset(2, 2, 10, 2) };
            _richLabelStyle = new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true };
        }

        #region Host

        private bool TryPrepareHost(out string blocker)
        {
            blocker = null;

            if (!EditorApplication.isPlaying)
            {
                blocker = "Bấm Play: ngoài Play host chưa được tiêm nguồn dữ liệu nên không đọc được pool và bảng keyword.";
                return false;
            }

            if (_host == null && Event.current.type == EventType.Layout)
                _host = FindFirstObjectByType<AUserSegmentationHost>(FindObjectsInactive.Include);

            _host = (AUserSegmentationHost)EditorGUILayout.ObjectField(HostLabel, _host, typeof(AUserSegmentationHost), true);

            if (_host == null)
            {
                blocker = "Scene đang chạy không có AUserSegmentationHost.";
                return false;
            }

            if (!_host.IsReady)
            {
                blocker = "Host chưa boot: chờ bootstrap chạy xong bước phân tập.";
                return false;
            }

            if (_host != _boundHost)
                Bind(_host);

            if (_configType == null)
            {
                blocker = $"{_host.GetType().Name} không kế thừa AUserSegmentationHost<TConfig>.";
                return false;
            }

            if (Event.current.type == EventType.Layout)
                RefreshSnapshotIfChanged();

            return true;
        }

        private void Bind(AUserSegmentationHost host)
        {
            _boundHost = host;
            _configs = null;
            _rules = null;
            _errors.Clear();
            _hasChecked = false;

            Type baseType = typeof(AUserSegmentationHost);
            _rulesProperty = baseType.GetProperty("KeywordRules", PrivateInstance);
            _defaultIdProperty = baseType.GetProperty("DefaultSegmentId", PrivateInstance);
            _purchasedIdProperty = baseType.GetProperty("PurchasedSegmentId", PrivateInstance);

            // Configs lives on the generic base: walk the chain, a derived type does not see a base's non-public member.
            _configType = null;

            for (Type t = host.GetType(); t != null; t = t.BaseType)
            {
                if (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(AUserSegmentationHost<>))
                    continue;

                _configType = t.GetGenericArguments()[0];
                _configsProperty = t.GetProperty("Configs", PrivateInstance);
                break;
            }

            if (_configType == null)
                return;

            _columns = ColumnsOf(_configType);
            _idField = UserSegmentationSimulatorCore.FindIdField(_configType, UserSegmentationSimulatorCore.IntFieldsOf(_configType));
        }

        // Int columns may inherit; string columns (a segment name) only help reading the grid.
        private static FieldInfo[] ColumnsOf(Type configType)
        {
            FieldInfo[] all = configType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            var columns = new List<FieldInfo>(all.Length);

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].FieldType == typeof(int) || all[i].FieldType == typeof(string))
                    columns.Add(all[i]);
            }

            return columns.ToArray();
        }

        private void RefreshSnapshotIfChanged()
        {
            var configs = (IReadOnlyList<IUserSegmentConfig>)_configsProperty.GetValue(_host);
            var rules = (IReadOnlyList<IUserSegmentKeywordRule>)_rulesProperty.GetValue(_host);

            if (ReferenceEquals(configs, _configs) && ReferenceEquals(rules, _rules))
                return;

            _configs = configs;
            _rules = rules;
            _defaultId = (int)_defaultIdProperty.GetValue(_host);
            _purchasedId = (int)_purchasedIdProperty.GetValue(_host);
            _hasChecked = false;
            RebuildPrevChoices();
            RebuildMatch();
        }

        #endregion

        #region Campaign

        private void DrawCampaign()
        {
            EditorGUILayout.LabelField("Camp", _sectionStyle);
            EditorGUI.BeginChangeCheck();
            _campaign = EditorGUILayout.TextField(CampaignLabel, _campaign);

            if (EditorGUI.EndChangeCheck())
                RebuildMatch();

            EditorGUILayout.LabelField(_matchLine, _richLabelStyle);

            for (int i = 0; _rules != null && i < _rules.Count; i++)
                DrawRule(i, _rules[i]);
        }

        private void RebuildMatch()
        {
            _hitRuleIndex = -1;
            _hitKeyword = null;

            if (!UserSegmentKeywordMatcher.TryMatch(_campaign, _rules, out int segmentId, out string keyword, out bool isAmbiguous))
            {
                _matchLine = $"Không khớp → tập default {_defaultId}";
                return;
            }

            _hitKeyword = keyword;

            // The winner is the first row of that segment holding the keyword: an earlier row holding it would have matched first.
            for (int i = 0; i < _rules.Count && _hitRuleIndex < 0; i++)
            {
                if (_rules[i] != null && _rules[i].SegmentId == segmentId && Contains(_rules[i].Keywords, keyword))
                    _hitRuleIndex = i;
            }

            _matchLine = $"Khớp <b>tập {segmentId}</b> bằng keyword '<b>{keyword}</b>'"
                + (isAmbiguous ? " — <color=#f0a040>keyword của tập khác cũng khớp, hàng trên thắng</color>" : string.Empty)
                + (segmentId == _purchasedId ? " — <color=#f06060>hàng trỏ Purchased: decider đưa về default</color>" : string.Empty);
        }

        private void DrawRule(int index, IUserSegmentKeywordRule rule)
        {
            Rect rect = EditorGUILayout.BeginHorizontal();

            if (index == _hitRuleIndex && Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rect, HitRowColor);

            EditorGUILayout.LabelField(index.ToString(), IndexWidth);

            if (rule == null)
            {
                EditorGUILayout.LabelField("(null)");
            }
            else
            {
                EditorGUILayout.LabelField("tập " + rule.SegmentId, SegmentWidth);
                EditorGUILayout.LabelField(FormatKeywords(rule.Keywords, index == _hitRuleIndex ? _hitKeyword : null), _richLabelStyle);
            }

            EditorGUILayout.EndHorizontal();
        }

        private static string FormatKeywords(IReadOnlyList<string> keywords, string hit)
        {
            if (keywords == null)
                return "(null)";

            var sb = new StringBuilder();

            for (int i = 0; i < keywords.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");

                bool isHit = hit != null && keywords[i] == hit;
                sb.Append(isHit ? "<b>[" : string.Empty).Append(keywords[i] ?? "(null)").Append(isHit ? "]</b>" : string.Empty);
            }

            return sb.ToString();
        }

        private static bool Contains(IReadOnlyList<string> keywords, string keyword)
        {
            for (int i = 0; keywords != null && i < keywords.Count; i++)
            {
                if (keywords[i] == keyword)
                    return true;
            }

            return false;
        }

        #endregion

        #region Pool

        private void RebuildPrevChoices()
        {
            var ids = new List<int>();
            var names = new List<string>();
            _purchasedRow = null;

            for (int i = 0; _configs != null && i < _configs.Count; i++)
            {
                IUserSegmentConfig row = _configs[i];

                if (row == null)
                    continue;

                if (row.Id == _purchasedId)
                {
                    _purchasedRow = row;
                    continue;
                }

                ids.Add(row.Id);
                names.Add("tập " + row.Id);
            }

            _prevIds = ids.ToArray();
            _prevNames = names.ToArray();
            _prevIndex = Math.Max(0, ids.IndexOf(_defaultId));
            RebuildPurchasedMerge();
        }

        private void RebuildPurchasedMerge()
        {
            _prevRow = _prevIds.Length > 0 ? UserSegmentConfigTable.Find(_configs, _prevIds[_prevIndex]) : null;
            _purchasedMerged = _purchasedRow != null && _prevRow != null
                ? UserSegmentationSimulatorCore.Inherit(_configType, _purchasedRow, _prevRow)
                : null;
        }

        private void DrawPool()
        {
            EditorGUILayout.LabelField("Pool", _sectionStyle);

            if (_purchasedRow != null && _prevIds.Length > 0)
            {
                EditorGUI.BeginChangeCheck();
                _prevIndex = EditorGUILayout.Popup(PrevLabel, _prevIndex, _prevNames);

                if (EditorGUI.EndChangeCheck())
                    RebuildPurchasedMerge();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Id", EditorStyles.boldLabel, CellWidth);

                for (int c = 0; c < _columns.Length; c++)
                    EditorGUILayout.LabelField(_columns[c].Name, EditorStyles.boldLabel, CellWidth);
            }

            for (int i = 0; _configs != null && i < _configs.Count; i++)
                DrawPoolRow(_configs[i]);
        }

        private void DrawPoolRow(IUserSegmentConfig row)
        {
            using var scope = new EditorGUILayout.HorizontalScope();

            if (row == null)
            {
                EditorGUILayout.LabelField("(null)", CellWidth);
                return;
            }

            bool isPurchased = row == _purchasedRow && _purchasedMerged != null;
            EditorGUILayout.LabelField(row.Id + (isPurchased ? " ← " + _prevRow.Id : string.Empty), CellWidth);

            for (int c = 0; c < _columns.Length; c++)
            {
                object raw = _columns[c].GetValue(row);
                bool isInherited = isPurchased && raw is int value && value == UserSegmentInheritance.InheritValue;

                if (!isInherited)
                {
                    EditorGUILayout.LabelField(raw?.ToString() ?? "(null)", CellWidth);
                    continue;
                }

                Color previous = GUI.contentColor;
                GUI.contentColor = InheritedColor;
                EditorGUILayout.LabelField(new GUIContent(_columns[c].GetValue(_purchasedMerged).ToString(),
                    $"gốc {UserSegmentInheritance.InheritValue} → lấy từ tập {_prevRow.Id}"), CellWidth);
                GUI.contentColor = previous;
            }
        }

        #endregion

        #region Check

        private void DrawCheck()
        {
            EditorGUILayout.LabelField("Kiểm", _sectionStyle);

            if (GUILayout.Button(CheckLabel))
            {
                _errors.Clear();
                UserSegmentationSimulatorCore.Check(_configType, _configs, _rules, _defaultId, _purchasedId, _errors);
                _hasChecked = true;
            }

            if (!_hasChecked)
                return;

            if (_errors.Count == 0)
            {
                EditorGUILayout.HelpBox("Hợp lệ: luật boot của lõi và ba kiểm của tool đều qua.", MessageType.Info);
                return;
            }

            Color previous = GUI.contentColor;
            GUI.contentColor = ErrorColor;

            for (int i = 0; i < _errors.Count; i++)
                EditorGUILayout.LabelField(_errors[i], _richLabelStyle);

            GUI.contentColor = previous;
        }

        #endregion

        #region Clear save

        private void DrawClearSave()
        {
            EditorGUILayout.LabelField("Save", _sectionStyle);
            _saveCollection = (BasePersistenceDataCollection)EditorGUILayout.ObjectField(SaveCollectionLabel, _saveCollection,
                typeof(BasePersistenceDataCollection), false);

            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || _saveCollection == null))
            {
                if (GUILayout.Button(ClearSaveLabel))
                    _clearMessage = ClearSegmentSave(_saveCollection);
            }

            if (_clearMessage != null)
                EditorGUILayout.HelpBox(_clearMessage, MessageType.None);
        }

        private static string ClearSegmentSave(BasePersistenceDataCollection collection)
        {
            string entryKey = collection.UserSegmentState?.Key;

            if (string.IsNullOrEmpty(entryKey))
                return "Entry User Segment trên collection chưa có Key.";

            // The collection's own prefixing, so the tool deletes exactly what the runtime writes.
            string storageKey = (string)typeof(BasePersistenceDataCollection).GetMethod("GetFinalKey", PrivateInstance)
                .Invoke(collection, new object[] { entryKey });

            if (!PlayerPrefs.HasKey(storageKey))
                return $"PlayerPrefs không có '{storageKey}': lần Play sau đã là cài mới.";

            if (!EditorUtility.DisplayDialog("Xoá save phân tập", $"Xoá PlayerPrefs '{storageKey}'? Lần Play sau chạy như cài mới.", "Xoá", "Huỷ"))
                return null;

            PlayerPrefs.DeleteKey(storageKey);
            PlayerPrefs.Save();
            return $"Đã xoá '{storageKey}'.";
        }

        #endregion
    }
}
