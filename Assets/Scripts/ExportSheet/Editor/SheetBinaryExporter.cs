using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 将 <see cref="ExportSheet"/> 写成 <see cref="SheetTableBin"/> 二进制，供运行时 Resources 加载。
/// </summary>
public static class SheetBinaryExporter
{
	public static void WriteFile(ExportSheet sheet, string tableName, string assetPath)
	{
		if (sheet == null)
			throw new System.ArgumentNullException(nameof(sheet));

		string fullPath = AssetPathToFullPath(assetPath);
		string dir = Path.GetDirectoryName(fullPath);
		if (!string.IsNullOrEmpty(dir))
			Directory.CreateDirectory(dir);

		byte[] bytes = BuildBytes(sheet, tableName);
		File.WriteAllBytes(fullPath, bytes);
	}

	/// <summary>写入磁盘；是否立刻出现在 Project 窗口取决于 Editor 的 Auto Refresh，未开启时需手动 Refresh。</summary>
	public static void WriteFileToFullPath(ExportSheet sheet, string tableName, string outputFullPath)
	{
		if (sheet == null)
			throw new System.ArgumentNullException(nameof(sheet));

		outputFullPath = Path.GetFullPath(outputFullPath);
		string dir = Path.GetDirectoryName(outputFullPath);
		if (!string.IsNullOrEmpty(dir))
			Directory.CreateDirectory(dir);

		byte[] bytes = BuildBytes(sheet, tableName);
		File.WriteAllBytes(outputFullPath, bytes);
	}

	static string AssetPathToFullPath(string assetPath)
	{
		assetPath = assetPath.Replace('\\', '/');
		if (Path.IsPathRooted(assetPath))
			return assetPath;
		if (assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
			return Path.GetFullPath(Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length)));
		return Path.GetFullPath(assetPath);
	}

	public static byte[] BuildBytes(ExportSheet sheet, string tableName)
	{
		using (var ms = new MemoryStream())
		{
			using (var w = new BinaryWriter(ms))
			{
				w.Write(SheetTableBin.Magic);
				w.Write(SheetTableBin.FormatVersion);
				w.Write(tableName ?? string.Empty);

				int col = sheet.ColumnCount;
				w.Write(col);
				for (int i = 0; i < col; i++)
					w.Write(sheet.Headers[i] ?? string.Empty);
				for (int i = 0; i < col; i++)
					w.Write(sheet.ColumnTypes[i] ?? string.Empty);

				int rowCount = sheet.RowCount;
				w.Write(rowCount);
				for (int r = 0; r < rowCount; r++)
				{
					IReadOnlyList<string> row = sheet.GetRow(r);
					for (int c = 0; c < col; c++)
					{
						string cell = c < row.Count ? row[c] : string.Empty;
						w.Write(cell ?? string.Empty);
					}
				}

				return ms.ToArray();
			}
		}
	}
}
