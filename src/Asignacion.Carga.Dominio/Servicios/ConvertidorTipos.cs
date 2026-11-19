using System.Globalization;
using Asignacion.Carga.Dominio.Entidades;

namespace Asignacion.Carga.Dominio.Servicios;

/// <summary>
/// Conversión de tipo de un valor de texto, con el mismo resultado que el "Tipo cambiado" + "Errores reemplazados"
/// del Power Query de WFM (verificado celda por celda contra Cartera_ISB_19112026). Devuelve texto normalizado
/// (invariante) para que el Excel lo escriba como número o fecha.
/// Como Power Query: acepta coma de miles ("2,3" = 23), signo al final ("202 -" = -202) y exponente ("7e2" = 700);
/// "NULL" = vacío; lo demás que no es número = 0.
/// </summary>
public static class ConvertidorTipos
{
    private static readonly DateTime FechaMinimaExcel = new(1900, 3, 1);

    private const NumberStyles EstiloNumero =
        NumberStyles.Float | NumberStyles.AllowThousands | NumberStyles.AllowTrailingSign;

    public static string? Convertir(string? valor, TipoColumna tipo)
    {
        if (tipo == TipoColumna.Texto) return valor;
        if (string.IsNullOrWhiteSpace(valor)) return null; // vacío sigue vacío (Power Query: null)
        var texto = valor.Trim();
        if (tipo != TipoColumna.Fecha && texto.Equals("NULL", StringComparison.OrdinalIgnoreCase)) return null;

        return tipo switch
        {
            TipoColumna.Entero => Numero(texto, out var n) && Math.Abs(n) < long.MaxValue
                ? Math.Round(n, MidpointRounding.ToEven).ToString("0", CultureInfo.InvariantCulture)
                : "0",
            TipoColumna.Decimal => Numero(texto, out var d)
                ? d.ToString("0.############################", CultureInfo.InvariantCulture)
                : "0",
            TipoColumna.Fecha => DateTime.TryParseExact(texto, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) && f >= FechaMinimaExcel
                ? f.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                : null,
            _ => valor
        };
    }

    private static bool Numero(string texto, out decimal numero)
    {
        // "666,." → Power Query 666: el punto decimal sin dígitos al final se ignora
        var limpio = texto.EndsWith('.') ? texto[..^1] : texto;
        if (decimal.TryParse(limpio, EstiloNumero, CultureInfo.InvariantCulture, out numero)) return true;
        // Exponentes muy grandes (p. ej. 50E16) desbordan decimal en algunos formatos: se intenta como double
        if (double.TryParse(limpio, EstiloNumero, CultureInfo.InvariantCulture, out var d) && Math.Abs(d) < 7.9e28)
        {
            numero = (decimal)d;
            return true;
        }
        return false;
    }
}
