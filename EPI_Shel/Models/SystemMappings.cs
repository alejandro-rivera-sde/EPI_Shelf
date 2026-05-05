using System.ComponentModel;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace EPI_Shel.Models;

// ── RAW MATERIAL ──────────────────────────────────────────────────────────────
[JsonConverter(typeof(EnumMemberConverter<RawMaterialOption>))]
[TypeConverter(typeof(EnumMemberTypeConverter<RawMaterialOption>))]
public enum RawMaterialOption {
    [EnumMember(Value = "No")]
    No = 0,

    [EnumMember(Value = "Si")]
    Si = 1,
}

// ── VALIDADOR DE NOMENCLATURAS WMS / OMS ──────────────────────────────────────
// WMS y OMS son campos de texto libre. El usuario escribe el codigo directamente.
// Se valida que el valor este en los HashSets y que la combinacion WMS-OMS sea valida.
public static class SystemValidator {

    // ── Codigos WMS aceptados ─────────────────────────────────────────────────
    public static readonly HashSet<string> ValidWmsCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "NVMCLX",   // Calexico
        "NVMMES",   // Dallas y Mesquite
        "NVMMXL",   // Mexicali
        "NVMLRD",   // Nuevo Laredo
        "NLDWMS",   // TEST Nuevo Laredo
        "VSW376",   // TEST Bodega Virtual
    };

    // ── Codigos OMS aceptados ─────────────────────────────────────────────────
    public static readonly HashSet<string> ValidOmsCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "NMCALX",   // Calexico
        "NMPRCA",   // PRIME Calexico
        "NMDALL",   // Dallas y Mesquite
        "NMPRIM",   // PRIME Dallas y Mesquite
        "NMMXLI",   // Mexicali
        "NVOLDO",   // Nuevo Laredo
        "NLDOMS",   // TEST Nuevo Laredo
        "VSO376",   // TEST Bodega Virtual
        "VSOPRI",   // TEST Bodega Virtual PRIME
    };

    // ── Pares WMS para OMS validos ───────────────────────────────────────────────
    // Cada codigo WMS solo puede combinarse con los codigos OMS de su misma ubicacion.
    public static readonly Dictionary<string, HashSet<string>> ValidWmsOmsPairs =
        new(StringComparer.OrdinalIgnoreCase) {
            //Plantas
            ["NVMCLX"] = new(StringComparer.OrdinalIgnoreCase) { "NMCALX", "NMPRCA" },  // Calexico
            ["NVMMES"] = new(StringComparer.OrdinalIgnoreCase) { "NMDALL", "NMPRIM" },  // Dallas y Mesquite
            ["NVMMXL"] = new(StringComparer.OrdinalIgnoreCase) { "NMMXLI" },  // Mexicali
            ["NVMLRD"] = new(StringComparer.OrdinalIgnoreCase) { "NVOLDO" },  // Nuevo Laredo
            //Test Plantas
            ["NLDWMS"] = new(StringComparer.OrdinalIgnoreCase) { "NLDOMS" },  // TEST Nuevo Laredo
            //Test Bodega Virtual
            ["VSW376"] = new(StringComparer.OrdinalIgnoreCase) { "VSO376", "VSOPRI" },  // TEST Nuevo Laredo
        };

    // ── Metodos de validacion individual ─────────────────────────────────────
    public static bool IsValidWms(string code) => ValidWmsCodes.Contains(code.Trim());
    public static bool IsValidOms(string code) => ValidOmsCodes.Contains(code.Trim());

    // ── Validacion de par WMS + OMS ───────────────────────────────────────────
    // Retorna false si el WMS existe en el diccionario pero el OMS no es valido para el.
    public static bool IsValidWmsOmsPair(string wmsCode, string omsCode) {
        if (!ValidWmsOmsPairs.TryGetValue(wmsCode.Trim(), out var validOmsCodes))
            return true; // WMS sin restriccion de par definida

        return validOmsCodes.Contains(omsCode.Trim());
    }

    // ── Helpers para mensajes de error ────────────────────────────────────────
    public static string WmsList => string.Join(", ", ValidWmsCodes);
    public static string OmsList => string.Join(", ", ValidOmsCodes);

    // Retorna los OMS validos para un WMS dado, o cadena vacia si no esta mapeado.
    public static string GetValidOmsForWms(string wmsCode) {
        if (!ValidWmsOmsPairs.TryGetValue(wmsCode.Trim(), out var validOmsCodes))
            return string.Empty;

        return string.Join(", ", validOmsCodes);
    }
}