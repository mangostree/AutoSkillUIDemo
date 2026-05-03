using System.IO;
using UnityEditor;
using UnityEngine;

public static class ExportSheetMenu
{
    const string MenuPath = "Tools/Export Sheet/导表...";

    [MenuItem(MenuPath, priority = 100)]
    static void SelectExcelFile()
    {
        string path = EditorUtility.OpenFilePanelWithFilters(
            "选择 CSV 表格",
            Application.dataPath,
            new[] { "CSV 文件", "csv", "所有文件", "*" });

        if (string.IsNullOrEmpty(path))
        {
            Debug.Log("[ExportSheet] 未选择文件。");
            return;
        }

        Debug.Log($"[ExportSheet] 已选择: {path}");

        if (!ExportSheet.TryLoad(path, out ExportSheet sheet))
        {
            Debug.LogWarning("[ExportSheet] CSV 解析失败。");
            return;
        }

        Debug.Log($"[ExportSheet] 列数 {sheet.ColumnCount}，数据行 {sheet.RowCount}，表头: {string.Join(", ", sheet.Headers)}");
        Debug.Log($"[ExportSheet] 类型行: {string.Join(", ", sheet.ColumnTypes)}");

        string tableName = Path.GetFileNameWithoutExtension(path);
        string binPath = Path.Combine(Application.dataPath, "Resources", "Sheets", tableName + ".bytes");
        SheetBinaryExporter.WriteFileToFullPath(sheet, tableName, binPath);
        Debug.Log($"[ExportSheet] 已写入 Bin: {binPath}（运行时 Resources.Load<TextAsset>(\"Sheets/{tableName}\")）");
    }
}
