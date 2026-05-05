using EPI_Shel.Models;
using EPI_Shel.Services;
using Microsoft.AspNetCore.Mvc;

namespace EPI_Shel.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ProductsController : ControllerBase {
    private readonly IProductService _service;
    public ProductsController(IProductService service) => _service = service;

    // ─────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest req) {
        var errors = new List<string>();

        // ── RawMaterial obligatorio ───────────────────────────────────────────
        if (req.RawMaterial == null)
            errors.Add("Debe indicar si es Materia Prima (rawMaterial: true o false).");

        // ── Campos de texto obligatorios ──────────────────────────────────────
        if (string.IsNullOrWhiteSpace(req.PartNum)) errors.Add("PartNum es obligatorio.");
        if (string.IsNullOrWhiteSpace(req.WMS_ID)) errors.Add("WMS ID es obligatorio.");
        if (string.IsNullOrWhiteSpace(req.OMS_ID)) errors.Add("OMS ID es obligatorio.");
        if (string.IsNullOrWhiteSpace(req.PartDescription)) errors.Add("PartDescription es obligatorio.");

        // ── Validacion de PartNum segun RawMaterial ───────────────────────────
        // false = No es materia prima: sin espacios, maximo 10 caracteres
        // true  = Si es materia prima: permite espacios, sin limite de longitud
        if (req.RawMaterial == false && !string.IsNullOrWhiteSpace(req.PartNum)) {
            if (req.PartNum.Contains(' '))
                errors.Add("PartNum no puede contener espacios cuando no es Materia Prima.");
            if (req.PartNum.Length > 10)
                errors.Add("PartNum no puede exceder 10 caracteres cuando no es Materia Prima.");
        }

        // ── Validacion individual de codigos WMS / OMS ────────────────────────
        if (!string.IsNullOrWhiteSpace(req.WMS_ID) && !SystemValidator.IsValidWms(req.WMS_ID))
            errors.Add($"WMS ID '{req.WMS_ID}' no es valido. Valores aceptados: {SystemValidator.WmsList}.");

        if (!string.IsNullOrWhiteSpace(req.OMS_ID) && !SystemValidator.IsValidOms(req.OMS_ID))
            errors.Add($"OMS ID '{req.OMS_ID}' no es valido. Valores aceptados: {SystemValidator.OmsList}.");

        // ── Validacion de combinacion WMS + OMS ───────────────────────────────
        // Solo se evalua si ambos codigos son individualmente validos (evita mensajes duplicados).
        if (!string.IsNullOrWhiteSpace(req.WMS_ID) && !string.IsNullOrWhiteSpace(req.OMS_ID)
            && SystemValidator.IsValidWms(req.WMS_ID) && SystemValidator.IsValidOms(req.OMS_ID)
            && !SystemValidator.IsValidWmsOmsPair(req.WMS_ID, req.OMS_ID)) {
            var validOms = SystemValidator.GetValidOmsForWms(req.WMS_ID);
            errors.Add($"OMS ID '{req.OMS_ID}' no es valido para WMS '{req.WMS_ID}'. " +
                       $"Valores aceptados para este WMS: {validOms}.");
        }

        // ── Numericos positivos ───────────────────────────────────────────────
        if (req.ShelfLife <= 0) errors.Add("ShelfLife debe ser mayor a 0.");
        if (req.Tie <= 0) errors.Add("Tie debe ser mayor a 0.");
        if (req.Hi <= 0) errors.Add("Hi debe ser mayor a 0.");
        if (req.Weight <= 0) errors.Add("Weight debe ser mayor a 0.");

        // ── Rangos razonables ──────
        //if (req.ShelfLife > 1800)   errors.Add("ShelfLife no puede exceder 1800 dias (5 años).");
        //if (req.Tie       > 500000) errors.Add("Tie no puede exceder 500,000.");
        //if (req.Hi        > 40)     errors.Add("Hi no puede exceder 40.");
        //if (req.Weight    > 1080)   errors.Add("Weight no puede exceder 1,080 kg.");

        // ── Formato de PartDescription ────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(req.PartDescription) && !InputSanitizer.IsValidDescription(req.PartDescription))
            errors.Add("PartDescription contiene caracteres no permitidos.");

        // ── Deteccion de amenazas SQL ─────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(req.PartNum) && InputSanitizer.ContainsSqlThreat(req.PartNum))
            errors.Add("PartNum contiene contenido no permitido.");
        if (!string.IsNullOrWhiteSpace(req.PartDescription) && InputSanitizer.ContainsSqlThreat(req.PartDescription))
            errors.Add("PartDescription contiene contenido no permitido.");

        if (errors.Count > 0)
            return BadRequest(ApiResponse.Failure("VALIDATION_ERROR", string.Join(" | ", errors)));

        // ── Sanitizar y normalizar ────────────────────────────────────────────
        req.PartDescription = InputSanitizer.Sanitize(req.PartDescription);
        req.PartNum = req.PartNum.Trim();
        req.WMS_ID = req.WMS_ID.Trim().ToUpperInvariant();
        req.OMS_ID = req.OMS_ID.Trim().ToUpperInvariant();

        var (success, product, error, details) = await _service.CreateProductAsync(req);

