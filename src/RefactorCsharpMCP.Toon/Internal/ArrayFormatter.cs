using System.Collections;
using System.Reflection;
using System.Text;

namespace RefactorCsharpMCP.Toon.Internal;

/// <summary>
/// Handles formatting of arrays and collections for TOON encoding.
/// Supports both primitive arrays and tabular object arrays.
/// </summary>
internal static class ArrayFormatter
{
    /// <summary>
    /// Determines if an enumerable contains objects that should be formatted as a table.
    /// Returns true for collections of objects with public properties (excluding primitives/strings).
    /// </summary>
    public static bool IsTabularCollection(IEnumerable items, out Type? elementType, out PropertyInfo[]? properties)
    {
        elementType = null;
        properties = null;

        // Get first non-null element to determine type
        object? firstItem = null;
        foreach (var item in items)
        {
            if (item != null)
            {
                firstItem = item;
                break;
            }
        }

        if (firstItem == null)
            return false;

        elementType = firstItem.GetType();

        // Primitives, strings, and value types (except structs with properties) are not tabular
        if (IsPrimitiveType(elementType))
            return false;

        // Get public instance properties
        properties = elementType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToArray();

        // Need at least one property for tabular format
        return properties.Length > 0;
    }

    /// <summary>
    /// Formats an array of objects as a TOON tabular structure.
    /// Format: name[count]{field1,field2,...}:\n  value1,value2,...\n  ...
    /// </summary>
    public static void FormatTabular(
        StringBuilder sb,
        string propertyName,
        IEnumerable items,
        PropertyInfo[] properties,
        int indent,
        ToonEncoderOptions options)
    {
        var indentString = new string(' ', indent);
        var itemList = items.Cast<object?>().ToList();

        // Build schema header: name[count]{field1,field2,...}:
        var fieldNames = properties
            .Select(p => ValueFormatter.FormatPropertyName(p.Name, options))
            .ToArray();

        sb.Append(indentString);
        sb.Append(ValueFormatter.FormatPropertyName(propertyName, options));
        sb.Append('[');
        sb.Append(itemList.Count);
        sb.Append("]{");
        sb.Append(string.Join(",", fieldNames));
        sb.AppendLine("}:");

        // Format each row
        var rowIndent = new string(' ', indent + options.IndentSize);
        foreach (var item in itemList)
        {
            sb.Append(rowIndent);
            FormatRow(sb, item, properties, options);
            sb.AppendLine();
        }
    }

    /// <summary>
    /// Formats a single row of tabular data.
    /// </summary>
    private static void FormatRow(StringBuilder sb, object? item, PropertyInfo[] properties, ToonEncoderOptions options)
    {
        if (item == null)
        {
            sb.Append(string.Join(",", properties.Select(_ => "null")));
            return;
        }

        var values = new List<string>();
        foreach (var prop in properties)
        {
            var value = prop.GetValue(item);
            var formattedValue = FormatCellValue(value, options);
            values.Add(formattedValue);
        }

        sb.Append(string.Join(",", values));
    }

    /// <summary>
    /// Formats a single cell value for tabular output.
    /// Handles nested objects by converting them to a compact representation.
    /// </summary>
    private static string FormatCellValue(object? value, ToonEncoderOptions options)
    {
        if (value == null)
            return "null";

        var type = value.GetType();

        // Handle primitives directly
        if (IsPrimitiveType(type))
        {
            return ValueFormatter.FormatPrimitive(value, options);
        }

        // Handle nested objects - use JSON-like compact notation for cells
        // This maintains readability while keeping rows parseable
        if (value is IEnumerable enumerable && value is not string)
        {
            var items = enumerable.Cast<object?>().Select(i => FormatCellValue(i, options));
            return $"[{string.Join(";", items)}]";
        }

        // Complex objects in cells - serialize key properties
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .Take(3) // Limit to avoid overly long cells
            .ToArray();

        if (props.Length == 0)
            return value.ToString() ?? "null";

        var parts = props.Select(p =>
        {
            var v = p.GetValue(value);
            var name = ValueFormatter.FormatPropertyName(p.Name, options);
            var formatted = FormatCellValue(v, options);
            return $"{name}={formatted}";
        });

        return $"{{{string.Join(";", parts)}}}";
    }

    /// <summary>
    /// Formats a simple array of primitives.
    /// Format: name[count]:\n  value1\n  value2\n  ...
    /// </summary>
    public static void FormatPrimitiveArray(
        StringBuilder sb,
        string propertyName,
        IEnumerable items,
        int indent,
        ToonEncoderOptions options)
    {
        var indentString = new string(' ', indent);
        var itemList = items.Cast<object?>().ToList();

        // Header: name[count]:
        sb.Append(indentString);
        sb.Append(ValueFormatter.FormatPropertyName(propertyName, options));
        sb.Append('[');
        sb.Append(itemList.Count);
        sb.AppendLine("]:");

        // Values
        var valueIndent = new string(' ', indent + options.IndentSize);
        foreach (var item in itemList)
        {
            sb.Append(valueIndent);
            sb.AppendLine(ValueFormatter.FormatPrimitive(item, options));
        }
    }

    /// <summary>
    /// Checks if a type is a primitive type (including string, DateTime, Guid, etc.).
    /// </summary>
    public static bool IsPrimitiveType(Type type)
    {
        return type.IsPrimitive
            || type == typeof(string)
            || type == typeof(decimal)
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(TimeSpan)
            || type == typeof(Guid)
            || type.IsEnum
            || (Nullable.GetUnderlyingType(type)?.IsPrimitive ?? false);
    }
}
