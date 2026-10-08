using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>보관함 이미지 임포트와 현재 맵의 실제 Unity 참조를 검사한다.</summary>
public static class AetherSceneValidation
{
    [InitializeOnLoadMethod]
    private static void RunRequestedCheck()
    {
        EditorApplication.delayCall += CheckRequest;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) CheckRequest();
        };
    }

    private static void CheckRequest()
    {
        const string request = "Temp/AetherSceneValidation.request";
        if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        File.Delete(request);
        RepairAndCheck();
    }

    [MenuItem("Tools/Aether/Repair and Check Map Scene")]
    public static void RepairAndCheck()
    {
        const string imagePath = "Assets/Art/AetherStorage/AetherStorageTerminal.png";
        string report = "Temp/AetherSceneValidation.txt";
        try
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode before repairing scene assets.");
            AssetDatabase.ImportAsset(imagePath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(imagePath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("TextureImporter was not created for " + imagePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 64;
            importer.filterMode = FilterMode.Point;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(imagePath);
            if (sprite == null)
                throw new InvalidOperationException("Imported texture has no Sprite subasset.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != "Map_baseScene")
                throw new InvalidOperationException("Open Map_baseScene before running this check.");
            AetherStorageTerminal terminal = UnityEngine.Object.FindFirstObjectByType<AetherStorageTerminal>();
            AetherEquipmentUI ui = UnityEngine.Object.FindFirstObjectByType<AetherEquipmentUI>();
            if (terminal == null || ui == null)
                throw new InvalidOperationException("Storage terminal or equipment UI is missing from the active scene.");
            SpriteRenderer renderer = terminal.GetComponent<SpriteRenderer>();
            if (renderer == null)
                throw new InvalidOperationException("Terminal SpriteRenderer is missing.");
            if (renderer.sprite != sprite)
            {
                Undo.RecordObject(renderer, "Repair Aether storage sprite");
                renderer.sprite = sprite;
                EditorUtility.SetDirty(renderer);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            SerializedObject terminalFields = new SerializedObject(terminal);
            if (terminalFields.FindProperty("equipmentUI").objectReferenceValue != ui ||
                terminalFields.FindProperty("dialogManager").objectReferenceValue == null)
                throw new InvalidOperationException("Terminal UI or dialogue reference is incorrect.");
            SerializedObject uiFields = new SerializedObject(ui);
            foreach (string field in new[] { "equipmentPanel", "ownedRoot", "itemTemplate", "message", "canvas", "storagePanel", "storageTitle" })
                if (uiFields.FindProperty(field).objectReferenceValue == null)
                    throw new InvalidOperationException("Equipment UI reference missing: " + field);
            SerializedProperty slots = uiFields.FindProperty("slots");
            if (slots.arraySize != PlayerBattleData.EquipmentCapacity)
                throw new InvalidOperationException("Equipment UI must have five slots.");
            for (int i = 0; i < slots.arraySize; i++)
                if (slots.GetArrayElementAtIndex(i).objectReferenceValue == null)
                    throw new InvalidOperationException("Missing slot " + i);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long localId);
            File.WriteAllText(report, $"PASS\nScene: {scene.path}\nSprite: {sprite.name}\nGUID: {guid}\nLocalID: {localId}\nTerminal: {terminal.transform.position}\nSlots: {slots.arraySize}\nUI and dialogue references: valid\n");
            Debug.Log("Aether scene validation passed. Report: " + report);
        }
        catch (Exception exception)
        {
            File.WriteAllText(report, "FAIL\n" + exception);
            Debug.LogException(exception);
        }
    }
}
