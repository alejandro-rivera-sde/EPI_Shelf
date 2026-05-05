using System.Text.Json.Serialization;

namespace EPI_Shel.Models;

// ── REQUEST: POST ──────────────────────────────────────────────────────────────
// RawMaterial: bool? en JSON → true = Si es materia prima | false = No lo es
public class CreateProductRequest {
    [JsonPropertyName("rawMaterial")] public bool? RawMaterial { get; set; }   // true = Si | false = No
    [JsonPropertyName("partNum")] public string PartNum { get; set; } = string.Empty;
    [JsonPropertyName("wmsId")] public string WMS_ID { get; set; } = string.Empty;
    [JsonPropertyName("omsId")] public string OMS_ID { get; set; } = string.Empty;
    [JsonPropertyName("shelfLife")] public int ShelfLife { get; set; }
    [JsonPropertyName("tie")] public int Tie { get; set; }
    [JsonPropertyName("hi")] public int Hi { get; set; }
    [JsonPropertyName("weight")] public double Weight { get; set; }
    [JsonPropertyName("partDescription")] public string PartDescription { get; set; } = string.Empty;
}

// ── REQUEST: PATCH ─────────────────────────────────────────────────────────────
// RawMaterial: bool? en JSON corresponde a: si null, no se modifica en la tabla
public class UpdateProductRequest {
    [JsonPropertyName("partNum")] public string PartNum { get; set; } = string.Empty;
    [JsonPropertyName("wmsId")] public string WMS_ID { get; set; } = string.Empty;
    [JsonPropertyName("omsId")] public string OMS_ID { get; set; } = string.Empty;
    [JsonPropertyName("rawMaterial")] public bool? RawMaterial { get; set; }   // true = Si | false = No | null = no modificar
    [JsonPropertyName("shelfLife")] public int? ShelfLife { get; set; }
    [JsonPropertyName("tie")] public int? Tie { get; set; }
    [JsonPropertyName("hi")] public int? Hi { get; set; }
    [JsonPropertyName("weight")] public double? Weight { get; set; }
    [JsonPropertyName("partDescription")] public string? PartDescription { get; set; }
}

// ── SNAPSHOT para deteccion de cambios en PATCH ───────────────────────────────
// Lee el estado actual de la BD antes del UPDATE para comparar campo por campo.
public class ProductSnapshot {
    public string? WMS_ID { get; set; }
    public string? OMS_ID { get; set; }
    public int RawMaterial { get; set; }   // Almacenado como int (0/1) en SQL
    public int ShelfLife { get; set; }   // Cast de float a int al leer
    public double PalletQty { get; set; }
    public double Weight { get; set; }   // Siempre 0 — no existe en la tabla
    public string PartDescription { get; set; } = string.Empty;
}

// ── RESPONSES ──────────────────────────────────────────────────────────────────
public class ApiResponse {
    [JsonPropertyName("done")] public bool Done { get; set; }
    [JsonPropertyName("data")] public object? Data { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
    [JsonPropertyName("details")] public string? Details { get; set; }

    public static ApiResponse Success(object? data = null) =>
        new() { Done = true, Data = data };

    public static ApiResponse Failure(string error, string? details = null) =>
        new() { Done = false, Error = error, Details = details };
}

// ── RESULTADO COMPLETO — POST ──────────────────────────────────────────────────
// RawMaterial: bool en JSON  corresponde a: false = No es materia prima | true = Si lo es
public class ProductResult {
    [JsonPropertyName("rawMaterial")] public bool RawMaterial { get; set; }   // false = No | true = Si
    [JsonPropertyName("partNum")] public string PartNum { get; set; } = string.Empty;
    [JsonPropertyName("wmsId")] public string? WMS_ID { get; set; }
    [JsonPropertyName("omsId")] public string? OMS_ID { get; set; }
    [JsonPropertyName("shelfLife")] public int ShelfLife { get; set; }
    [JsonPropertyName("tie")] public int Tie { get; set; }
    [JsonPropertyName("hi")] public int Hi { get; set; }
    [JsonPropertyName("palletQty")] public double PalletQty { get; set; }
    [JsonPropertyName("weight")] public double Weight { get; set; }
    [JsonPropertyName("partDescription")] public string PartDescription { get; set; } = string.Empty;
}