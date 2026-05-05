using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Runtime.Serialization;

namespace EPI_Shel.Models;

public class EnumMemberSchemaFilter : ISchemaFilter {
    public void Apply(OpenApiSchema schema, SchemaFilterContext context) {
        if (!context.Type.IsEnum) return;

        // Reemplaza los valores del dropdown con los de [EnumMember(Value = "...")]
        schema.Enum.Clear();

        foreach (var name in Enum.GetNames(context.Type)) {
            var member = context.Type
                .GetMember(name)
                .First();

            var enumMember = member
                .GetCustomAttributes(typeof(EnumMemberAttribute), false)
                .Cast<EnumMemberAttribute>()
                .FirstOrDefault();

            // Si tiene [EnumMember], usa ese texto; si no, usa el nombre del enum
            var displayValue = enumMember?.Value ?? name;
            schema.Enum.Add(new OpenApiString(displayValue));
        }
    }
}