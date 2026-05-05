using EPI_Shel.Data;
using EPI_Shel.Models;
using Microsoft.AspNetCore.Connections;
using Microsoft.Data.SqlClient;

namespace EPI_Shel.Services;

public interface IProductService {
    Task<(bool Success, ProductResult? Product, string? Error, string? Details)> CreateProductAsync(CreateProductRequest request);
    Task<(bool Success, object? Data, string? Error, string? Details)> UpdateProductAsync(UpdateProductRequest request);
}

public class ProductService : IProductService {
    private readonly IDbConnection _db;
    private readonly ILogger<ProductService> _logger;
    private readonly string _table;   // Leido de appsettings: Database:ShelfLifeTable

    public ProductService(IDbConnection db, ILogger<ProductService> logger, IConfiguration config) {
        _db = db;
        _logger = logger;
        _table = config["Database:ShelfLifeTable"]
                  ?? throw new InvalidOperationException("La clave 'Database:ShelfLifeTable' no está configurada en appsettings.");
    }

    // ───────────────────────────────────────────────────────────────────────────
    // POST: INSERT en dbo.[_table]
    //   Llave unica : PRODUCT ID + WMSID + OMSID
    //   PalletQty   : Tie × Hi  (calculado aqui, no viene del body/json)
    //   RawMaterial : bool en JSON como: true/false se manda como int en SQL (1/0)
    //   ShelfLife   : int en la API; columna en la tabla SQL es float
    // ───────────────────────────────────────────────────────────────────────────
    public async Task<(bool Success, ProductResult? Product, string? Error, string? Details)>
        CreateProductAsync(CreateProductRequest req) {
        try {
            await using var conn = _db.CreateConnection();
            await conn.OpenAsync();

            // ── Verificar duplicado: combinacion unica PRODUCT ID + WMSID + OMSID ──
            string checkSql = $@"
                SELECT COUNT(1) FROM [dbo].[{_table}]
                WHERE [PRODUCT ID] = @PartNum
                  AND [WMSID]      = @WMSID
                  AND [OMSID]      = @OMSID";

            await using (var chk = new SqlCommand(checkSql, conn)) {
                chk.Parameters.AddWithValue("@PartNum", req.PartNum.Trim());
                chk.Parameters.AddWithValue("@WMSID", req.WMS_ID.Trim());
                chk.Parameters.AddWithValue("@OMSID", req.OMS_ID.Trim());

                var count = (int)(await chk.ExecuteScalarAsync() ?? 0);
                if (count > 0)
                    return (false, null, "DUPLICATE_COMBINATION",
                        $"Ya existe un registro con PRODUCT ID='{req.PartNum}', " +
                        $"WMSID='{req.WMS_ID}' y OMSID='{req.OMS_ID}'. Use PATCH para modificarlo.");
            }

            // ── PalletQty = Tie × Hi ──────────────────────────────────────────
            int palletQty = (int)req.Tie * req.Hi;

            // ── bool mandado como int para SQL: true = 1 (Si) | false = 0 (No) ──────────
            int rawMaterialInt = req.RawMaterial == true ? 1 : 0;

            // ── INSERT con OUTPUT para obtener el registro insertado ───────────
            string sql = $@"
                INSERT INTO [dbo].[{_table}]
                    ([WMSID],[OMSID],[PRODUCT ID],[shelf life],[PalletQty],[PartDescription],[RawMaterial])
                OUTPUT
                    INSERTED.[WMSID], INSERTED.[OMSID], INSERTED.[PRODUCT ID],
                    INSERTED.[shelf life], INSERTED.[PalletQty],
                    INSERTED.[PartDescription], INSERTED.[RawMaterial]
                VALUES
                    (@WMSID,@OMSID,@PartNum,@ShelfLife,@PalletQty,@PartDescription,@RawMaterial)";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@PartNum", req.PartNum.Trim());
            cmd.Parameters.AddWithValue("@WMSID", req.WMS_ID.Trim());
            cmd.Parameters.AddWithValue("@OMSID", req.OMS_ID.Trim());
            cmd.Parameters.AddWithValue("@ShelfLife", req.ShelfLife);           // int como float en SQL
            cmd.Parameters.AddWithValue("@PalletQty", palletQty);
            cmd.Parameters.AddWithValue("@PartDescription", req.PartDescription.Trim());
            cmd.Parameters.AddWithValue("@RawMaterial", rawMaterialInt);          // bool como int para SQL

            await using var rdr = await cmd.ExecuteReaderAsync();
            if (await rdr.ReadAsync())
                return (true, MapProduct(rdr, req.Tie, req.Hi, req.Weight), null, null);

            return (false, null, "INSERT_FAILED", "No se pudo insertar el registro.");

        } catch (SqlException ex) {
            _logger.LogError(ex, "SQL error POST {P}", req.PartNum);
            return (false, null, "SQL_ERROR", ex.Message);
        } catch (Exception ex) {
            _logger.LogError(ex, "Error POST {P}", req.PartNum);
            return (false, null, "SERVER_ERROR", ex.Message);
        }
    }

    // ───────────────────────────────────────────────────────────────────────────
    // PATCH: UPDATE en dbo.[_table]
    //   WHERE  : PRODUCT ID + WMSID + OMSID deben coincidir exactamente.
    //   Snapshot: lee el registro actual antes de comparar.
    //   SET    : dinamico — solo los campos que difieren del valor actual.
    //   Guard  : si sets queda vacio como: NO_CHANGES (evita UPDATE sin SET).
    //   Response: identificadores + solo los campos que cambiaron.
    // ───────────────────────────────────────────────────────────────────────────
    public async Task<(bool Success, object? Data, string? Error, string? Details)>
        UpdateProductAsync(UpdateProductRequest req) {
        try {
            await using var conn = _db.CreateConnection();
            await conn.OpenAsync();

            // ── Parametros del WHERE ──────────────────────────────────────────
            const string where = "[PRODUCT ID]=@PartNum AND [WMSID]=@WMSID AND [OMSID]=@OMSID";
            var wp = new List<(string N, object V)> {
                ("@PartNum", req.PartNum.Trim()),
                ("@WMSID",   req.WMS_ID.Trim()),
                ("@OMSID",   req.OMS_ID.Trim()),
            };

            // ── Verificar existencia ──────────────────────────────────────────
            await using (var chk = new SqlCommand(
                $"SELECT COUNT(1) FROM [dbo].[{_table}] WHERE {where}", conn)) {
                foreach (var p in wp) chk.Parameters.AddWithValue(p.N, p.V);
                var count = (int)(await chk.ExecuteScalarAsync() ?? 0);
                if (count == 0)
                    return (false, null, "NOT_FOUND",
                        $"No se encontro ningun registro con PRODUCT ID='{req.PartNum}', " +
                        $"WMSID='{req.WMS_ID}' y OMSID='{req.OMS_ID}'.");
            }

            // ── Leer estado actual (snapshot) ─────────────────────────────────
            // ShelfLife  : columna SQL es float como GetDouble tratado como cast a int.
            // RawMaterial: columna SQL es int como GetInt32 se compara contra bool del request.
            ProductSnapshot current;
            string fetchSql = $@"
                SELECT TOP 1
                    [WMSID],[OMSID],[shelf life],[PalletQty],
                    [PartDescription],[RawMaterial]
                FROM [dbo].[{_table}]
                WHERE [PRODUCT ID]=@PartNum AND [WMSID]=@WMSID AND [OMSID]=@OMSID";

            await using (var fc = new SqlCommand(fetchSql, conn)) {
                foreach (var p in wp) fc.Parameters.AddWithValue(p.N, p.V);
                await using var fr = await fc.ExecuteReaderAsync();
                if (!await fr.ReadAsync())
                    return (false, null, "NOT_FOUND", "No se pudo leer el registro actual.");

                current = new ProductSnapshot {
                    WMS_ID = fr.IsDBNull(fr.GetOrdinal("WMSID")) ? null : fr.GetString(fr.GetOrdinal("WMSID")),
                    OMS_ID = fr.IsDBNull(fr.GetOrdinal("OMSID")) ? null : fr.GetString(fr.GetOrdinal("OMSID")),
                    ShelfLife = fr.IsDBNull(fr.GetOrdinal("shelf life")) ? 0 : (int)fr.GetDouble(fr.GetOrdinal("shelf life")),
                    PalletQty = fr.IsDBNull(fr.GetOrdinal("PalletQty")) ? 0 : fr.GetDouble(fr.GetOrdinal("PalletQty")),
                    PartDescription = fr.IsDBNull(fr.GetOrdinal("PartDescription")) ? "" : fr.GetString(fr.GetOrdinal("PartDescription")),
                    RawMaterial = fr.IsDBNull(fr.GetOrdinal("RawMaterial")) ? 0 : fr.GetInt32(fr.GetOrdinal("RawMaterial")),
                };
            }

            // ── Recalcular PalletQty si viene Tie o Hi ────────────────────────
            // Si solo llega uno de los dos, se infiere el otro desde el PalletQty actual.
            int? newPalletQty = null;
            if (req.Tie.HasValue || req.Hi.HasValue) {
                double cur = current.PalletQty;
                newPalletQty = (req.Tie.HasValue, req.Hi.HasValue) switch {
                    (true, true) => (int)req.Tie!.Value * req.Hi!.Value,
                    (true, false) => (int)req.Tie!.Value * (cur > 0 && req.Tie.Value > 0 ? (int)Math.Round(cur / req.Tie.Value) : 1),
                    (false, true) => (int)(cur > 0 && req.Hi!.Value > 0 ? (int)Math.Round(cur / req.Hi.Value) : 1) * req.Hi!.Value,
                    _ => null
                };
            }

            // ── SET dinamico: acumula solo los campos que realmente cambian ────
            var sets = new List<string>();
            var sp = new List<(string N, object V)>();

            if (req.ShelfLife.HasValue && req.ShelfLife.Value != current.ShelfLife) {
                sets.Add("[shelf life]=@ShelfLife");
                sp.Add(("@ShelfLife", req.ShelfLife.Value));
            }
            if (newPalletQty.HasValue && Math.Abs(newPalletQty.Value - current.PalletQty) > 0.001) {
                sets.Add("[PalletQty]=@PalletQty");
                sp.Add(("@PalletQty", newPalletQty.Value));
            }
            if (req.PartDescription != null && req.PartDescription.Trim() != current.PartDescription.Trim()) {
                sets.Add("[PartDescription]=@PDesc");
                sp.Add(("@PDesc", req.PartDescription.Trim()));
            }
            // RawMaterial: bool → int para comparar con snapshot (int) y para persistir en SQL
            if (req.RawMaterial.HasValue) {
                int newRawInt = req.RawMaterial.Value ? 1 : 0;   // true = 1 | false = 0
                if (newRawInt != current.RawMaterial) {
                    sets.Add("[RawMaterial]=@RawMat");
                    sp.Add(("@RawMat", newRawInt));
                }
            }

            // ── Guard: sin cambios reales → no ejecutar UPDATE ────────────────
            if (sets.Count == 0)
                return (false, null, "NO_CHANGES",
                    "Los valores ingresados son identicos al registro actual. No se realizaron modificaciones.");

            // ── UPDATE con OUTPUT para leer el registro actualizado ───────────
            string updSql = $@"
                UPDATE [dbo].[{_table}]
                SET {string.Join(", ", sets)}
                OUTPUT
                    INSERTED.[WMSID], INSERTED.[OMSID], INSERTED.[PRODUCT ID],
                    INSERTED.[shelf life], INSERTED.[PalletQty],
                    INSERTED.[PartDescription], INSERTED.[RawMaterial]
                WHERE {where}";

            await using var cmd = new SqlCommand(updSql, conn);
            foreach (var p in wp) cmd.Parameters.AddWithValue(p.N, p.V);
            foreach (var p in sp) cmd.Parameters.AddWithValue(p.N, p.V);

            await using var rdr = await cmd.ExecuteReaderAsync();
            if (await rdr.ReadAsync())
                return (true, MapChangedFields(rdr, req, current, newPalletQty), null, null);

            return (false, null, "UPDATE_FAILED", "No se pudo actualizar el registro.");

        } catch (SqlException ex) {
            _logger.LogError(ex, "SQL error PATCH {P}", req.PartNum);
            return (false, null, "SQL_ERROR", ex.Message);
        } catch (Exception ex) {
            _logger.LogError(ex, "Error PATCH {P}", req.PartNum);
            return (false, null, "SERVER_ERROR", ex.Message);
        }
    }

    // ── Mapper completo — POST ────────────────────────────────────────────────
    // RawMaterial: int en SQL se manda como booleano en JSON (0 = false, != 0 = true).
    // ShelfLife  : float en SQL se hace cast a int.
    // Tie/Hi/Weight: del request original (no se almacenan en la tabla).
    private static ProductResult MapProduct(SqlDataReader r, int tie, int hi, double weight) => new() {
        RawMaterial = !r.IsDBNull(r.GetOrdinal("RawMaterial")) && r.GetInt32(r.GetOrdinal("RawMaterial")) != 0,
        WMS_ID = r.IsDBNull(r.GetOrdinal("WMSID")) ? null : r.GetString(r.GetOrdinal("WMSID")),
        OMS_ID = r.IsDBNull(r.GetOrdinal("OMSID")) ? null : r.GetString(r.GetOrdinal("OMSID")),
        PartNum = r.GetString(r.GetOrdinal("PRODUCT ID")),
        ShelfLife = r.IsDBNull(r.GetOrdinal("shelf life")) ? 0 : (int)r.GetDouble(r.GetOrdinal("shelf life")),
        PalletQty = r.IsDBNull(r.GetOrdinal("PalletQty")) ? 0 : r.GetDouble(r.GetOrdinal("PalletQty")),
        PartDescription = r.IsDBNull(r.GetOrdinal("PartDescription")) ? "" : r.GetString(r.GetOrdinal("PartDescription")),
        Tie = tie,
        Hi = hi,
        Weight = weight,
    };

    // ── Mapper de solo cambios — PATCH ────────────────────────────────────────
    // Siempre incluye los tres identificadores.
    // Solo agrega al diccionario los campos que realmente cambiaron.
    // RawMaterial: int en SQL se manda como booleano en JSON.
    private static Dictionary<string, object?> MapChangedFields(
        SqlDataReader r, UpdateProductRequest req,
        ProductSnapshot current, double? newPalletQty) {

        var result = new Dictionary<string, object?>();

        // Identificadores — siempre presentes
        result["partNum"] = r.GetString(r.GetOrdinal("PRODUCT ID"));
        result["wmsId"] = r.IsDBNull(r.GetOrdinal("WMSID")) ? null : r.GetString(r.GetOrdinal("WMSID"));
        result["omsId"] = r.IsDBNull(r.GetOrdinal("OMSID")) ? null : r.GetString(r.GetOrdinal("OMSID"));

        // RawMaterial: bool del request → int para comparar con snapshot → bool en response
        if (req.RawMaterial.HasValue) {
            int newRawInt = req.RawMaterial.Value ? 1 : 0;
            if (newRawInt != current.RawMaterial)
                result["rawMaterial"] = !r.IsDBNull(r.GetOrdinal("RawMaterial")) &&
                                        r.GetInt32(r.GetOrdinal("RawMaterial")) != 0;
        }

        // ShelfLife: int en request y snapshot; cast de float al leer SQL
        if (req.ShelfLife.HasValue && req.ShelfLife.Value != current.ShelfLife)
            result["shelfLife"] = r.IsDBNull(r.GetOrdinal("shelf life")) ? 0 : (int)r.GetDouble(r.GetOrdinal("shelf life"));

        // PalletQty: incluye tie y/o hi que causaron el cambio
        if (newPalletQty.HasValue && Math.Abs(newPalletQty.Value - current.PalletQty) > 0.001) {
            result["palletQty"] = r.IsDBNull(r.GetOrdinal("PalletQty")) ? 0 : r.GetDouble(r.GetOrdinal("PalletQty"));
            if (req.Tie.HasValue) result["tie"] = req.Tie.Value;
            if (req.Hi.HasValue) result["hi"] = req.Hi.Value;
        }

        // Weight: no se persiste en la tabla — excluido del response
        //if (req.Weight.HasValue && Math.Abs(req.Weight.Value - current.Weight) > 0.001)
        //    result["weight"] = req.Weight.Value;

        if (req.PartDescription != null && req.PartDescription.Trim() != current.PartDescription.Trim())
            result["partDescription"] = r.IsDBNull(r.GetOrdinal("PartDescription")) ? "" : r.GetString(r.GetOrdinal("PartDescription"));

        return result;
    }
}