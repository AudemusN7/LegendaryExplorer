using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.Classes;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    public static class Bio2DAExtended
    {
        public static void Write2DAToCsv(this Bio2DA twoDA, string path)
        {
            using var writer = new StreamWriter(path, false, new UTF8Encoding(false));

            WriteCsvRow(writer, new List<string>(GetHeaderRow(twoDA)));
            for (int rowIndex = 0; rowIndex < twoDA.RowCount; rowIndex++)
            {
                var rowValues = new string[twoDA.ColumnCount + 1];
                rowValues[0] = twoDA.RowNames[rowIndex];
                for (int columnIndex = 0; columnIndex < twoDA.ColumnCount; columnIndex++)
                {
                    rowValues[columnIndex + 1] = twoDA.Cells[rowIndex, columnIndex]?.DisplayableValue ?? string.Empty;
                }

                WriteCsvRow(writer, rowValues);
            }
        }

        public static Bio2DA ReadCsvTo2DA(ExportEntry export, string filename)
        {
            var rows = ReadCsvRows(filename);
            if (rows.Count == 0)
            {
                MessageBox.Show("CSV is empty.");
                return null;
            }

            var headerRow = rows[0];
            if (headerRow.Count == 0)
            {
                MessageBox.Show("CSV header row is missing.");
                return null;
            }

            var colNames = new List<string>();
            for (int columnIndex = 1; columnIndex < headerRow.Count; columnIndex++)
            {
                colNames.Add(headerRow[columnIndex]);
            }

            var dataRows = new List<List<string>>();
            for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                if (row.TrueForAll(string.IsNullOrWhiteSpace))
                {
                    continue;
                }

                if (row.Count > colNames.Count + 1)
                {
                    MessageBox.Show("CSV contains more values in at least one row than are defined in the header.");
                    return null;
                }

                dataRows.Add(row);
            }

            Bio2DA bio2da = new Bio2DA
            {
                Export = export
            };

            var rowNames = new List<string>(dataRows.Count);
            foreach (var row in dataRows)
            {
                rowNames.Add(row.Count > 0 ? row[0] : string.Empty);
            }

            bio2da.Cells = new Bio2DACell[rowNames.Count, colNames.Count];
            for (int rowIndex = 0; rowIndex < rowNames.Count; rowIndex++)
            {
                for (int columnIndex = 0; columnIndex < colNames.Count; columnIndex++)
                {
                    bio2da.Cells[rowIndex, columnIndex] = new Bio2DACell { package = export.FileRef };
                }
            }

            foreach (var col in colNames)
            {
                bio2da.AddColumn(col);
            }

            foreach (var row in rowNames)
            {
                bio2da.AddRow(row);
            }

            for (int rowIndex = 0; rowIndex < bio2da.RowCount; rowIndex++)
            {
                var sourceRow = dataRows[rowIndex];
                for (int columnIndex = 0; columnIndex < bio2da.ColumnCount; columnIndex++)
                {
                    string cellContents = sourceRow.Count > columnIndex + 1 ? sourceRow[columnIndex + 1] : string.Empty;
                    if (!string.IsNullOrEmpty(cellContents))
                    {
                        Bio2DACell newCell;
                        if (int.TryParse(cellContents, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intVal))
                        {
                            newCell = new Bio2DACell(intVal) { package = export.FileRef };
                        }
                        else if (float.TryParse(cellContents, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float floatVal)
                                 || float.TryParse(cellContents, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out floatVal))
                        {
                            newCell = new Bio2DACell(floatVal) { package = export.FileRef };
                        }
                        else
                        {
                            newCell = new Bio2DACell(cellContents, export.FileRef) { package = export.FileRef };
                        }

                        bio2da[rowIndex, columnIndex] = newCell;
                    }
                    else
                    {
                        bio2da.IsIndexed = true;
                    }
                }
            }

            return bio2da;
        }

        private static IEnumerable<string> GetHeaderRow(Bio2DA twoDA)
        {
            yield return string.Empty;
            foreach (var columnName in twoDA.ColumnNames)
            {
                yield return columnName;
            }
        }

        private static void WriteCsvRow(TextWriter writer, IReadOnlyList<string> values)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (index > 0)
                {
                    writer.Write(',');
                }

                writer.Write(EscapeCsvField(values[index]));
            }

            writer.WriteLine();
        }

        private static string EscapeCsvField(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            bool needsQuotes = value.Contains(',')
                               || value.Contains('"')
                               || value.Contains('\r')
                               || value.Contains('\n');
            return needsQuotes ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
        }

        private static List<List<string>> ReadCsvRows(string path)
        {
            var rows = new List<List<string>>();
            using var reader = new StreamReader(path);

            var currentRow = new List<string>();
            var currentField = new StringBuilder();
            bool inQuotes = false;

            while (true)
            {
                int nextChar = reader.Read();
                if (nextChar == -1)
                {
                    if (currentField.Length > 0 || currentRow.Count > 0)
                    {
                        currentRow.Add(currentField.ToString());
                        rows.Add(currentRow);
                    }

                    return rows;
                }

                char currentChar = (char)nextChar;
                if (inQuotes)
                {
                    if (currentChar == '"')
                    {
                        if (reader.Peek() == '"')
                        {
                            reader.Read();
                            currentField.Append('"');
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        currentField.Append(currentChar);
                    }

                    continue;
                }

                if (currentChar == '"' && currentField.Length == 0)
                {
                    inQuotes = true;
                    continue;
                }

                if (currentChar == ',')
                {
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    continue;
                }

                if (currentChar == '\r' || currentChar == '\n')
                {
                    if (currentChar == '\r' && reader.Peek() == '\n')
                    {
                        reader.Read();
                    }

                    currentRow.Add(currentField.ToString());
                    rows.Add(currentRow);
                    currentRow = new List<string>();
                    currentField.Clear();
                    continue;
                }

                currentField.Append(currentChar);
            }
        }

    }
}
