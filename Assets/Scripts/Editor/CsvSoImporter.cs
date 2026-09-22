using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

/// <summary>CSV를 검증해 ScriptableObject를 생성하거나 변경된 값만 갱신한다.</summary>
public static class CsvSoImporter
{
    public abstract class RefArrayBase
    {
        internal string Field { get; }
        internal string Header { get; }
        internal int Count { get; }
        internal string Folder { get; }
        internal string Prefix { get; }
        internal Type Type { get; }

        protected RefArrayBase(string field, string header, int count, string folder, string prefix, Type type)
        {
            Field = field;
            Header = header;
            Count = count;
            Folder = folder;
            Prefix = prefix;
            Type = type;
        }
    }

    public sealed class RefArray<T> : RefArrayBase where T : ScriptableObject
    {
        public RefArray(string field, string header, int count, string folder, string prefix)
            : base(field, header, count, folder, prefix, typeof(T)) { }
    }

    // 선택한 CSV를 지정한 SO 타입으로 변환
    public static void Import<T>(
        string outputFolder,
        string filePrefix,
        string keyHeader,
        IReadOnlyDictionary<string, string> fieldMap = null,
        string[] ignoreHeaders = null,
        RefArrayBase refArray = null)
        where T : ScriptableObject
    {
        if (Selection.activeObject is not TextAsset csv ||
            !AssetDatabase.GetAssetPath(csv).EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError("Project 창에서 CSV 파일을 선택하세요.");
            return;
        }

        string[] lines = csv.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length <= 1)
        {
            Debug.LogError("CSV에 데이터가 없습니다.");
            return;
        }

        if (!TryGetHeaders(lines[0], out string[] headers, out Dictionary<string, int> columns))
            return;

        HashSet<string> ignored = new(ignoreHeaders ?? Array.Empty<string>(), StringComparer.Ordinal);

        if (!columns.TryGetValue(keyHeader, out int keyCol))
        {
            Debug.LogError($"CSV에 Key Header가 없습니다: {keyHeader}");
            return;
        }

        if (ignored.Contains(keyHeader))
        {
            Debug.LogError($"Key Header는 무시할 수 없습니다: {keyHeader}");
            return;
        }

        T sample = ScriptableObject.CreateInstance<T>();
        SerializedObject sampleSo = new(sample);
        HashSet<string> refHeaders = new(StringComparer.Ordinal);

        if (!ValidateFields<T>(sampleSo, headers, columns, fieldMap, ignored, refArray, refHeaders))
        {
            UnityEngine.Object.DestroyImmediate(sample);
            return;
        }

        List<string[]> rows = new(lines.Length - 1);
        HashSet<string> keys = new(StringComparer.Ordinal);
        Dictionary<string, UnityEngine.Object> refCache = new();

        // SO를 수정하기 전에 CSV 전체 검증
        for (int i = 1; i < lines.Length; i++)
        {
            string[] cells = lines[i].Split(',');

            if (cells.Length != headers.Length)
            {
                UnityEngine.Object.DestroyImmediate(sample);
                Debug.LogError($"CSV {i + 1}번째 줄의 열 개수가 올바르지 않습니다.");
                return;
            }

            string key = cells[keyCol].Trim();

            if (string.IsNullOrEmpty(key) || !keys.Add(key))
            {
                UnityEngine.Object.DestroyImmediate(sample);
                Debug.LogError($"CSV {i + 1}번째 줄의 {keyHeader}가 비었거나 중복되었습니다: {key}");
                return;
            }

            for (int j = 0; j < headers.Length; j++)
            {
                string header = headers[j];

                if (ignored.Contains(header))
                    continue;

                string value = cells[j].Trim();

                if (refHeaders.Contains(header))
                {
                    if (TryGetReference(value, refArray, refCache, out _))
                        continue;

                    UnityEngine.Object.DestroyImmediate(sample);
                    Debug.LogError($"CSV {i + 1}번째 줄 '{header}' 참조를 찾을 수 없습니다: {value}");
                    return;
                }

                SerializedProperty property = sampleSo.FindProperty(GetField(header, fieldMap));

                if (CanConvert(property, value))
                    continue;

                UnityEngine.Object.DestroyImmediate(sample);
                Debug.LogError($"CSV {i + 1}번째 줄 '{header}' 값이 올바르지 않습니다: {value}");
                return;
            }

            rows.Add(cells);
        }

