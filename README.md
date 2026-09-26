# Plataforma de Créditos

Plataforma web interna para una entidad financiera para gestionar solicitudes de crédito de sus clientes. Los usuarios autenticados registran solicitudes y los analistas de riesgo las evalúan bajo reglas de negocio automatizadas y manuales.

---

## 🛠️ Stack Tecnológico

- **Framework:** ASP.NET Core MVC (.NET 8) con Razor Views
- **Autenticación y Autorización:** ASP.NET Core Identity con Roles (`Analista`, usuario cliente)
- **Persistencia:** Entity Framework Core + SQLite
- **Sesión y Caché:** Redis (`IDistributedCache`) *(en progreso)*
- **Tiempo Real:** WebSockets / SignalR *(en progreso)*
- **Mensajería Asíncrona:** RabbitMQ en CloudAMQP con confirmación de publicador y consumidor en `BackgroundService` *(en progreso)*
- **Despliegue:** Render.com (Web Service con contenedor y puerto dinámico) *(en progreso)*

---

## 📐 Reglas de Negocio Clave

1. **Límite de Registro de Solicitud (Pregunta 3):**
   - El monto solicitado no puede superar **10 × los ingresos mensuales** del cliente.
2. **Límite de Aprobación de Solicitud (Pregunta 1 y 5):**
   - Un analista **no puede aprobar** una solicitud si el monto solicitado supera **5 × los ingresos mensuales** del cliente.
   - *Diferencia documentada:* El sistema permite que el cliente solicite hasta 10 veces sus ingresos para registro y análisis, pero impone una política de riesgo más estricta para la aprobación efectiva (máximo 5 veces sus ingresos).
3. **Restricción de Concurrencia:**
   - Un cliente solo puede tener **UNA** solicitud en estado `Pendiente` a la vez.
4. **Estado del Cliente:**
   - El cliente debe tener `Activo = true` para crear nuevas solicitudes.

---

## 🗄️ Modelo de Datos y Migraciones

### Modelos de Dominio
- **Cliente:**
  - `Id` (PK)
  - `UsuarioId` (FK hacia `AspNetUsers`)
  - `IngresosMensuales` (> 0)
  - `Activo` (bool)
  - `Solicitudes` (1 a N)
- **SolicitudCredito:**
  - `Id` (PK)
  - `ClienteId` (FK hacia `Clientes`)
  - `MontoSolicitado` (> 0)
  - `FechaSolicitud` (UTC)
  - `Estado` (`Pendiente`, `Aprobado`, `Rechazado`)
  - `MotivoRechazo` (string opcional)

### Migraciones EF Core
Para aplicar las migraciones localmente:
```bash
dotnet ef database update --project PlataformaCreditos
```
Las migraciones incluyen:
1. `00000000000000_CreateIdentitySchema`: Tablas de ASP.NET Core Identity.
2. `20260925011614_InicialDominio`: Creación de tablas `Clientes` y `Solicitudes`.

### Datos de Prueba (SeedData)
Al iniciar la aplicación, se cargan automáticamente:
- **Rol:** `Analista`
- **Analista:** `analista@plataforma.com` / Contraseña: `Analista123!`
- **Cliente 1:** `cliente1@plataforma.com` / Contraseña: `Cliente123!` (Ingresos: $3,000, 1 solicitud `Pendiente` por $5,000)
- **Cliente 2:** `cliente2@plataforma.com` / Contraseña: `Cliente123!` (Ingresos: $2,000, 1 solicitud `Aprobado` por $4,000)

---

## 🚀 Ejecución Local

1. **Levantar Redis local con Docker Compose:**
   ```bash
   docker-compose up -d
   ```
2. **Restaurar dependencias y compilar:**
   ```bash
   dotnet build PlataformaCreditos/PlataformaCreditos.csproj
   ```
3. **Ejecutar la aplicación:**
   ```bash
   dotnet run --project PlataformaCreditos/PlataformaCreditos.csproj
   ```
4. **Acceder a la aplicación:**
   Navegar a `http://localhost:5000` o la URL configurada por Kestrel.

---

## 📋 Estado del Examen Práctico

| Paso / Pregunta | Rama | Descripción | Estado |
|---|---|---|---|
| **Paso 0** | `main` | Bootstrap inicial del repositorio y solución MVC con Identity | ✅ Completado |
| **Pregunta 1** | `feature/bootstrap-dominio` | Modelos `Cliente` y `SolicitudCredito`, SQLite, SeedData y migraciones | ✅ Completado |
| **Pregunta 2** | `feature/catalogo-solicitudes` | Vista "Mis solicitudes", detalle, filtros y validaciones server-side | ✅ Completado |
| **Pregunta 3** | `feature/solicitudes` | Formulario de registro de solicitud con validaciones de negocio (10× ingresos, único pendiente) | ✅ Completado |
| **Pregunta 4** | `feature/sesion-redis` | Sesión y caché distribuida con Redis (`IDistributedCache`), docker-compose local | ✅ Completado |
| **Pregunta 5** | `feature/panel-analista` | Panel de analista protegido por rol, aprobar/rechazar solicitudes | ⏳ Pendiente |
| **Pregunta 6** | `feature/websocket-notificaciones` | Notificaciones en tiempo real vía WebSocket / SignalR con reconexión | ⏳ Pendiente |
| **Pregunta 7** | `feature/cloudmq-notificaciones` | Mensajería asíncrona con RabbitMQ (CloudAMQP) y `BackgroundService` | ⏳ Pendiente |
| **Pregunta 8** | `deploy/render` | Despliegue en Render.com con variables de entorno y SQLite persistente | ⏳ Pendiente |
