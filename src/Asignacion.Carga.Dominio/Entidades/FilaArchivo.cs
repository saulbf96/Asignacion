namespace Asignacion.Carga.Dominio.Entidades;

/// <summary>
/// Una fila de datos del Excel. La llave es el encabezado tal cual viene en el archivo
/// (p. ej. "id Campania") y el valor es el dato de la celda (texto, número, fecha o null).
/// </summary>
/// <param name="Numero">Número de fila en el Excel (base 1), útil para reportar errores.</param>
public sealed record FilaArchivo(int Numero, IReadOnlyDictionary<string, object?> Valores);
