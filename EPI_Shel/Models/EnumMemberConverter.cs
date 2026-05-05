using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EPI_Shel.Models;

public class EnumMemberConverter<T> : JsonConverter<T> where T : struct, Enum {
    // Construye el mapa: "texto del EnumMember" hacia valor del enum
    private static readonly Dictionary<string, T> _readMap;
    private static readonly Dictionary<T, string> _writeMap;

    static EnumMemberConverter() {
        _readMap = new(StringComparer.OrdinalIgnoreCase);
        _writeMap = new();

        foreach (var name in Enum.GetNames<T>()) {
            var value = Enum.Parse<T>(name);
            var member = typeof(T).GetMember(name).First();

            var attr = member
                .GetCustomAttributes(typeof(EnumMemberAttribute), false)
                .Cast<EnumMemberAttribute>()
                .FirstOrDefault();

            var display = attr?.Value ?? name;

            _readMap[display] = value;
            _readMap[name] = value;
            _writeMap[value] = display;
        }
    }

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        var text = reader.GetString() ?? string.Empty;
        if (_readMap.TryGetValue(text, out var result)) return result;
        throw new JsonException($"Valor no reconocido para {typeof(T).Name}: '{text}'");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        => writer.WriteStringValue(_writeMap.TryGetValue(value, out var text) ? text : value.ToString());
}