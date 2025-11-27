using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using RefactorCsharpMCP.Toon.Internal;

namespace RefactorCsharpMCP.Toon;

/// <summary>
/// Encodes objects to TOON (Token-Oriented Object Notation) format.
/// TOON is a compact, human-readable format optimized for LLM interactions.
/// </summary>
/// <remarks>
/// TOON format rules:
/// - Key-value pairs: "key: value" (no quotes, no braces)
/// - Nested objects: indentation
/// - Multi-line strings: Base64 encoded with "base64:" prefix
/// - Arrays of primitives: "name[count]:" followed by values
/// - Arrays of objects: "name[count]{field1,field2}:" followed by tabular rows
/// </remarks>
public class ToonEncoder : IToonEncoder
{
    /// <summary>
    /// Cache for type property metadata to avoid repeated reflection calls.
    /// Uses ConcurrentDictionary for thread-safe access.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

    private readonly ToonEncoderOptions _defaultOptions;

    /// <summary>
    /// Creates a new TOON encoder with default options.
    /// </summary>
    public ToonEncoder() : this(ToonEncoderOptions.Default)
    {
    }

    /// <summary>
    /// Creates a new TOON encoder with specified default options.
    /// </summary>
    /// <param name="defaultOptions">Default encoding options.</param>
    public ToonEncoder(ToonEncoderOptions defaultOptions)
    {
        _defaultOptions = defaultOptions ?? throw new ArgumentNullException(nameof(defaultOptions));
    }

    /// <inheritdoc />
    public string Encode(object? value)
    {
        return Encode(value, _defaultOptions);
    }

    /// <inheritdoc />
    public string Encode(object? value, ToonEncoderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (value == null)
            return "null";

        var sb = new StringBuilder();
        EncodeValue(value, sb, 0, options, 0);
        return sb.ToString().TrimEnd('\r', '\n');
    }

    /// <summary>
    /// Recursively encodes a value to TOON format.
    /// </summary>
    private void EncodeValue(object? value, StringBuilder sb, int indent, ToonEncoderOptions options, int depth)
    {
        if (depth > options.MaxDepth)
        {
            sb.Append("[max depth exceeded]");
            return;
        }

        if (value == null)
        {
            sb.Append("null");
            return;
        }

        var type = value.GetType();

        // Handle primitives
        if (ArrayFormatter.IsPrimitiveType(type))
        {
            sb.Append(ValueFormatter.FormatPrimitive(value, options));
            return;
        }

        // Handle enumerables (but not strings)
        if (value is IEnumerable enumerable && value is not string)
        {
            EncodeEnumerable(enumerable, sb, indent, options, depth);
            return;
        }

        // Handle objects
        EncodeObject(value, sb, indent, options, depth);
    }

    /// <summary>
    /// Gets cached property metadata for a type, avoiding repeated reflection calls.
    /// </summary>
    /// <param name="type">The type to get properties for.</param>
    /// <returns>Array of readable public instance properties without indexers.</returns>
    internal static PropertyInfo[] GetCachedProperties(Type type)
    {
        return PropertyCache.GetOrAdd(type, t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                .ToArray());
    }

    /// <summary>
    /// Encodes an object's properties to TOON format.
    /// </summary>
    private void EncodeObject(object obj, StringBuilder sb, int indent, ToonEncoderOptions options, int depth)
    {
        var type = obj.GetType();
        var properties = GetCachedProperties(type);

        if (properties.Length == 0)
        {
            sb.Append(obj.ToString() ?? "null");
            return;
        }

        var indentString = new string(' ', indent);
        var isFirst = indent == 0; // Top-level object doesn't need leading newline

        foreach (var prop in properties)
        {
            var propValue = prop.GetValue(obj);
            var propName = ValueFormatter.FormatPropertyName(prop.Name, options);

            // Skip null values at top level for cleaner output
            if (propValue == null && indent == 0)
                continue;

            if (!isFirst && indent == 0)
            {
                sb.AppendLine();
            }
            isFirst = false;

            var propType = propValue?.GetType();

            // Handle nested objects and arrays
            if (propValue != null && !ArrayFormatter.IsPrimitiveType(propType!))
            {
                if (propValue is IEnumerable enumerable && propValue is not string)
                {
                    // Array/collection property
                    EncodeArrayProperty(propName, enumerable, sb, indent, options, depth + 1);
                }
                else
                {
                    // Nested object
                    sb.Append(indentString);
                    sb.Append(propName);
                    sb.AppendLine(":");
                    EncodeObject(propValue, sb, indent + options.IndentSize, options, depth + 1);
                }
            }
            else
            {
                // Simple value
                sb.Append(indentString);
                sb.Append(propName);
                sb.Append(": ");
                EncodeValue(propValue, sb, indent, options, depth + 1);
                if (indent > 0 || prop != properties.Last())
                {
                    // Don't add newline after last property at root level
                }
            }
        }
    }

    /// <summary>
    /// Encodes an array/collection property.
    /// </summary>
    private void EncodeArrayProperty(string propertyName, IEnumerable items, StringBuilder sb, int indent, ToonEncoderOptions options, int depth)
    {
        // Check if we should use tabular format
        if (ArrayFormatter.IsTabularCollection(items, out var elementType, out var properties))
        {
            ArrayFormatter.FormatTabular(sb, propertyName, items, properties!, indent, options);
        }
        else
        {
            ArrayFormatter.FormatPrimitiveArray(sb, propertyName, items, indent, options);
        }
    }

    /// <summary>
    /// Encodes a standalone enumerable (not as a property).
    /// </summary>
    private void EncodeEnumerable(IEnumerable items, StringBuilder sb, int indent, ToonEncoderOptions options, int depth)
    {
        var itemList = items.Cast<object?>().ToList();

        if (itemList.Count == 0)
        {
            sb.Append("[]");
            return;
        }

        // Check if tabular
        if (ArrayFormatter.IsTabularCollection(items, out var elementType, out var properties))
        {
            var indentString = new string(' ', indent);
            var fieldNames = properties!
                .Select(p => ValueFormatter.FormatPropertyName(p.Name, options))
                .ToArray();

            sb.Append('[');
            sb.Append(itemList.Count);
            sb.Append("]{");
            sb.Append(string.Join(",", fieldNames));
            sb.AppendLine("}:");

            var rowIndent = new string(' ', indent + options.IndentSize);
            foreach (var item in itemList)
            {
                sb.Append(rowIndent);
                if (item == null)
                {
                    sb.AppendLine(string.Join(",", properties!.Select(_ => "null")));
                }
                else
                {
                    var values = properties!.Select(p =>
                    {
                        var v = p.GetValue(item);
                        return ValueFormatter.FormatPrimitive(v, options);
                    });
                    sb.AppendLine(string.Join(",", values));
                }
            }
        }
        else
        {
            // Primitive array
            sb.Append('[');
            sb.Append(itemList.Count);
            sb.AppendLine("]:");

            var valueIndent = new string(' ', indent + options.IndentSize);
            foreach (var item in itemList)
            {
                sb.Append(valueIndent);
                sb.AppendLine(ValueFormatter.FormatPrimitive(item, options));
            }
        }
    }
}
