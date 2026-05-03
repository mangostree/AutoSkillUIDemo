using System;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// 运行时表数据（由 Editor 从 CSV 导出的 .bytes）。方案 A：单文件紧凑二进制，Resources 加载。
/// </summary>
public sealed class SheetTableBin
{
	public const uint Magic = 0x31544853; // "SHT1" 小端
	public const int FormatVersion = 1;

	public string TableName { get; private set; }
	public string[] Headers { get; private set; }
	public string[] ColumnTypes { get; private set; }
	public string[][] Rows { get; private set; }

	SheetTableBin() { }

	public int RowCount => Rows?.Length ?? 0;
	public int ColumnCount => Headers?.Length ?? 0;

	public static bool TryLoad(TextAsset asset, out SheetTableBin table)
	{
		table = null;
		if (asset == null || asset.bytes == null || asset.bytes.Length == 0)
			return false;
		return TryLoad(asset.bytes, out table);
	}

	public static bool TryLoad(byte[] bytes, out SheetTableBin table)
	{
		table = null;
		if (bytes == null || bytes.Length < 12)
			return false;
		try
		{
			using (var ms = new MemoryStream(bytes, writable: false))
			using (var r = new BinaryReader(ms))
			{
				if (r.ReadUInt32() != Magic)
					return false;
				if (r.ReadInt32() != FormatVersion)
					return false;

				var t = new SheetTableBin();
				t.TableName = r.ReadString();
				int colCount = r.ReadInt32();
				if (colCount < 0 || colCount > 4096)
					return false;
				t.Headers = new string[colCount];
				for (int i = 0; i < colCount; i++)
					t.Headers[i] = r.ReadString();
				t.ColumnTypes = new string[colCount];
				for (int i = 0; i < colCount; i++)
					t.ColumnTypes[i] = r.ReadString();
				int rowCount = r.ReadInt32();
				if (rowCount < 0 || rowCount > 1_000_000)
					return false;
				t.Rows = new string[rowCount][];
				for (int ri = 0; ri < rowCount; ri++)
				{
					var row = new string[colCount];
					for (int ci = 0; ci < colCount; ci++)
						row[ci] = r.ReadString();
					t.Rows[ri] = row;
				}
				if (ms.Position != ms.Length)
					return false;
				table = t;
				return true;
			}
		}
		catch
		{
			return false;
		}
	}

	public int GetColumnIndex(string headerName)
	{
		if (Headers == null)
			return -1;
		for (int i = 0; i < Headers.Length; i++)
		{
			if (string.Equals(Headers[i], headerName, StringComparison.Ordinal))
				return i;
		}
		return -1;
	}

	public string GetColumnType(int columnIndex)
	{
		if (ColumnTypes == null || (uint)columnIndex >= (uint)ColumnTypes.Length)
			return "string";
		return ColumnTypes[columnIndex] ?? "string";
	}

	public string GetCell(int rowIndex, string columnName, string defaultValue = "")
	{
		int col = GetColumnIndex(columnName);
		if (col < 0 || (uint)rowIndex >= (uint)Rows.Length)
			return defaultValue;
		string[] row = Rows[rowIndex];
		if ((uint)col >= (uint)row.Length)
			return defaultValue;
		return row[col] ?? defaultValue;
	}

	public bool TryGetCellAsInt(int rowIndex, string columnName, out int value)
	{
		value = 0;
		int col = GetColumnIndex(columnName);
		if (col < 0 || (uint)rowIndex >= (uint)Rows.Length)
			return false;
		if (!string.Equals(GetColumnType(col), "int", StringComparison.OrdinalIgnoreCase))
			return false;
		string raw = Rows[rowIndex][col];
		return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
	}

	public bool TryGetCellAsFloat(int rowIndex, string columnName, out float value)
	{
		value = 0f;
		int col = GetColumnIndex(columnName);
		if (col < 0 || (uint)rowIndex >= (uint)Rows.Length)
			return false;
		string t = GetColumnType(col);
		if (!string.Equals(t, "float", StringComparison.OrdinalIgnoreCase) &&
		    !string.Equals(t, "double", StringComparison.OrdinalIgnoreCase))
			return false;
		string raw = Rows[rowIndex][col];
		return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
	}

	public bool TryGetCellAsListInt(int rowIndex, string columnName, out int[] values)
	{
		values = null;
		int col = GetColumnIndex(columnName);
		if (col < 0 || (uint)rowIndex >= (uint)Rows.Length)
			return false;
		if (!IsListTypeOf(GetColumnType(col), "int"))
			return false;
		string raw = (Rows[rowIndex][col] ?? string.Empty).Trim();
		if (raw.Length >= 2 && raw[0] == '"' && raw[raw.Length - 1] == '"')
			raw = raw.Substring(1, raw.Length - 2).Trim();
		if (string.IsNullOrEmpty(raw))
		{
			values = Array.Empty<int>();
			return true;
		}
		string[] parts = raw.Split(',');
		var arr = new int[parts.Length];
		for (int i = 0; i < parts.Length; i++)
		{
			if (!int.TryParse(parts[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out arr[i]))
				return false;
		}
		values = arr;
		return true;
	}

	static bool IsListTypeOf(string typeToken, string innerSimple)
	{
		if (string.IsNullOrEmpty(typeToken) || typeToken.Length < 7)
			return false;
		if (!typeToken.StartsWith("List<", StringComparison.OrdinalIgnoreCase))
			return false;
		if (!typeToken.EndsWith(">", StringComparison.Ordinal))
			return false;
		string inner = typeToken.Substring(5, typeToken.Length - 6).Trim();
		return string.Equals(inner, innerSimple, StringComparison.OrdinalIgnoreCase);
	}
}
