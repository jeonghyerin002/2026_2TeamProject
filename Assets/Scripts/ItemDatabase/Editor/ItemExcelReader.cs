using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace TeamProject.ItemManagement.Editor
{
    // Unity API 없이 XLSX를 읽습니다. Excel 설치나 외부 패키지가 필요하지 않습니다.
    public static class ItemExcelReader
    {
        private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly string[] Headers = {
            "아이템 ID", "이름", "설명", "사용 설명", "이미지 키",
            "초기 수량", "묶음 제한", "소모 여부", "사용 동작 ID", "메모",
            "스탯 보정", "에테르 타입", "기술 ID", "기술 이름", "기술 피해",
            "회복량", "해제 상태 이상", "강화 대상", "강화 수치(%)"
        };

        public static GameItemDataFile Read(string path)
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
            {
                XDocument workbook = ReadXml(zip, "xl/workbook.xml");
                XDocument relationships = ReadXml(zip, "xl/_rels/workbook.xml.rels");
                var targets = relationships.Root.Elements().ToDictionary(x => (string)x.Attribute("Id"), x => (string)x.Attribute("Target"));
                var strings = zip.GetEntry("xl/sharedStrings.xml") == null
                    ? new List<string>()
                    : ReadXml(zip, "xl/sharedStrings.xml").Root.Elements(Main + "si").Select(Text).ToList();
                var categories = new List<GameItemCategory>();
                var categoryIds = new HashSet<string>(StringComparer.Ordinal);
                var itemIds = new HashSet<string>(StringComparer.Ordinal);

                foreach (XElement sheet in workbook.Root.Element(Main + "sheets").Elements(Main + "sheet"))
                {
                    string name = (string)sheet.Attribute("name");
                    if (string.IsNullOrEmpty(name)) throw new FormatException("시트 이름이 없습니다.");
                    if (name.StartsWith("_", StringComparison.Ordinal)) continue;
                    string relationshipId = (string)sheet.Attribute(Rel + "id");
                    if (!targets.TryGetValue(relationshipId, out string target)) throw new FormatException(name + ": 시트 연결이 없습니다.");
                    var uri = new Uri(new Uri("https://xlsx.local/xl/workbook.xml"), target);
                    if (uri.Host != "xlsx.local") throw new FormatException(name + ": 외부 시트 연결은 지원하지 않습니다.");
                    var xml = ReadXml(zip, Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')));
                    var cells = new Dictionary<string, string>();
                    var rows = new SortedSet<int>();
                    foreach (XElement cell in xml.Descendants(Main + "sheetData").Elements(Main + "row").Elements(Main + "c"))
                    {
                        string address = (string)cell.Attribute("r");
                        Match match = Regex.Match(address ?? "", "^([A-Z]+)([0-9]+)$");
                        if (!match.Success) throw new FormatException(name + ": 잘못된 셀 주소");
                        int row = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                        if (row >= 10) rows.Add(row);
                        if (cell.Element(Main + "f") != null) throw Error(name, address, "수식 대신 값으로 붙여넣으세요.");
                        string type = (string)cell.Attribute("t");
                        string value = (string)cell.Element(Main + "v") ?? "";
                        if (type == "s")
                        {
                            if (!int.TryParse(value, out int index) || index < 0 || index >= strings.Count)
                                throw Error(name, address, "잘못된 문자열 인덱스입니다.");
                            value = strings[index];
                        }
                        else if (type == "inlineStr") value = Text(cell);
                        else if (type == "e") throw Error(name, address, "엑셀 오류 값: " + value);
                        string column = match.Groups[1].Value;
                        if (row >= 9 && (column.Length != 1 || column[0] > 'S') && !string.IsNullOrWhiteSpace(value))
                            throw Error(name, address, "데이터는 A~S열에 입력하세요.");
                        cells[address] = value;
                    }

                    if (Cell(cells, "A4") != "분류 ID" || Cell(cells, "A5") != "분류 설명")
                        throw new FormatException(name + ": 기존 분류 시트를 복사하세요. A4/A5 양식이 다릅니다.");
                    for (int col = 0; col < Headers.Length; col++)
                        if (Cell(cells, ((char)('A' + col)) + "9") != Headers[col])
                            throw Error(name, ((char)('A' + col)) + "9", "열 제목은 '" + Headers[col] + "'이어야 합니다.");
                    string categoryId = Cell(cells, "B4").Trim();
                    CheckId(categoryId, name, "B4");
                    if (!categoryIds.Add(categoryId)) throw Error(name, "B4", "중복 분류 ID: " + categoryId);
                    var items = new List<GameItemDefinition>();
                    foreach (int row in rows)
                    {
                        string[] values = Enumerable.Range(0, Headers.Length).Select(col => Cell(cells, ((char)('A' + col)).ToString() + row).Trim()).ToArray();
                        if (values.All(string.IsNullOrWhiteSpace)) continue;
                        CheckId(values[0], name, "A" + row);
                        if (!itemIds.Add(values[0])) throw Error(name, "A" + row, "전체 시트에서 중복 아이템 ID: " + values[0]);
                        if (values[1].Length == 0) throw Error(name, "B" + row, "아이템 이름이 필요합니다.");
                        items.Add(new GameItemDefinition {
                            id = values[0], displayName = values[1], description = values[2], usageDescription = values[3],
                            spriteKey = values[4], initialQuantity = Integer(values[5], 0, 0, name, "F" + row),
                            maxStack = Integer(values[6], 1, 1, name, "G" + row),
                            consumable = Boolean(values[7], name, "H" + row), useActionId = values[8],
                            statModifiers = ParseStatModifiers(values[10], name, "K" + row),
                            etherType = values[11], skillId = values[12], skillName = values[13],
                            skillDamage = Integer(values[14], 0, 0, name, "O" + row),
                            recoveryAmount = Integer(values[15], 0, 0, name, "P" + row),
                            curedStatusEffects = SplitIds(values[16], name, "Q" + row),
                            enhancementTarget = OptionalId(values[17], name, "R" + row),
                            enhancementPercentage = Percentage(values[18], name, "S" + row)
                        });
                    }
                    categories.Add(new GameItemCategory { id = categoryId, displayName = name, description = Cell(cells, "B5"), items = items.ToArray() });
                }
                if (categories.Count == 0) throw new FormatException("변환할 분류 시트가 없습니다.");
                return new GameItemDataFile { schemaVersion = 1, categories = categories.ToArray() };
            }
        }

        // 검증된 내용만 교체하고 동일한 JSON이면 임포트 반복을 피합니다.
        public static bool WriteIfChanged(string path, string json)
        {
            if (File.Exists(path) && File.ReadAllText(path, Encoding.UTF8) == json) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, json, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return true;
        }

        private static XDocument ReadXml(ZipArchive zip, string name)
        {
            ZipArchiveEntry entry = zip.GetEntry(name);
            if (entry == null) throw new FormatException("XLSX 내부 파일이 없습니다: " + name);
            using (Stream stream = entry.Open())
            using (XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                return XDocument.Load(reader);
        }
        private static string Text(XElement node) => string.Concat(node.Descendants(Main + "t").Where(x => !x.Ancestors(Main + "rPh").Any()).Select(x => x.Value));
        private static string Cell(Dictionary<string, string> cells, string address) => cells.TryGetValue(address, out string value) ? value : "";
        private static FormatException Error(string sheet, string cell, string message) => new FormatException(sheet + "!" + cell + ": " + message);
        private static void CheckId(string value, string sheet, string cell)
        {
            if (!Regex.IsMatch(value, "^[a-z][a-z0-9_]*$")) throw Error(sheet, cell, "ID는 영문 소문자로 시작하고 소문자·숫자·밑줄만 사용하세요.");
        }
        private static int Integer(string value, int fallback, int minimum, string sheet, string cell)
        {
            if (value.Length == 0) return fallback;
            if (!decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal number) ||
                number < minimum || number > int.MaxValue || decimal.Truncate(number) != number)
                throw Error(sheet, cell, minimum + " 이상 " + int.MaxValue + " 이하 정수가 필요합니다.");
            return (int)number;
        }
        private static bool Boolean(string value, string sheet, string cell)
        {
            switch (value.ToLowerInvariant())
            {
                case "": case "아니오": case "false": case "0": return false;
                case "예": case "true": case "1": return true;
                default: throw Error(sheet, cell, "소모 여부는 예 또는 아니오를 선택하세요.");
            }
        }

        // 예: attack:+10%; health:+5%. 빈칸이면 스탯 보정이 없는 아이템입니다.
        private static GameItemStatModifier[] ParseStatModifiers(string value, string sheet, string cell)
        {
            if (string.IsNullOrWhiteSpace(value)) return Array.Empty<GameItemStatModifier>();
            var modifiers = new List<GameItemStatModifier>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string entry in value.Split(';'))
            {
                string[] pair = entry.Split(':');
                if (pair.Length != 2) throw Error(sheet, cell, "스탯 보정은 attack:+10%; health:+5% 형식으로 입력하세요.");
                string statId = pair[0].Trim();
                CheckId(statId, sheet, cell);
                if (!ids.Add(statId)) throw Error(sheet, cell, "중복 스탯 ID: " + statId);
                string number = pair[1].Trim();
                if (!number.EndsWith("%", StringComparison.Ordinal)) throw Error(sheet, cell, "스탯 보정 값 끝에 %를 붙이세요.");
                number = number.Substring(0, number.Length - 1);
                if (!float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out float percentage))
                    throw Error(sheet, cell, "스탯 보정 수치가 올바르지 않습니다.");
                modifiers.Add(new GameItemStatModifier { statId = statId, percentage = percentage });
            }
            return modifiers.ToArray();
        }

        // 예: poison; burn. 빈칸이면 해제할 상태 이상이 없습니다.
        private static string[] SplitIds(string value, string sheet, string cell)
        {
            if (string.IsNullOrWhiteSpace(value)) return Array.Empty<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string entry in value.Split(';'))
            {
                string id = entry.Trim();
                CheckId(id, sheet, cell);
                if (!ids.Add(id)) throw Error(sheet, cell, "중복 상태 이상 ID: " + id);
            }
            return ids.ToArray();
        }

        private static string OptionalId(string value, string sheet, string cell)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            CheckId(value, sheet, cell);
            return value;
        }

        private static float Percentage(string value, string sheet, string cell)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0f;
            string number = value.EndsWith("%", StringComparison.Ordinal) ? value.Substring(0, value.Length - 1) : value;
            if (!float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out float percentage))
                throw Error(sheet, cell, "강화 수치는 +10% 또는 10 형식으로 입력하세요.");
            return percentage;
        }
    }
}