        if (!success) {
            var code = error is "SQL_ERROR" or "SERVER_ERROR"
                ? StatusCodes.Status500InternalServerError
                : StatusCodes.Status400BadRequest;
            return StatusCode(code, ApiResponse.Failure(error!, details));
        }

        return StatusCode(StatusCodes.Status201Created, ApiResponse.Success(product));
    }

    // ─────────────────────────────────────────────────────────────────────────
    [HttpPatch]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateProduct([FromBody] UpdateProductRequest req) {
        var errors = new List<string>();

        // ── Identificadores obligatorios (para el query) ────────────────────
        if (string.IsNullOrWhiteSpace(req.PartNum)) errors.Add("PartNum es obligatorio.");
        if (string.IsNullOrWhiteSpace(req.WMS_ID)) errors.Add("WMS ID es obligatorio.");
        if (string.IsNullOrWhiteSpace(req.OMS_ID)) errors.Add("OMS ID es obligatorio.");

        // ── Validacion de PartNum segun RawMaterial (solo si se provee) ───────
        if (req.RawMaterial == false && !string.IsNullOrWhiteSpace(req.PartNum)) {
            if (req.PartNum.Contains(' '))
                errors.Add("PartNum no puede contener espacios cuando no es Materia Prima.");
            if (req.PartNum.Length > 10)
                errors.Add("PartNum no puede exceder 10 caracteres cuando no es Materia Prima.");
        }

        // ── Validacion individual de codigos WMS / OMS ────────────────────────
        if (!string.IsNullOrWhiteSpace(req.WMS_ID) && !SystemValidator.IsValidWms(req.WMS_ID))
            errors.Add($"WMS ID '{req.WMS_ID}' no es valido. Valores aceptados: {SystemValidator.WmsList}.");

        if (!string.IsNullOrWhiteSpace(req.OMS_ID) && !SystemValidator.IsValidOms(req.OMS_ID))
            errors.Add($"OMS ID '{req.OMS_ID}' no es valido. Valores aceptados: {SystemValidator.OmsList}.");

        // ── Validacion de combinacion WMS + OMS ───────────────────────────────
        if (!string.IsNullOrWhiteSpace(req.WMS_ID) && !string.IsNullOrWhiteSpace(req.OMS_ID)
            && SystemValidator.IsValidWms(req.WMS_ID) && SystemValidator.IsValidOms(req.OMS_ID)
            && !SystemValidator.IsValidWmsOmsPair(req.WMS_ID, req.OMS_ID)) {
            var validOms = SystemValidator.GetValidOmsForWms(req.WMS_ID);
            errors.Add($"OMS ID '{req.OMS_ID}' no es valido para WMS '{req.WMS_ID}'. " +
                       $"Valores aceptados para este WMS: {validOms}.");
        }

        // ── Numericos positivos (solo si el campo fue enviado) ────────────────
        if (req.ShelfLife.HasValue && req.ShelfLife <= 0) errors.Add("ShelfLife debe ser mayor a 0.");
        if (req.Tie.HasValue && req.Tie <= 0) errors.Add("Tie debe ser mayor a 0.");
        if (req.Hi.HasValue && req.Hi <= 0) errors.Add("Hi debe ser mayor a 0.");
        if (req.Weight.HasValue && req.Weight <= 0) errors.Add("Weight debe ser mayor a 0.");

        // ── Rangos razonables ──────
        //if (req.ShelfLife.HasValue && req.ShelfLife > 1800)   errors.Add("ShelfLife no puede exceder 1800 dias (5 años).");
        //if (req.Tie.HasValue       && req.Tie       > 500000) errors.Add("Tie no puede exceder 500,000.");
        //if (req.Hi.HasValue        && req.Hi        > 40)     errors.Add("Hi no puede exceder 40.");
        //if (req.Weight.HasValue    && req.Weight    > 1080)   errors.Add("Weight no puede exceder 1,080 kg.");

        // ── Formato de PartDescription ────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(req.PartDescription) && !InputSanitizer.IsValidDescription(req.PartDescription))
            errors.Add("PartDescription contiene caracteres no permitidos.");

        // ── Deteccion de amenazas SQL ─────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(req.PartNum) && InputSanitizer.ContainsSqlThreat(req.PartNum))
            errors.Add("PartNum contiene contenido no permitido.");
        if (!string.IsNullOrWhiteSpace(req.PartDescription) && InputSanitizer.ContainsSqlThreat(req.PartDescription))
            errors.Add("PartDescription contiene contenido no permitido.");

        if (errors.Count > 0)
            return BadRequest(ApiResponse.Failure("VALIDATION_ERROR", string.Join(" | ", errors)));

        // ── Sanitizar y normalizar ────────────────────────────────────────────
        if (req.PartDescription != null)
            req.PartDescription = InputSanitizer.Sanitize(req.PartDescription);

        req.PartNum = req.PartNum.Trim();
        req.WMS_ID = req.WMS_ID.Trim().ToUpperInvariant();
        req.OMS_ID = req.OMS_ID.Trim().ToUpperInvariant();

        var (success, product, error, details) = await _service.UpdateProductAsync(req);

        if (!success) {
            var code = error is "SQL_ERROR" or "SERVER_ERROR"
                ? StatusCodes.Status500InternalServerError
                : StatusCodes.Status400BadRequest;
            return StatusCode(code, ApiResponse.Failure(error!, details));
        }

        return Ok(ApiResponse.Success(product));
    }
}