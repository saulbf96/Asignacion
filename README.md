# Asignacion

Consola .NET 10 que unifica archivos Excel de dos fuentes y los inserta en SQL Server.

## Arquitectura (Clean Architecture)

```
Consola ──► Aplicacion ──► Dominio
   │             ▲
   └──► Infraestructura
```

| Proyecto | Responsabilidad |
|---|---|
| `src/Asignacion.Carga.Dominio` | Entidades, enums y excepciones. Sin dependencias. |
| `src/Asignacion.Carga.Aplicacion` | Casos de uso e interfaces (contratos). |
| `src/Asignacion.Carga.Infraestructura` | Implementaciones: lectura de Excel (ClosedXML), SQL Server (SqlClient), manejo de archivos. |
| `src/Asignacion.Carga.Consola` | Punto de entrada: configuración, inyección de dependencias, parámetros, código de salida. |
| `tests/Asignacion.Carga.Pruebas` | Pruebas unitarias (xUnit + Moq). |

## Flujo de ramas

`master` (producción) ← `Preprod` (integración) ← `feature/*`
