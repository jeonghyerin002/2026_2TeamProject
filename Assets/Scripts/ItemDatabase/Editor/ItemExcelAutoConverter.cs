using System;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEditor;
using UnityEngine;

namespace TeamProject.ItemManagement.Editor
{
    [InitializeOnLoad]
    public static class ItemExcelAutoConverter
    {
        public const string ExcelAssetPath = "Assets/Editor/ItemData/ItemManager.xlsx";
        public const string JsonAssetPath = "Assets/Resources/ItemDatabase/items.json";
        public static string Status { get; private set; } = "엑셀 확인 대기 중";
        public static bool HasError { get; private set; }
        private static string observedStamp;
        private static double nextPoll;
        private static double convertAfter;
        private static bool pending;
        private static int retryCount;

        static ItemExcelAutoConverter()
        {
            // 정적 초기화에서 에셋을 읽지 않고 에디터가 준비된 후 확인합니다.
            EditorApplication.update += Update;
        }

        public static string Absolute(string assetPath) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));

        private static void Update()
        {
            if (Application.isBatchMode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            double now = EditorApplication.timeSinceStartup;
            if (now >= nextPoll)
            {
                nextPoll = now + 1.0;
                try
                {
                    var info = new FileInfo(Absolute(ExcelAssetPath));
                    string stamp = info.Exists ? info.LastWriteTimeUtc.Ticks + ":" + info.Length : "missing";
                    if (stamp != observedStamp)
                    {
                        observedStamp = stamp;
                        if (info.Exists)
                        {
                            pending = true; retryCount = 0; convertAfter = now + 1.0;
                            Status = "엑셀 저장 완료 대기 중"; HasError = false;
                        }
                        else
                        {
                            pending = false; HasError = true;
                            Status = "엑셀 파일이 없습니다: " + ExcelAssetPath;
                        }
                    }
                }
                catch (IOException) { return; }
                catch (UnauthorizedAccessException) { Status = "엑셀 파일에 접근할 수 없습니다."; HasError = true; return; }
            }
            if (!pending || now < convertAfter) return;
            pending = false;
            try { Convert(); }
            catch (Exception exception)
            {
                bool mayBeSaving = exception is IOException || exception is InvalidDataException || exception is XmlException;
                if (mayBeSaving && ++retryCount <= 5)
                {
                    pending = true; convertAfter = now + 1.0;
                    Status = "엑셀 저장 중이거나 사용 중입니다. 다시 확인합니다.";
                }
                else ReportError(exception);
            }
        }

        // 수동 실행/빌드 검사에서는 오류를 호출자에게 전달합니다.
        public static void Convert()
        {
            string sourcePath = Absolute(ExcelAssetPath);
            var before = new FileInfo(sourcePath);
            long previousLength = before.Length;
            DateTime previousWrite = before.LastWriteTimeUtc;
            GameItemDataFile data = ItemExcelReader.Read(sourcePath);
            var after = new FileInfo(sourcePath);
            if (after.Length != previousLength || after.LastWriteTimeUtc != previousWrite)
                throw new IOException("엑셀 파일이 저장 중입니다.");
            string json = JsonUtility.ToJson(data, true);
            bool changed = ItemExcelReader.WriteIfChanged(Absolute(JsonAssetPath), json);
            // 자동 새로고침 설정과 무관하게 이 결과 파일만 임포트합니다.
            AssetDatabase.ImportAsset(JsonAssetPath, ImportAssetOptions.ForceUpdate);
            ItemDatabaseScriptableObjectBuilder.CreateOrUpdate(data);
            HasError = false;
            Status = DateTime.Now.ToString("HH:mm:ss") + " · 분류 " + data.categories.Length + "개 / 아이템 " + data.categories.Sum(c => c.items.Length) + "개";
            if (changed) Debug.Log("아이템 엑셀 자동 변환 완료: " + Status);
        }

        [MenuItem("Tools/아이템 데이터/지금 변환")]
        public static void ConvertNow()
        {
            pending = false;
            try { Convert(); }
            catch (Exception exception) { ReportError(exception); }
        }

        [MenuItem("Tools/아이템 데이터/엑셀 열기")]
        public static void OpenExcel()
        {
            string path = Absolute(ExcelAssetPath);
            if (!File.Exists(path)) { ReportError(new FileNotFoundException("엑셀 파일이 없습니다: " + path)); return; }
            EditorUtility.OpenWithDefaultApp(path);
        }

        private static void ReportError(Exception exception)
        {
            HasError = true;
            Status = exception.Message;
            Debug.LogError("아이템 엑셀 변환 실패: " + Status + "\n엑셀을 수정하고 저장하세요. 검증 실패 시 기존 JSON은 유지됩니다.");
        }
    }
}
