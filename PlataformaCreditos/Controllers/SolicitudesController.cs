using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;
using PlataformaCreditos.Models.ViewModels;

namespace PlataformaCreditos.Controllers
{
    [Authorize]
    public class SolicitudesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IDistributedCache _cache;
        private readonly ILogger<SolicitudesController> _logger;

        public SolicitudesController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            IDistributedCache cache,
            ILogger<SolicitudesController> logger)
        {
            _context = context;
            _userManager = userManager;
            _cache = cache;
            _logger = logger;
        }

        // GET: /Solicitudes
        public async Task<IActionResult> Index(SolicitudFiltroViewModel filtro)
        {
            var userId = _userManager.GetUserId(User);

            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.UsuarioId == userId);

            if (cliente is null)
            {
                filtro.Resultados = new List<SolicitudCredito>();
                return View(filtro);
            }

            // Validaciones server-side de rangos
            if (filtro.MontoMinimo is < 0 || filtro.MontoMaximo is < 0)
            {
                ModelState.AddModelError(string.Empty, "Los montos no pueden ser negativos.");
            }

            if (filtro.FechaInicio.HasValue && filtro.FechaFin.HasValue
                && filtro.FechaInicio > filtro.FechaFin)
            {
                ModelState.AddModelError(string.Empty, "La fecha de inicio no puede ser mayor a la fecha fin.");
            }

            // Caché distribuida de 60 segundos por cliente
            var cacheKey = $"solicitudes_cliente_{cliente.Id}";
            List<SolicitudCredito>? solicitudes = null;

            try
            {
                var cachedData = await _cache.GetStringAsync(cacheKey);
                if (!string.IsNullOrEmpty(cachedData))
                {
                    solicitudes = JsonSerializer.Deserialize<List<SolicitudCredito>>(cachedData);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al leer de la caché Redis para {Key}. Se consultará la BD.", cacheKey);
            }

            if (solicitudes is null)
            {
                solicitudes = await _context.Solicitudes
                    .Where(s => s.ClienteId == cliente.Id)
                    .OrderByDescending(s => s.FechaSolicitud)
                    .ToListAsync();

                try
                {
                    var serialized = JsonSerializer.Serialize(solicitudes);
                    await _cache.SetStringAsync(cacheKey, serialized, new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al escribir en la caché Redis para {Key}.", cacheKey);
                }
            }

            IEnumerable<SolicitudCredito> query = solicitudes;

            if (ModelState.IsValid)
            {
                if (filtro.Estado.HasValue)
                {
                    query = query.Where(s => s.Estado == filtro.Estado.Value);
                }

                if (filtro.MontoMinimo.HasValue)
                {
                    query = query.Where(s => s.MontoSolicitado >= filtro.MontoMinimo.Value);
                }

                if (filtro.MontoMaximo.HasValue)
                {
                    query = query.Where(s => s.MontoSolicitado <= filtro.MontoMaximo.Value);
                }

                if (filtro.FechaInicio.HasValue)
                {
                    query = query.Where(s => s.FechaSolicitud >= filtro.FechaInicio.Value);
                }

                if (filtro.FechaFin.HasValue)
                {
                    query = query.Where(s => s.FechaSolicitud <= filtro.FechaFin.Value.Date.AddDays(1).AddTicks(-1));
                }
            }

            filtro.Resultados = query.ToList();

            return View(filtro);
        }

        // GET: /Solicitudes/Detalle/5
        public async Task<IActionResult> Detalle(int id)
        {
            var userId = _userManager.GetUserId(User);

            var solicitud = await _context.Solicitudes
                .Include(s => s.Cliente)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (solicitud is null)
            {
                return NotFound();
            }

            // Seguridad: que la solicitud pertenezca al usuario autenticado
            if (solicitud.Cliente?.UsuarioId != userId)
            {
                return Forbid();
            }

            // Guardar en sesión respaldada por Redis la última solicitud visitada
            HttpContext.Session.SetString("UltimaSolicitud_Id", solicitud.Id.ToString());
            HttpContext.Session.SetString("UltimaSolicitud_Monto", solicitud.MontoSolicitado.ToString("C"));

            return View(solicitud);
        }

        // GET: /Solicitudes/Crear
        public async Task<IActionResult> Crear()
        {
            var userId = _userManager.GetUserId(User);
            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.UsuarioId == userId);

            if (cliente is null)
            {
                ViewBag.Error = "No se encontró un perfil de cliente asociado a este usuario.";
                return View(new SolicitudCreateViewModel { ClienteActivo = false });
            }

            var tienePendiente = await _context.Solicitudes
                .AnyAsync(s => s.ClienteId == cliente.Id && s.Estado == EstadoSolicitud.Pendiente);

            var model = new SolicitudCreateViewModel
            {
                IngresosMensuales = cliente.IngresosMensuales,
                ClienteActivo = cliente.Activo,
                TieneSolicitudPendiente = tienePendiente
            };

            return View(model);
        }

        // POST: /Solicitudes/Crear
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(SolicitudCreateViewModel model)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.UsuarioId == userId);

            if (cliente is null)
            {
                ModelState.AddModelError(string.Empty, "No existe un perfil de cliente registrado para su cuenta.");
                model.ClienteActivo = false;
                return View(model);
            }

            // Datos para preservar en la vista si falla
            model.IngresosMensuales = cliente.IngresosMensuales;
            model.ClienteActivo = cliente.Activo;

            // Validación 1: Cliente debe estar Activo
            if (!cliente.Activo)
            {
                ModelState.AddModelError(string.Empty, "Su cuenta de cliente está inactiva. No puede solicitar créditos.");
            }

            // Validación 2: No permitir más de una solicitud Pendiente
            var tienePendiente = await _context.Solicitudes
                .AnyAsync(s => s.ClienteId == cliente.Id && s.Estado == EstadoSolicitud.Pendiente);
            model.TieneSolicitudPendiente = tienePendiente;

            if (tienePendiente)
            {
                ModelState.AddModelError(string.Empty, "Ya tiene una solicitud en estado Pendiente. Debe esperar su evaluación antes de solicitar otra.");
            }

            // Validación 3: MontoSolicitado > 0
            if (model.MontoSolicitado <= 0)
            {
                ModelState.AddModelError(nameof(model.MontoSolicitado), "El monto solicitado debe ser mayor a 0.");
            }

            // Validación 4: MontoSolicitado <= 10 * IngresosMensuales
            var limiteMaximo = cliente.IngresosMensuales * 10;
            if (model.MontoSolicitado > limiteMaximo)
            {
                ModelState.AddModelError(nameof(model.MontoSolicitado),
                    $"El monto solicitado ({model.MontoSolicitado:C2}) no puede superar 10 veces sus ingresos mensuales ({limiteMaximo:C2}).");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Crear solicitud
            var solicitud = new SolicitudCredito
            {
                ClienteId = cliente.Id,
                MontoSolicitado = model.MontoSolicitado,
                FechaSolicitud = DateTime.UtcNow,
                Estado = EstadoSolicitud.Pendiente
            };

            _context.Solicitudes.Add(solicitud);
            await _context.SaveChangesAsync();

            // Invalidar caché Redis del listado de solicitudes del cliente
            var cacheKey = $"solicitudes_cliente_{cliente.Id}";
            try
            {
                await _cache.RemoveAsync(cacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al invalidar caché Redis para {Key}.", cacheKey);
            }

            TempData["MensajeExito"] = $"¡Solicitud #{solicitud.Id} registrada exitosamente por un monto de {solicitud.MontoSolicitado:C2}!";
            return RedirectToAction(nameof(Detalle), new { id = solicitud.Id });
        }
    }
}