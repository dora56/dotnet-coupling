using System.Text.Json;
using Tomlyn.Model;

namespace DotnetCoupling.Core;

internal static class ConfigurationValueReader
{
    internal static void AssertKnownProperties(JsonElement element, string path, IReadOnlyCollection<string> knownProperties)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new ConfigurationException($"{(path.Length == 0 ? "configuration" : path)} must be an object.");
        }

        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!knownProperties.Contains(property.Name))
            {
                string prefix = path.Length == 0 ? "" : path + ".";
                throw new ConfigurationException($"Unknown configuration property: {prefix}{property.Name}");
            }
        }
    }

    internal static void AssertKnownProperties(TomlTable table, string path, IReadOnlyCollection<string> knownProperties)
    {
        foreach (string propertyName in table.Keys)
        {
            if (!knownProperties.Contains(propertyName))
            {
                string prefix = path.Length == 0 ? "" : path + ".";
                throw new ConfigurationException($"Unknown configuration property: {prefix}{propertyName}");
            }
        }
    }

    internal static List<string> ReadStringArray(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new ConfigurationException($"{path} must be an array.");
        }

        List<string> values = [];
        foreach (JsonElement item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                throw new ConfigurationException($"{path} must contain non-empty strings.");
            }

            values.Add(item.GetString()!);
        }

        return values;
    }

    internal static List<string>? ReadStringArray(TomlTable table, string propertyName, string path)
    {
        if (!table.TryGetValue(propertyName, out object? value))
        {
            return null;
        }

        if (value is not TomlArray array)
        {
            throw new ConfigurationException($"{path} must be an array.");
        }

        List<string> values = [];
        foreach (object? item in array)
        {
            if (item is not string stringValue || string.IsNullOrWhiteSpace(stringValue))
            {
                throw new ConfigurationException($"{path} must contain non-empty strings.");
            }

            values.Add(stringValue);
        }

        return values;
    }

    internal static int? ReadPositiveInt(JsonElement element, string propertyName)
    {
        return ReadPositiveInt(element, propertyName, $"thresholds.{propertyName}");
    }

    internal static int? ReadPositiveInt(JsonElement element, string propertyName, string path)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int result) || result <= 0)
        {
            throw new ConfigurationException($"{path} must be a positive integer.");
        }

        return result;
    }

    internal static int? ReadPositiveInt(TomlTable table, string propertyName, string path)
    {
        if (!table.TryGetValue(propertyName, out object? value))
        {
            return null;
        }

        long? result = value switch
        {
            int intValue => intValue,
            long longValue => longValue,
            _ => null,
        };

        if (result is null or <= 0 or > int.MaxValue)
        {
            throw new ConfigurationException($"{path} must be a positive integer.");
        }

        return (int)result.Value;
    }

    internal static double? ReadDoubleInRange(
        JsonElement element,
        string propertyName,
        string path,
        double minimum,
        double maximum)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Number
            || !value.TryGetDouble(out double result)
            || !double.IsFinite(result)
            || result < minimum
            || result > maximum)
        {
            throw new ConfigurationException($"{path} must be between {minimum} and {maximum}.");
        }

        return result;
    }

    internal static double? ReadDoubleInRange(
        TomlTable table,
        string propertyName,
        string path,
        double minimum,
        double maximum)
    {
        if (!table.TryGetValue(propertyName, out object? value))
        {
            return null;
        }

        double? result = value switch
        {
            double doubleValue => doubleValue,
            float floatValue => floatValue,
            int intValue => intValue,
            long longValue => longValue,
            _ => null,
        };

        if (result is null
            || !double.IsFinite(result.Value)
            || result < minimum
            || result > maximum)
        {
            throw new ConfigurationException($"{path} must be between {minimum} and {maximum}.");
        }

        return result;
    }

    internal static List<RawIssueSuppression> ReadIssueSuppressions(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new ConfigurationException("ignore.issues must be an array.");
        }

        List<RawIssueSuppression> suppressions = [];
        int index = 0;
        foreach (JsonElement item in element.EnumerateArray())
        {
            string path = $"ignore.issues[{index}]";
            AssertKnownProperties(item, path, ["type", "source", "target", "reason"]);
            suppressions.Add(new RawIssueSuppression(
                ReadRequiredString(item, path, "type"),
                ReadRequiredString(item, path, "source"),
                ReadRequiredString(item, path, "target"),
                ReadRequiredString(item, path, "reason")));
            index++;
        }

        return suppressions;
    }

    internal static List<RawIssueSuppression>? ReadIssueSuppressions(TomlTable table, string propertyName)
    {
        if (!table.TryGetValue(propertyName, out object? value))
        {
            return null;
        }

        if (value is not TomlTableArray array)
        {
            throw new ConfigurationException("ignore.issues must be an array.");
        }

        List<RawIssueSuppression> suppressions = [];
        int index = 0;
        foreach (TomlTable item in array)
        {
            string path = $"ignore.issues[{index}]";
            AssertKnownProperties(item, path, ["type", "source", "target", "reason"]);
            suppressions.Add(new RawIssueSuppression(
                ReadRequiredString(item, path, "type"),
                ReadRequiredString(item, path, "source"),
                ReadRequiredString(item, path, "target"),
                ReadRequiredString(item, path, "reason")));
            index++;
        }

        return suppressions;
    }

    internal static TomlTable ReadTable(object? value, string path)
    {
        if (value is not TomlTable table)
        {
            throw new ConfigurationException($"{path} must be an object.");
        }

        return table;
    }

    internal static string ReadRequiredString(JsonElement element, string path, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property)
            || property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new ConfigurationException($"{path}.{propertyName} must be a non-empty string.");
        }

        return property.GetString()!;
    }

    internal static string ReadRequiredString(TomlTable table, string path, string propertyName)
    {
        if (!table.TryGetValue(propertyName, out object? value)
            || value is not string stringValue
            || string.IsNullOrWhiteSpace(stringValue))
        {
            throw new ConfigurationException($"{path}.{propertyName} must be a non-empty string.");
        }

        return stringValue;
    }
}
