using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        public SolicitudesController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
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

            var query = _context.Solicitudes
                .Where(s => s.ClienteId == cliente.Id)
                .AsQueryable();

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
                    query = query.Where(s => s.FechaSolicitud <= filtro.FechaFin.Value);
                }
            }

            filtro.Resultados = await query
                .OrderByDescending(s => s.FechaSolicitud)
                .ToListAsync();

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

            return View(solicitud);
        }
    }
}