# Asignacion

Consola .NET 10 que toma el archivo Excel del día de cada fuente y lo inserta en SQL Server,
dejando bitácora de cada carga y avisando por correo (inicio, fin, error, sin archivo, ya cargado).

## Arquitectura (Clean Architecture)

```
Consola ──► Aplicacion ──► Dominio
   │             ▲
   └──► Infraestructura
```

| Proyecto | Responsabilidad |
|---|---|
| `src/Asignacion.Carga.Dominio` | Entidades, enums, excepciones y reglas. Sin dependencias. |
| `src/Asignacion.Carga.Aplicacion` | Caso de uso de la carga, interfaces (contratos) y textos de las notificaciones. |
| `src/Asignacion.Carga.Infraestructura` | SQL Server (`ASIG.SP_CARGA`), lectura de Excel por flujo (OpenXml), búsqueda de archivos, correo (MailKit). |
| `src/Asignacion.Carga.Consola` | Punto de entrada: parámetros, configuración, inyección de dependencias, log y código de salida. |
| `tests/Asignacion.Carga.Pruebas` | Pruebas unitarias (xUnit + Moq) e integración contra SQL local. |

Los scripts de base de datos y su rollback se mantienen fuera del repositorio.

## Ejecución

```
Asignacion.Carga.exe [--fuente N] [--fecha yyyy-MM-dd] [--forzar] [--ambiente NOMBRE] [--ayuda]
```

| Parámetro | Por omisión | Uso |
|---|---|---|
| `--fuente N` | todas las activas | Procesa solo una fuente de `ASIG.CONFIG_FUENTE` |
| `--fecha` | hoy | Día del archivo a buscar |
| `--forzar` | no | Recarga aunque el archivo ya esté cargado con éxito |
| `--ambiente` | `Local` | Usa `appsettings.{ambiente}.json` (también la variable `ASIGNACION_AMBIENTE`) |

| Código de salida | Significado |
|---|---|
| `0` | Cargado, o ya estaba cargado (no se cargó nada) |
| `1` | Error en alguna fuente, o el proceso no pudo ejecutarse |
| `2` | Parámetros inválidos |
| `3` | No hubo archivo del día |

## Configuración

- `appsettings.json`: valores comunes (correo, log).
- `appsettings.{Ambiente}.json`: cadena de conexión de cada ambiente (Windows Authentication, sin contraseñas).
- Rutas, patrón del archivo y correos destino: en la base (`ASIG.CONFIG_FUENTE`, `ASIG.CONFIG_NOTIFICACION`).
- Contraseña del correo remitente: **solo** en la variable de entorno `ASIGNACION_SMTP_PASSWORD`
  (en Gmail, una contraseña de aplicación):

  ```powershell
  [Environment]::SetEnvironmentVariable("ASIGNACION_SMTP_PASSWORD", "<contraseña>", "User")
  ```

  Para un job, la variable debe existir para la cuenta con la que corre el job.
- Log: `logs/carga-yyyyMMdd.log` junto al ejecutable (se conservan 60 días).

## Pruebas

```
dotnet test
```

La prueba de integración (`Integracion/CargaCompletaSqlTests`) solo corre si existe la variable
`ASIGNACION_SQL_PRUEBAS` con la cadena de conexión a una base `*_LOCAL`, porque borra `ASIG.MOVILIDAD`.

## Flujo de ramas

`master` (producción) ← `Preprod` (integración) ← `feature/*`
