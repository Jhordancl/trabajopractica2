using System.ComponentModel.DataAnnotations;

namespace PlataformaCreditos.Models.ViewModels
{
    public class SolicitudCreateViewModel
    {
        [Required(ErrorMessage = "El monto solicitado es obligatorio.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El monto solicitado debe ser mayor a 0.")]
        [Display(Name = "Monto Solicitado")]
        public decimal MontoSolicitado { get; set; }

        // Datos informativos del cliente para visualización
        public decimal IngresosMensuales { get; set; }
        public decimal MontoMaximoPermitido => IngresosMensuales * 10;
        public bool ClienteActivo { get; set; } = true;
        public bool TieneSolicitudPendiente { get; set; }
    }
}
