using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TeamProject.ItemManagement.Editor
{
    public sealed class ItemExcelWindow : EditorWindow
    {
        private Vector2 scroll;
        private GameItemDataFile preview;
        private string previousJson;
        private string previewError;
        private double nextRepaint;
        private readonly HashSet<string> expanded = new HashSet<string>();

        [MenuItem("Tools/아이템 데이터/관리 창")]
        public static void Open() { GetWindow<ItemExcelWindow>("아이템 데이터"); }
        private void OnEnable() { EditorApplication.update += Tick; }
        private void OnDisable() { EditorApplication.update -= Tick; }
        private void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextRepaint) return;
            nextRepaint = EditorApplication.timeSinceStartup + 0.5;
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("엑셀 아이템 관리", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("엑셀을 열어 편집하고 저장하면 자동 반영됩니다. 아래 목록은 마지막으로 변환된 데이터입니다.", MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("엑셀 열기")) ItemExcelAutoConverter.OpenExcel();
            if (GUILayout.Button("지금 변환")) ItemExcelAutoConverter.ConvertNow();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(ItemExcelAutoConverter.Status, ItemExcelAutoConverter.HasError ? MessageType.Error : MessageType.None);
            if (GUILayout.Button("프로젝트에서 엑셀 위치 보기"))
            {
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(ItemExcelAutoConverter.ExcelAssetPath);
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
            ReadPreview();
            if (previewError != null) EditorGUILayout.HelpBox(previewError, MessageType.Warning);
            if (preview?.categories == null) return;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (GameItemCategory category in preview.categories)
            {
                bool isExpanded = EditorGUILayout.Foldout(expanded.Contains(category.id), category.displayName + " (" + category.items.Length + "개)", true);
                if (isExpanded) expanded.Add(category.id); else expanded.Remove(category.id);
                if (!isExpanded) continue;
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("분류 ID", category.id);
                EditorGUILayout.LabelField(category.description, EditorStyles.wordWrappedLabel);
                if (category.items.Length == 0) EditorGUILayout.LabelField("등록된 아이템 없음");
                foreach (GameItemDefinition item in category.items)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField(item.displayName + " (" + item.id + ")", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(item.description, EditorStyles.wordWrappedLabel);
                    EditorGUILayout.LabelField("사용 설명: " + item.usageDescription, EditorStyles.wordWrappedLabel);
                    EditorGUILayout.LabelField("초기 수량 " + item.initialQuantity + " / 묶음 제한 " + item.maxStack + " / 소모 " + (item.consumable ? "예" : "아니오"));
                    EditorGUILayout.LabelField("이미지: " + item.spriteKey + " / 동작: " + item.useActionId, EditorStyles.wordWrappedLabel);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndScrollView();
        }

        private void ReadPreview()
        {
            TextAsset source = AssetDatabase.LoadAssetAtPath<TextAsset>(ItemExcelAutoConverter.JsonAssetPath);
            if (source == null) { previewError = "아직 JSON이 없습니다. 지금 변환을 실행하세요."; return; }
            if (previousJson == source.text) return;
            try
            {
                GameItemDataFile parsed = JsonUtility.FromJson<GameItemDataFile>(source.text);
                if (parsed?.categories == null) throw new FormatException("categories 배열이 없습니다.");
                foreach (GameItemCategory category in parsed.categories)
                    if (category == null || category.items == null || Array.Exists(category.items, item => item == null))
                        throw new FormatException("분류 또는 아이템 데이터가 올바르지 않습니다.");
                preview = parsed; previousJson = source.text; previewError = null;
            }
            catch (Exception exception) { previewError = "JSON 미리보기 실패: " + exception.Message; }
        }
    }
}