        UnityEngine.Object.DestroyImmediate(sample);

        // 같은 위치에 다른 타입의 Asset이 있는지 검사
        foreach (string[] cells in rows)
        {
            string path = GetPath(outputFolder, filePrefix, cells[keyCol].Trim());
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);

            if (asset != null && asset is not T)
            {
                Debug.LogError($"{path}에 {typeof(T).Name}이 아닌 Asset이 있습니다.");
                return;
            }
        }

        EnsureFolder(outputFolder);

        int added = 0;
        int updated = 0;
        int skipped = 0;

        // 검증된 CSV만 SO에 적용
        foreach (string[] cells in rows)
        {
            string path = GetPath(outputFolder, filePrefix, cells[keyCol].Trim());
            T data = AssetDatabase.LoadAssetAtPath<T>(path);
            bool isNew = data == null;

            if (isNew)
            {
                data = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(data, path);
            }

            SerializedObject so = new(data);
            bool changed = false;

            for (int i = 0; i < headers.Length; i++)
            {
                string header = headers[i];

                if (ignored.Contains(header) || refHeaders.Contains(header))
                    continue;

                SerializedProperty property = so.FindProperty(GetField(header, fieldMap));

                if (SetValue(property, cells[i].Trim()))
                    changed = true;
            }

            if (refArray != null && SetRefArray(so, cells, columns, refArray, refCache))
                changed = true;

            if (!isNew && !changed)
            {
                skipped++;
                continue;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            if (isNew)
                added++;
            else
                updated++;
        }

        if (added > 0 || updated > 0)
            AssetDatabase.SaveAssets();

        Debug.Log($"{typeof(T).Name} Import 완료 | 추가: {added} | 수정: {updated} | 변화 없음: {skipped}");
    }

    // CSV Header와 열 위치를 생성
    private static bool TryGetHeaders(
        string line,
        out string[] headers,
        out Dictionary<string, int> columns)
    {
        headers = line.Split(',');
        columns = new Dictionary<string, int>(headers.Length, StringComparer.Ordinal);

        for (int i = 0; i < headers.Length; i++)
        {
            string header = headers[i].Trim().TrimStart('\uFEFF');

            if (string.IsNullOrEmpty(header) || !columns.TryAdd(header, i))
            {
                Debug.LogError($"CSV Header가 비어 있거나 중복되었습니다: {header}");
                return false;
            }

            headers[i] = header;
        }

        return true;
    }

    // 일반 필드와 참조 배열 설정을 검사
    private static bool ValidateFields<T>(
        SerializedObject so,
        string[] headers,
        Dictionary<string, int> columns,
        IReadOnlyDictionary<string, string> fieldMap,
        HashSet<string> ignored,
        RefArrayBase refArray,
        HashSet<string> refHeaders)
        where T : ScriptableObject
    {
        if (refArray != null)
        {
            if (refArray.Count <= 0)
            {
                Debug.LogError("RefArray Count는 1 이상이어야 합니다.");
                return false;
            }

            SerializedProperty array = so.FindProperty(refArray.Field);

            if (array == null || !array.isArray)
            {
                Debug.LogError($"{typeof(T).Name}에 배열 필드가 없습니다: {refArray.Field}");
                return false;
            }

            for (int i = 1; i <= refArray.Count; i++)
            {
                string header = $"{refArray.Header}{i}";

                if (!columns.ContainsKey(header))
                {
                    Debug.LogError($"CSV에 필요한 Header가 없습니다: {header}");
                    return false;
                }

                refHeaders.Add(header);
            }
        }

        foreach (string header in headers)
        {
            if (ignored.Contains(header) || refHeaders.Contains(header))
                continue;

            string field = GetField(header, fieldMap);

            if (so.FindProperty(field) != null)
                continue;

            Debug.LogError($"{typeof(T).Name}에 SerializeField가 없습니다: {field}");
            return false;
        }

        return true;
    }

    // CSV Header에 연결된 SO 필드 이름 반환
    private static string GetField(
        string header,
        IReadOnlyDictionary<string, string> fieldMap)
    {
        return fieldMap != null && fieldMap.TryGetValue(header, out string field)
            ? field
            : header;
    }

    // CSV 값이 SO 필드 타입으로 변환 가능한지 검사
    private static bool CanConvert(SerializedProperty property, string value)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
                return int.TryParse(value, out _);

            case SerializedPropertyType.Float:
                return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

            case SerializedPropertyType.Boolean:
                return bool.TryParse(value, out _);

            case SerializedPropertyType.String:
                return true;

            case SerializedPropertyType.Enum:
                return GetEnumIndex(property, value) >= 0;

            default:
                return false;
        }
    }

    // 기존 값과 다를 때만 SO 값을 변경
    private static bool SetValue(SerializedProperty property, string value)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
                {
                    int next = int.Parse(value);

                    if (property.intValue == next)
                        return false;

                    property.intValue = next;
                    return true;
                }

            case SerializedPropertyType.Float:
                {
                    float next = float.Parse(value, CultureInfo.InvariantCulture);

                    if (Mathf.Approximately(property.floatValue, next))
                        return false;

                    property.floatValue = next;
                    return true;
                }

            case SerializedPropertyType.Boolean:
                {
                    bool next = bool.Parse(value);

                    if (property.boolValue == next)
                        return false;

                    property.boolValue = next;
                    return true;
                }

            case SerializedPropertyType.String:
                if (property.stringValue == value)
                    return false;

                property.stringValue = value;
                return true;

            case SerializedPropertyType.Enum:
                {
                    int next = GetEnumIndex(property, value);

                    if (property.enumValueIndex == next)
                        return false;

                    property.enumValueIndex = next;
                    return true;
                }

            default:
                return false;
        }
    }

    // Enum 이름에 해당하는 Index 반환
    private static int GetEnumIndex(SerializedProperty property, string value)
    {
        for (int i = 0; i < property.enumNames.Length; i++)
        {
            if (string.Equals(property.enumNames[i], value, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    // CSV ID에 해당하는 SO 참조 검색
    private static bool TryGetReference(
        string value,
        RefArrayBase refArray,
        Dictionary<string, UnityEngine.Object> cache,
        out UnityEngine.Object reference)
    {
        value = value.Trim();

        if (string.IsNullOrEmpty(value) || value == "0")
        {
            reference = null;
            return true;
        }

        string path = $"{refArray.Folder}/{refArray.Prefix}_{value}.asset";

        if (cache.TryGetValue(path, out reference))
            return reference != null;

        reference = AssetDatabase.LoadAssetAtPath(path, refArray.Type);
        cache[path] = reference;
        return reference != null;
    }

    // CSV ID 목록을 SO 참조 배열에 적용
    private static bool SetRefArray(
        SerializedObject so,
        string[] cells,
        Dictionary<string, int> columns,
        RefArrayBase refArray,
        Dictionary<string, UnityEngine.Object> cache)
    {
        SerializedProperty array = so.FindProperty(refArray.Field);
        bool changed = array.arraySize != refArray.Count;

        array.arraySize = refArray.Count;

        for (int i = 0; i < refArray.Count; i++)
        {
            string header = $"{refArray.Header}{i + 1}";
            string value = cells[columns[header]].Trim();

            TryGetReference(value, refArray, cache, out UnityEngine.Object reference);

            SerializedProperty element = array.GetArrayElementAtIndex(i);

            if (element.objectReferenceValue == reference)
                continue;

            element.objectReferenceValue = reference;
            changed = true;
        }

        return changed;
    }

    // 생성할 Asset 경로 반환
    private static string GetPath(string folder, string prefix, string key)
    {
        return $"{folder}/{prefix}_{key}.asset";
    }

    // 필요한 Unity 폴더가 없으면 생성
    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";

            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }
}