using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace SuteppiStore.CopyVariant
{
    public class CopyVariantWindow : EditorWindow
    {
        private GameObject _editedPrefab;
        private readonly List<GameObject> _basePrefabs = new List<GameObject>();
        private string _outputFolder = "Assets";
        private ReorderableList _reorderableList;
        private Vector2 _scrollPos;

        [MenuItem("Tools/CopyVariant")]
        public static void Open()
        {
            GetWindow<CopyVariantWindow>("CopyVariant");
        }

        private void OnEnable()
        {
            InitList();
        }

        private void InitList()
        {
            _reorderableList = new ReorderableList(_basePrefabs, typeof(GameObject), true, true, true, true);

            _reorderableList.drawHeaderCallback = rect =>
                EditorGUI.LabelField(rect, "ベース衣装 Prefab");

            _reorderableList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                rect.y += 2;
                rect.height = EditorGUIUtility.singleLineHeight;
                _basePrefabs[index] = (GameObject)EditorGUI.ObjectField(
                    rect, _basePrefabs[index], typeof(GameObject), false);
            };

            _reorderableList.onAddCallback = _ => _basePrefabs.Add(null);

            _reorderableList.onRemoveCallback = list =>
            {
                if (list.index >= 0 && list.index < _basePrefabs.Count)
                    _basePrefabs.RemoveAt(list.index);
            };
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("CopyVariant", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            DrawEditedPrefabField();
            EditorGUILayout.Space(8);
            _reorderableList.DoLayoutList();
            DrawBasePrefabToolbar();
            EditorGUILayout.Space(8);
            DrawOutputFolderField();
            EditorGUILayout.Space(12);
            DrawGenerateButton();

            EditorGUILayout.EndScrollView();
        }

        private void DrawBasePrefabToolbar()
        {
            // Drop zone
            var dropRect = GUILayoutUtility.GetRect(0, 36, GUILayout.ExpandWidth(true));
            var style = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
            };
            GUI.Box(dropRect, "Prefab / フォルダをここにドロップ", style);
            HandleDragAndDrop(dropRect);

            // Folder add button
            EditorGUILayout.Space(2);
            if (GUILayout.Button("フォルダから一括追加..."))
                AddPrefabsFromFolder();
        }

        private void HandleDragAndDrop(Rect dropRect)
        {
            var evt = Event.current;
            if (!dropRect.Contains(evt.mousePosition)) return;

            if (evt.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = CanAcceptDrag()
                    ? DragAndDropVisualMode.Copy
                    : DragAndDropVisualMode.Rejected;
                evt.Use();
            }
            else if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                AddPrefabsFromDraggedObjects(DragAndDrop.objectReferences);
                evt.Use();
            }
        }

        private static bool CanAcceptDrag()
        {
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj is GameObject go && IsPrefabAsset(go)) return true;
                if (obj is DefaultAsset) return true; // folder
            }
            return false;
        }

        private void AddPrefabsFromDraggedObjects(Object[] objects)
        {
            int added = 0;
            foreach (var obj in objects)
            {
                if (obj is GameObject go && IsPrefabAsset(go))
                {
                    if (!_basePrefabs.Contains(go)) { _basePrefabs.Add(go); added++; }
                }
                else if (obj is DefaultAsset)
                {
                    string folderPath = AssetDatabase.GetAssetPath(obj);
                    if (AssetDatabase.IsValidFolder(folderPath))
                        added += AddPrefabsInFolder(folderPath);
                }
            }
            if (added > 0) Repaint();
        }

        private void AddPrefabsFromFolder()
        {
            string startDir = AssetPathToFullPath(_outputFolder);
            if (!Directory.Exists(startDir)) startDir = Application.dataPath;

            string selected = EditorUtility.OpenFolderPanel("Prefab フォルダを選択", startDir, "");
            if (string.IsNullOrEmpty(selected)) return;

            string dataPath = Application.dataPath.Replace('\\', '/').TrimEnd('/');
            selected = selected.Replace('\\', '/').TrimEnd('/');

            string folderAssetPath;
            if (string.Equals(selected, dataPath, System.StringComparison.OrdinalIgnoreCase))
                folderAssetPath = "Assets";
            else if (selected.StartsWith(dataPath + "/", System.StringComparison.OrdinalIgnoreCase))
                folderAssetPath = "Assets/" + selected.Substring(dataPath.Length + 1);
            else
            {
                EditorUtility.DisplayDialog("エラー", "Assets フォルダ内のフォルダを選択してください。", "OK");
                return;
            }

            int added = AddPrefabsInFolder(folderAssetPath);
            if (added == 0)
                EditorUtility.DisplayDialog("CopyVariant", "追加できる新しい Prefab が見つかりませんでした。", "OK");
        }

        private int AddPrefabsInFolder(string folderAssetPath)
        {
            int added = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { folderAssetPath }))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (go != null && !_basePrefabs.Contains(go)) { _basePrefabs.Add(go); added++; }
            }
            return added;
        }

        private void DrawEditedPrefabField()
        {
            EditorGUILayout.LabelField("変換済み Prefab", EditorStyles.boldLabel);
            var next = (GameObject)EditorGUILayout.ObjectField(_editedPrefab, typeof(GameObject), false);
            if (next != _editedPrefab)
            {
                if (next != null && !IsPrefabAsset(next))
                    EditorUtility.DisplayDialog("エラー", "Prefab アセットを設定してください。", "OK");
                else
                    _editedPrefab = next;
            }
        }

        private void DrawOutputFolderField()
        {
            EditorGUILayout.LabelField("出力フォルダ", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.TextField(_outputFolder, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("選択", GUILayout.Width(50)))
                    SelectOutputFolder();
            }
        }

        private void DrawGenerateButton()
        {
            var validation = Validate();
            using (new EditorGUI.DisabledScope(validation != null))
            {
                if (GUILayout.Button("Prefab Variant を生成", GUILayout.Height(32)))
                    GenerateVariants();
            }

            if (validation != null)
                EditorGUILayout.HelpBox(validation, MessageType.Info);
        }

        private string Validate()
        {
            if (_editedPrefab == null)
                return "改変 Prefab を設定してください。";
            if (!_basePrefabs.Any(b => b != null))
                return "ベース衣装 Prefab を 1 つ以上設定してください。";
            if (!AssetDatabase.IsValidFolder(_outputFolder))
                return "有効な出力フォルダを選択してください。";
            return null;
        }

        private void SelectOutputFolder()
        {
            string startDir = AssetPathToFullPath(_outputFolder);
            if (!Directory.Exists(startDir))
                startDir = Application.dataPath;

            string selected = EditorUtility.OpenFolderPanel("出力フォルダを選択", startDir, "");
            if (string.IsNullOrEmpty(selected)) return;

            string dataPath = Application.dataPath.Replace('\\', '/').TrimEnd('/');
            selected = selected.Replace('\\', '/').TrimEnd('/');

            // Assets フォルダ自体が選択された場合
            if (string.Equals(selected, dataPath, System.StringComparison.OrdinalIgnoreCase))
            {
                _outputFolder = "Assets";
                return;
            }

            // Assets 配下のサブフォルダが選択された場合（大文字小文字を区別しない）
            if (selected.StartsWith(dataPath + "/", System.StringComparison.OrdinalIgnoreCase))
            {
                _outputFolder = "Assets/" + selected.Substring(dataPath.Length + 1);
                return;
            }

            EditorUtility.DisplayDialog("エラー", "Assets フォルダ内のフォルダを選択してください。", "OK");
        }

        private static string AssetPathToFullPath(string assetPath)
        {
            // "Assets"          -> Application.dataPath
            // "Assets/Foo/Bar"  -> Application.dataPath + "/Foo/Bar"
            string dataPath = Application.dataPath.Replace('\\', '/').TrimEnd('/');
            if (assetPath == "Assets")
                return dataPath;
            if (assetPath.StartsWith("Assets/"))
                return dataPath + "/" + assetPath.Substring("Assets/".Length);
            return dataPath;
        }

        private void GenerateVariants()
        {
            var validBases = _basePrefabs.Where(b => b != null).ToList();

            // Check for existing files first (outside batch edit)
            var targets = BuildTargetList(validBases);
            if (targets == null) return; // User cancelled

            int success = 0;
            int fail = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var (basePrefab, outputPath) in targets)
                {
                    if (TryGenerateVariant(basePrefab, outputPath, out string error))
                        success++;
                    else
                    {
                        Debug.LogError($"[CopyVariant] {basePrefab.name}: {error}");
                        fail++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            string msg = $"生成完了: {success} 件成功";
            if (fail > 0) msg += $"\n{fail} 件失敗（Console を確認してください）";
            EditorUtility.DisplayDialog("CopyVariant", msg, "OK");
        }

        // Returns null if user cancels, otherwise list of (base, outputPath) pairs to generate.
        private List<(GameObject base_, string outputPath)> BuildTargetList(List<GameObject> bases)
        {
            var result = new List<(GameObject, string)>();

            foreach (var basePrefab in bases)
            {
                string path = BuildOutputPath(basePrefab);

                if (FileExistsAtAssetPath(path))
                {
                    bool overwrite = EditorUtility.DisplayDialog(
                        "上書き確認",
                        $"{Path.GetFileName(path)} は既に存在します。\n上書きしますか？",
                        "上書き", "スキップ");
                    if (!overwrite) continue;
                }

                result.Add((basePrefab, path));
            }

            return result;
        }

        private string BuildOutputPath(GameObject basePrefab)
        {
            string name = $"{_editedPrefab.name}_{basePrefab.name}.prefab";
            return $"{_outputFolder}/{name}";
        }

        private bool TryGenerateVariant(GameObject basePrefab, string outputPath, out string error)
        {
            var baseMaterials = CollectMaterials(basePrefab);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(_editedPrefab);
            if (instance == null)
            {
                error = "InstantiatePrefab に失敗しました";
                return false;
            }

            try
            {
                ApplyMaterials(instance, baseMaterials);
                PrefabUtility.SaveAsPrefabAsset(instance, outputPath);
                error = null;
                return true;
            }
            catch (System.Exception e)
            {
                error = e.Message;
                return false;
            }
            finally
            {
                DestroyImmediate(instance);
            }
        }

        private static List<Material> CollectMaterials(GameObject prefab)
        {
            return prefab
                .GetComponentsInChildren<Renderer>(true)
                .SelectMany(r => r.sharedMaterials)
                .ToList();
        }

        private static void ApplyMaterials(GameObject target, List<Material> materials)
        {
            int index = 0;
            foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                if (index >= materials.Count) break;
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length && index < materials.Count; i++, index++)
                    slots[i] = materials[index];
                renderer.sharedMaterials = slots;
            }
        }

        private static bool FileExistsAtAssetPath(string assetPath)
        {
            return File.Exists(AssetPathToFullPath(assetPath));
        }

        private static bool IsPrefabAsset(GameObject go)
        {
            var type = PrefabUtility.GetPrefabAssetType(go);
            return type == PrefabAssetType.Regular
                || type == PrefabAssetType.Variant
                || type == PrefabAssetType.Model;
        }
    }
}
