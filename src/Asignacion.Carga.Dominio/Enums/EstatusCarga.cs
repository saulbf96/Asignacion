namespace Asignacion.Carga.Dominio.Enums;

/// <summary>
/// Estado de una carga registrada en la bitácora.
/// </summary>
public enum EstatusCarga
{
    /// <summary>La carga inició y no ha terminado.</summary>
    EnProceso,

    /// <summary>La carga terminó correctamente.</summary>
    Exitoso,

    /// <summary>La carga falló; las filas parciales se eliminan.</summary>
    Error,

    /// <summary>Fue exitosa, pero el archivo se volvió a cargar de forma forzada.</summary>
    Reemplazado,

    /// <summary>Se volvió a ejecutar, pero el archivo ya estaba cargado: no se cargó nada.</summary>
    Omitido,

    /// <summary>No hubo archivo del día en la ruta de origen.</summary>
    SinArchivo
}
