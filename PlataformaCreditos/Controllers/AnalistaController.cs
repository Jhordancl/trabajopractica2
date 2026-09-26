using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Controllers
{
    [Authorize(Roles = "Analista")]
    public class AnalistaController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IDistributedCache _cache;
        private readonly ILogger<AnalistaController> _logger;

        public AnalistaController(
            ApplicationDbContext context,
            IDistributedCache cache,
            ILogger<AnalistaController> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        // GET: /Analista
        public async Task<IActionResult> Index()
        {
            var solicitudesPendientes = await _context.Solicitudes
                .Include(s => s.Cliente)
                .Where(s => s.Estado == EstadoSolicitud.Pendiente)
                .OrderBy(s => s.FechaSolicitud)
                .ToListAsync();

            return View(solicitudesPendientes);
        }

        // POST: /Analista/Aprobar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Aprobar(int id)
        {
            var solicitud = await _context.Solicitudes
                .Include(s => s.Cliente)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (solicitud is null)
            {
                TempData["Error"] = "La solicitud no fue encontrada.";
                return RedirectToAction(nameof(Index));
            }

            // Validación: No procesar solicitudes que ya estén Aprobadas o Rechazadas
            if (solicitud.Estado != EstadoSolicitud.Pendiente)
            {
                TempData["Error"] = $"La solicitud #{solicitud.Id} ya fue procesada anteriormente y está en estado {solicitud.Estado}.";
                return RedirectToAction(nameof(Index));
            }

            // Validación de negocio: No aprobar si MontoSolicitado > 5 × IngresosMensuales
            var ingresos = solicitud.Cliente?.IngresosMensuales ?? 0m;
            var limiteAprobacion = ingresos * 5;

            if (solicitud.MontoSolicitado > limiteAprobacion)
            {
                TempData["Error"] = $"No se puede aprobar la solicitud #{solicitud.Id}: El monto solicitado ({solicitud.MontoSolicitado:C2}) supera el límite máximo de aprobación de 5 veces los ingresos mensuales ({limiteAprobacion:C2}).";
                return RedirectToAction(nameof(Index));
            }

            solicitud.Estado = EstadoSolicitud.Aprobado;
            solicitud.MotivoRechazo = null;
            await _context.SaveChangesAsync();

            // Invalidar caché Redis del cliente
            await InvalidarCacheClienteAsync(solicitud.ClienteId);

            TempData["Exito"] = $"¡Solicitud #{solicitud.Id} aprobada exitosamente!";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Analista/Rechazar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Rechazar(int id, string motivoRechazo)
        {
            var solicitud = await _context.Solicitudes
                .Include(s => s.Cliente)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (solicitud is null)
            {
                TempData["Error"] = "La solicitud no fue encontrada.";
                return RedirectToAction(nameof(Index));
            }

            // Validación: No procesar solicitudes que ya estén Aprobadas o Rechazadas
            if (solicitud.Estado != EstadoSolicitud.Pendiente)
            {
                TempData["Error"] = $"La solicitud #{solicitud.Id} ya fue procesada anteriormente y está en estado {solicitud.Estado}.";
                return RedirectToAction(nameof(Index));
            }

            // Validación de negocio: MotivoRechazo obligatorio
            if (string.IsNullOrWhiteSpace(motivoRechazo))
            {
                TempData["Error"] = "El motivo de rechazo es obligatorio para denegar la solicitud.";
                return RedirectToAction(nameof(Index));
            }

            solicitud.Estado = EstadoSolicitud.Rechazado;
            solicitud.MotivoRechazo = motivoRechazo.Trim();
            await _context.SaveChangesAsync();

            // Invalidar caché Redis del cliente
            await InvalidarCacheClienteAsync(solicitud.ClienteId);

            TempData["Exito"] = $"Solicitud #{solicitud.Id} rechazada con éxito.";
            return RedirectToAction(nameof(Index));
        }

        private async Task InvalidarCacheClienteAsync(int clienteId)
        {
            var cacheKey = $"solicitudes_cliente_{clienteId}";
            try
            {
                await _cache.RemoveAsync(cacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al invalidar caché Redis para la clave {Key}", cacheKey);
            }
        }
    }
}
