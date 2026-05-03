using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// 通用表格读取：首行表头、第二行可为列类型说明，第三行起为数据。默认逗号分隔（.csv），支持制表符；支持 RFC 风格引号字段。
/// </summary>
public sealed class ExportSheet
{
	static readonly HashSet<string> s_simpleTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"int", "long", "float", "double", "bool", "string"
	};

	readonly string[] _headers;
	readonly string[] _columnTypes;
	readonly List<string[]> _rows;

	ExportSheet(string[] headers, string[] columnTypes, List<string[]> rows)
	{
		_headers = headers;
		_columnTypes = columnTypes;
		_rows = rows;
	}

	public IReadOnlyList<string> Headers => _headers;
	/// <summary>与表头列一一对应；未写类型行时为每列 string。</summary>
	public IReadOnlyList<string> ColumnTypes => _columnTypes;
	public IReadOnlyList<IReadOnlyList<string>> Rows => _rows;
	public int RowCount => _rows.Count;
	public int ColumnCount => _headers.Length;

	public IReadOnlyList<string> GetRow(int rowIndex)
	{
		if ((uint)rowIndex >= (uint)_rows.Count)
			throw new ArgumentOutOfRangeException(nameof(rowIndex));
		return _rows[rowIndex];
	}

	/// <summary>按表头名称取列索引，找不到返回 -1。</summary>
	public int GetColumnIndex(string headerName)
	{
		for (int i = 0; i < _headers.Length; i++)
		{
			if (string.Equals(_headers[i], headerName, StringComparison.Ordinal))
				return i;
		}
		return -1;
	}

	public string GetCell(int rowIndex, string columnName, string defaultValue = "")
	{
		int col = GetColumnIndex(columnName);
		if (col < 0)
			return defaultValue;
		var row = _rows[rowIndex];
		if ((uint)col >= (uint)row.Length)
			return defaultValue;
		return row[col] ?? defaultValue;
	}

	/// <summary>列类型原始字符串，如 int、float、List&lt;int&gt;。</summary>
	public string GetColumnType(int columnIndex)
	{
		if ((uint)columnIndex >= (uint)_columnTypes.Length)
			return "string";
		return _columnTypes[columnIndex] ?? "string";
	}

	/// <summary>
	/// 按类型行声明解析单元格为 int（列类型须为 int，否则失败）。
	/// 示例：if (sheet.TryGetCellAsInt(0, "技能id", out var id)) { … }
	/// </summary>
	public bool TryGetCellAsInt(int rowIndex, string columnName, out int value)
	{
		value = 0;
		int col = GetColumnIndex(columnName);
		if (col < 0 || (uint)rowIndex >= (uint)_rows.Count)
			return false;
		if (!string.Equals(GetColumnType(col), "int", StringComparison.OrdinalIgnoreCase))
			return false;
		string raw = _rows[rowIndex][col];
		return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
	}

	/// <summary>列类型须为 float 或 double（double 也按浮点解析）。</summary>
	public bool TryGetCellAsFloat(int rowIndex, string columnName, out float value)
	{
		value = 0f;
		int col = GetColumnIndex(columnName);
		if (col < 0 || (uint)rowIndex >= (uint)_rows.Count)
			return false;
		string t = GetColumnType(col);
		if (!string.Equals(t, "float", StringComparison.OrdinalIgnoreCase) &&
		    !string.Equals(t, "double", StringComparison.OrdinalIgnoreCase))
			return false;
		string raw = _rows[rowIndex][col];
		return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
	}

	/// <summary>列类型须为 List&lt;int&gt;；单元格如 1,2 或 "1,2" 去引号后按逗号拆分。</summary>
	public bool TryGetCellAsListInt(int rowIndex, string columnName, out int[] values)
	{
		values = null;
		int col = GetColumnIndex(columnName);
		if (col < 0 || (uint)rowIndex >= (uint)_rows.Count)
			return false;
		if (!IsListTypeOf(GetColumnType(col), "int"))
			return false;
		string raw = (_rows[rowIndex][col] ?? string.Empty).Trim();
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

	/// <summary>
	/// 加载表格。path 可为绝对路径，或以 Assets/ 开头的工程相对路径。
	/// </summary>
	/// <param name="encoding">为 null 时：识别 BOM；无 BOM 则先按严格 UTF-8 解码，失败再按 GBK(936)，以兼容 Excel 中文 Windows 另存的 CSV。</param>
	public static bool TryLoad(string path, out ExportSheet sheet, char delimiter = ',', Encoding encoding = null)
	{
		sheet = null;
		if (string.IsNullOrWhiteSpace(path))
			return false;

		string fullPath = ResolvePath(path);
		if (!File.Exists(fullPath))
		{
			Debug.LogWarning($"[ExportSheet] 文件不存在: {fullPath}");
			return false;
		}

		byte[] raw;
		try
		{
			raw = ReadFileBytesShared(fullPath);
		}
		catch (Exception e)
		{
			Debug.LogWarning($"[ExportSheet] 读取失败: {fullPath}\n{e.Message}");
			return false;
		}

		string text;
		try
		{
			text = DecodeCsvText(raw, encoding, fullPath);
		}
		catch (Exception e)
		{
			Debug.LogWarning($"[ExportSheet] 解码失败: {fullPath}\n{e.Message}");
			return false;
		}

		List<string[]> parsed = ParseDelimited(text, delimiter);
		if (parsed == null || parsed.Count == 0)
		{
			Debug.LogWarning($"[ExportSheet] 无有效数据: {fullPath}");
			return false;
		}

		string[] headers = TrimCells(parsed[0]);
		int width = headers.Length;
		if (width == 0)
		{
			Debug.LogWarning($"[ExportSheet] 表头为空: {fullPath}");
			return false;
		}

		string[] columnTypes;
		int dataStartIndex;

		if (parsed.Count >= 2)
		{
			string[] typeRowCandidate = NormalizeRowWidth(TrimCells(parsed[1]), width);
			if (IsLikelyTypeRow(typeRowCandidate))
			{
				columnTypes = new string[width];
				for (int c = 0; c < width; c++)
				{
					string t = typeRowCandidate[c]?.Trim() ?? string.Empty;
					columnTypes[c] = string.IsNullOrEmpty(t) ? "string" : t;
				}
				dataStartIndex = 2;
			}
			else
			{
				columnTypes = BuildDefaultColumnTypes(width);
				dataStartIndex = 1;
			}
		}
		else
		{
			columnTypes = BuildDefaultColumnTypes(width);
			dataStartIndex = 1;
		}

		var data = new List<string[]>(Math.Max(0, parsed.Count - dataStartIndex));
		for (int i = dataStartIndex; i < parsed.Count; i++)
		{
			string[] trimmed = TrimCells(parsed[i]);
			if (IsRowEmpty(trimmed))
				continue;
			data.Add(NormalizeRowWidth(trimmed, width));
		}

		sheet = new ExportSheet(headers, columnTypes, data);
		return true;
	}

	static string[] BuildDefaultColumnTypes(int width)
	{
		var a = new string[width];
		for (int i = 0; i < width; i++)
			a[i] = "string";
		return a;
	}

	/// <summary>第二行是否整行都是合法类型名（含 List&lt;T&gt; 形式）。</summary>
	static bool IsLikelyTypeRow(string[] cells)
	{
		if (cells == null || cells.Length == 0)
			return false;
		for (int i = 0; i < cells.Length; i++)
		{
			string t = cells[i]?.Trim() ?? string.Empty;
			if (string.IsNullOrEmpty(t))
				return false;
			if (s_simpleTypes.Contains(t))
				continue;
			if (IsListTypeSyntax(t))
				continue;
			return false;
		}
		return true;
	}

	static bool IsListTypeSyntax(string t)
	{
		if (t.Length < 6)
			return false;
		if (!t.StartsWith("List<", StringComparison.OrdinalIgnoreCase))
			return false;
		if (!t.EndsWith(">", StringComparison.Ordinal))
			return false;
		string inner = t.Substring(5, t.Length - 6).Trim();
		return inner.Length > 0 && s_simpleTypes.Contains(inner);
	}

	static byte[] ReadFileBytesShared(string fullPath)
	{
		using (var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
		{
			long len = fs.Length;
			if (len > int.MaxValue)
				throw new IOException("CSV 文件过大。");
			var buf = new byte[(int)len];
			int read = 0;
			while (read < buf.Length)
			{
				int n = fs.Read(buf, read, buf.Length - read);
				if (n == 0)
					throw new EndOfStreamException();
				read += n;
			}
			return buf;
		}
	}

	/// <summary>显式 encoding 时按指定解码；encoding 为 null 时 BOM + UTF-8/GBK 自动。</summary>
	static string DecodeCsvText(byte[] bytes, Encoding explicitEncoding, string pathForLog)
	{
		if (explicitEncoding != null)
		{
			int start = 0;
			if (IsUtf8Encoding(explicitEncoding) && bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
				start = 3;
			return explicitEncoding.GetString(bytes, start, bytes.Length - start);
		}

		if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
			return new UTF8Encoding(false, false).GetString(bytes, 3, bytes.Length - 3);
		if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
			return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
		if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
			return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

		try
		{
			return new UTF8Encoding(false, true).GetString(bytes);
		}
		catch (DecoderFallbackException)
		{
		}
		catch (ArgumentException)
		{
		}

		Encoding gbk = TryGetGbkEncoding();
		if (gbk != null)
		{
			Debug.Log($"[ExportSheet] 无 BOM 且非合法 UTF-8，已按 GBK 解码: {pathForLog}");
			return gbk.GetString(bytes);
		}

		Debug.LogWarning($"[ExportSheet] 当前环境无法使用 GBK，已用宽松 UTF-8 解码（可能乱码）: {pathForLog}");
		return new UTF8Encoding(false, false).GetString(bytes);
	}

	static bool IsUtf8Encoding(Encoding enc)
	{
		if (enc is UTF8Encoding)
			return true;
		return enc.CodePage == 65001;
	}

	static Encoding TryGetGbkEncoding()
	{
		try
		{
			return Encoding.GetEncoding(936);
		}
		catch (NotSupportedException)
		{
		}
		catch (ArgumentException)
		{
		}

		TryRegisterCodePagesEncodingProvider();
		try
		{
			return Encoding.GetEncoding(936);
		}
		catch
		{
			return null;
		}
	}

	static void TryRegisterCodePagesEncodingProvider()
	{
		try
		{
			Type providerType = Type.GetType("System.Text.CodePagesEncodingProvider, System.Text.Encoding.CodePages");
			if (providerType == null)
				return;
			object instance = providerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
			if (instance is EncodingProvider ep)
				Encoding.RegisterProvider(ep);
		}
		catch
		{
			// 未引用 System.Text.Encoding.CodePages 包时忽略
		}
	}

	public static string ResolvePath(string path)
	{
		path = path.Trim();
		if (Path.IsPathRooted(path))
			return Path.GetFullPath(path);

		if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
		    path.StartsWith("Assets\\", StringComparison.OrdinalIgnoreCase))
		{
			string relative = path.Substring("Assets/".Length).Replace('\\', Path.DirectorySeparatorChar);
			return Path.GetFullPath(Path.Combine(Application.dataPath, relative));
		}

		return Path.GetFullPath(Path.Combine(Application.dataPath, path.Replace('/', Path.DirectorySeparatorChar)));
	}

	static string[] TrimCells(string[] cells)
	{
		var a = new string[cells.Length];
		for (int i = 0; i < cells.Length; i++)
			a[i] = cells[i]?.Trim() ?? string.Empty;
		return a;
	}

	static bool IsRowEmpty(string[] cells)
	{
		for (int i = 0; i < cells.Length; i++)
		{
			if (!string.IsNullOrWhiteSpace(cells[i]))
				return false;
		}
		return true;
	}

	static string[] NormalizeRowWidth(string[] row, int width)
	{
		if (row.Length == width)
			return row;
		if (row.Length > width)
		{
			var t = new string[width];
			Array.Copy(row, t, width);
			return t;
		}
		var padded = new string[width];
		Array.Copy(row, padded, row.Length);
		for (int i = row.Length; i < width; i++)
			padded[i] = string.Empty;
		return padded;
	}

	/// <summary>支持引号包裹、双引号转义、引号内换行。</summary>
	static List<string[]> ParseDelimited(string text, char delimiter)
	{
		var rows = new List<string[]>();
		var row = new List<string>();
		var field = new StringBuilder();
		bool inQuotes = false;

		for (int i = 0; i < text.Length; i++)
		{
			char c = text[i];
			if (inQuotes)
			{
				if (c == '"')
				{
					if (i + 1 < text.Length && text[i + 1] == '"')
					{
						field.Append('"');
						i++;
					}
					else
						inQuotes = false;
				}
				else
					field.Append(c);
			}
			else
			{
				if (c == '"')
					inQuotes = true;
				else if (c == delimiter)
				{
					row.Add(field.ToString());
					field.Length = 0;
				}
				else if (c == '\n' || c == '\r')
				{
					row.Add(field.ToString());
					field.Length = 0;
					if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
						i++;
					FlushRow(rows, row);
				}
				else
					field.Append(c);
			}
		}

		row.Add(field.ToString());
		FlushRow(rows, row);
		return rows;
	}

	static void FlushRow(List<string[]> rows, List<string> row)
	{
		if (row.Count == 0)
			return;
		if (row.Count == 1 && string.IsNullOrEmpty(row[0]))
		{
			row.Clear();
			return;
		}
		rows.Add(row.ToArray());
		row.Clear();
	}
}
