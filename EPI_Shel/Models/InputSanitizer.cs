using System.Text.RegularExpressions;

namespace EPI_Shel.Models;

public static class InputSanitizer {
    // Patrones sospechosos
    private static readonly string[] SqlKeywords =
    [
        "--", ";--", ";", "/*", "*/", "xp_", "EXEC", "EXECUTE",
        "INSERT", "UPDATE", "DELETE", "DROP", "CREATE", "ALTER",
        "UNION", "SELECT", "CAST(", "CONVERT(", "CHAR(", "NCHAR(",
        "VARCHAR(", "DECLARE", "WAITFOR", "SHUTDOWN"
    ];

    // Solo letras, números, espacios y estos caracteres para descripciones
    private static readonly Regex SafeDescriptionRegex = new(
        @"^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-_.,&()/]+$",
        RegexOptions.Compiled);

    // Solo alfanumérico, guiones y puntos para IDs de producto
    private static readonly Regex SafePartNumRegex = new(
        @"^[a-zA-Z0-9\-_.]+$",
        RegexOptions.Compiled);

    public static bool ContainsSqlThreat(string input) {
        if (string.IsNullOrWhiteSpace(input)) return false;
        var upper = input.ToUpperInvariant();
        return SqlKeywords.Any(k => upper.Contains(k.ToUpperInvariant()));
    }

    public static bool IsValidPartNum(string input) =>
        !string.IsNullOrWhiteSpace(input) && SafePartNumRegex.IsMatch(input);

    public static bool IsValidDescription(string input) =>
        !string.IsNullOrWhiteSpace(input) && SafeDescriptionRegex.IsMatch(input);

    public static string Sanitize(string input) =>
        Regex.Replace(input.Trim(), @"\s{2,}", " ");
}