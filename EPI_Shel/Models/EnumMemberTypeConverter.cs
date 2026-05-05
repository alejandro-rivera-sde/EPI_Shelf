using System.ComponentModel;
using System.Globalization;
using System.Runtime.Serialization;

namespace EPI_Shel.Models;

public class EnumMemberTypeConverter<T> : TypeConverter where T : struct, Enum {
    // Mapa: "texto del [EnumMember]" hacia valor del enum
    private static readonly Dictionary<string, T> _map;

    static EnumMemberTypeConverter() {
        _map = new(StringComparer.OrdinalIgnoreCase);

        foreach (var name in Enum.GetNames<T>()) {
            var value = Enum.Parse<T>(name);
            var member = typeof(T).GetMember(name).First();
            var attr = member
                .GetCustomAttributes(typeof(EnumMemberAttribute), false)
                .Cast<EnumMemberAttribute>()
                .FirstOrDefault();

            var display = attr?.Value ?? name;
            _map[display] = value;  // acepta el texto del dropdown
            _map[name] = value;  // acepta también el nombre del enum como fallback
        }
    }

    public override bool CanConvertFrom(ITypeDescriptorContext? ctx, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(ctx, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? ctx, CultureInfo? culture, object value) {
        if (value is string str && _map.TryGetValue(str, out var result))
            return result;

        return base.ConvertFrom(ctx, culture, value);
    }
}