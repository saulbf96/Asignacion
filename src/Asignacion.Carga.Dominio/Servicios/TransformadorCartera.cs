using Asignacion.Carga.Dominio.Entidades;
using Asignacion.Carga.Dominio.Excepciones;

namespace Asignacion.Carga.Dominio.Servicios;

/// <summary>
/// Aplica las <see cref="ReglasUnificacion"/> a cada fila de la descarga, en el mismo orden que el Power Query de WFM:
/// reemplazar valores → convertir tipos → descartar filas (filtros y encabezados repetidos) → agregar columnas → renombrar por grupo.
/// Las columnas que mencionan las reglas deben existir en la descarga: si el banco cambia el formato, se detiene
/// con un mensaje claro en lugar de armar una cartera incompleta.
/// </summary>
public sealed class TransformadorCartera
{
    private const int Vacio = -1;
    private const int Fijo = -2;

    private readonly string[] _entrada;
    private readonly (int Indice, string? Valor)[] _plan;                       // por columna de salida
    private readonly Dictionary<GrupoArchivos, (int Indice, string? Valor)[]> _planPorGrupo = [];
    private readonly (int Indice, string Valor, string? Por)[] _reemplazos;
    private readonly (int Indice, TipoColumna Tipo)[] _tipos;
    private readonly (int Indice, string[] EmpiezaCon)[] _filtros;

    public TransformadorCartera(IReadOnlyList<string> encabezados, ReglasUnificacion reglas)
    {
        _entrada = encabezados.Select(e => e.Trim()).ToArray();

        var excluidas = new HashSet<string>(reglas.ColumnasExcluidas.Select(c => c.Trim()), StringComparer.Ordinal);
        var nombres = _entrada.Where(e => !excluidas.Contains(e)).ToList();
        var plan = nombres.Select(n => (Indice: Entrada(n, "es una columna de salida"), Valor: (string?)null)).ToList();

        // Columnas nuevas, cada una después de su columna (varias después de la misma quedan en el orden configurado)
        var ultimaDespuesDe = new Dictionary<string, string>(StringComparer.Ordinal);
        var resaltadas = new List<string>();
        foreach (var nueva in reglas.ColumnasNuevas)
        {
            var nombre = nueva.Nombre.Trim();
            if (_entrada.Contains(nombre, StringComparer.Ordinal))
            {
                if (nueva.EnBlanco) continue; // la descarga ya la trae: se respeta la de la descarga
                throw new DominioException($"La descarga ya trae la columna '{nombre}' y la regla la quiere agregar. Revise las reglas de unificación.");
            }
            if (nombres.Contains(nombre, StringComparer.Ordinal))
                throw new DominioException($"La columna nueva '{nombre}' está dos veces en las reglas de unificación.");

            var despuesDe = nueva.DespuesDe.Trim();
            var ancla = ultimaDespuesDe.GetValueOrDefault(despuesDe, despuesDe);
            var posicion = nombres.IndexOf(ancla);
            if (posicion < 0)
                throw new DominioException($"No se puede agregar '{nombre}': la descarga no trae la columna '{despuesDe}'.");

            nombres.Insert(posicion + 1, nombre);
            (int Indice, string? Valor) origen =
                nueva.Valor is not null ? (Fijo, (string?)nueva.Valor)
                : nueva.CopiaDe is not null ? (Entrada(nueva.CopiaDe, $"se copia en '{nombre}'"), (string?)null)
                : (Vacio, (string?)null);
            plan.Insert(posicion + 1, origen);
            ultimaDespuesDe[despuesDe] = nombre;
            if (nueva.Resaltar) resaltadas.Add(nombre);
        }

        // Renombrar por grupo: la columna destino va al final (en el orden en que vienen sus columnas origen en la descarga,
        // como al combinar en Power Query: PREVENTA→TIPO_CARTERA antes que GESTORES→TIPO_GESTION); en los demás grupos queda en blanco
        var destinos = reglas.Grupos
            .SelectMany(g => g.Renombrar.Select(r => (Destino: r.Value.Trim(), Origen: Entrada(r.Key, $"se renombra en el grupo {g.Nombre}"))))
            .GroupBy(r => r.Destino, StringComparer.Ordinal)
            .OrderBy(g => g.Min(r => r.Origen))
            .Select(g => g.Key);
        foreach (var destino in destinos)
        {
            if (nombres.Contains(destino, StringComparer.Ordinal))
                throw new DominioException($"No se puede renombrar a '{destino}': esa columna ya existe en el unificado.");
            nombres.Add(destino);
            plan.Add((Vacio, null));
        }
        _plan = [.. plan];
        foreach (var grupo in reglas.Grupos.Where(g => g.Renombrar.Count > 0))
        {
            var planGrupo = (ValueTuple<int, string?>[])_plan.Clone();
            foreach (var (origen, destino) in grupo.Renombrar)
            {
                var indiceOrigen = Entrada(origen, $"se renombra en el grupo {grupo.Nombre}");
                var enSalida = nombres.IndexOf(origen.Trim());
                if (enSalida >= 0) planGrupo[enSalida] = (Vacio, null);
                planGrupo[nombres.IndexOf(destino.Trim())] = (indiceOrigen, null);
            }
            _planPorGrupo[grupo] = planGrupo;
        }

        _reemplazos = reglas.Reemplazos
            .SelectMany(r => r.Columnas.Select(c => (Entrada(c, "tiene un reemplazo de valor"), r.Valor, r.Por)))
            .ToArray();
        _tipos = reglas.Tipos
            .SelectMany(t => t.Columnas.Select(c => (Entrada(c, $"se convierte a {t.Tipo}"), t.Tipo)))
            .ToArray();
        _filtros = reglas.FilasExcluidas
            .Select(f => (Entrada(f.Columna, "se usa para descartar filas"), f.EmpiezaCon.Where(t => t.Length > 0).ToArray()))
            .ToArray();

        // Tipo de cada columna de salida (para que el Excel escriba texto como texto y números como número)
        var tipoEntrada = _tipos.GroupBy(t => t.Indice).ToDictionary(g => g.Key, g => g.Last().Tipo);
        Tipos = Enumerable.Range(0, nombres.Count).Select(j =>
        {
            var indice = _plan[j].Indice;
            if (indice == Vacio) // columna renombrada: toma el tipo de su origen en el grupo que la llena
                indice = _planPorGrupo.Values.Select(p => p[j].Indice).FirstOrDefault(i => i >= 0, Vacio);
            return indice switch
            {
                Fijo => TipoColumna.Texto,
                >= 0 when tipoEntrada.TryGetValue(indice, out var t) => t,
                _ => (TipoColumna?)null
            };
        }).ToArray();

        Encabezados = nombres;
        Resaltadas = resaltadas.Select(r => nombres.IndexOf(r)).ToHashSet();
        IndicesDuplicado = reglas.ColumnasDuplicado.Select(c =>
        {
            var i = nombres.IndexOf(c.Trim());
            return i >= 0 ? i : throw new DominioException($"La columna '{c}' para detectar duplicados no está en el unificado.");
        }).ToArray();
    }

    /// <summary>Encabezados del unificado, en orden.</summary>
    public IReadOnlyList<string> Encabezados { get; }

    /// <summary>Tipo de cada columna del unificado (null = sin tipo: el Excel decide por el valor).</summary>
    public IReadOnlyList<TipoColumna?> Tipos { get; }

    /// <summary>Posiciones (base 0) de los encabezados que se pintan (columnas que el usuario llena a mano).</summary>
    public IReadOnlySet<int> Resaltadas { get; }

    /// <summary>Posiciones (base 0, en el unificado) de las columnas que identifican una fila repetida; vacío = todas.</summary>
    public IReadOnlyList<int> IndicesDuplicado { get; }

    /// <summary>Filas descartadas por los filtros (p. ej. CAMPANIA QUEJA/CLON).</summary>
    public int FilasExcluidas { get; private set; }

    /// <summary>Filas que repetían el encabezado a media hoja (se descartan).</summary>
    public int EncabezadosRepetidos { get; private set; }

    /// <summary>La fila ya transformada, o null si se descarta.</summary>
    public string?[]? Transformar(string?[] campos, GrupoArchivos? grupo)
    {
        if (EsEncabezado(campos)) { EncabezadosRepetidos++; return null; }

        var valores = (string?[])campos.Clone();
        foreach (var (indice, valor, por) in _reemplazos)
            if (string.Equals(valores[indice], valor, StringComparison.Ordinal)) valores[indice] = por;
        foreach (var (indice, tipo) in _tipos)
            valores[indice] = ConvertidorTipos.Convertir(valores[indice], tipo);

        foreach (var (indice, empiezaCon) in _filtros)
            if (valores[indice] is { } v && empiezaCon.Any(t => v.StartsWith(t, StringComparison.Ordinal)))
            {
                FilasExcluidas++;
                return null;
            }

        var plan = grupo is not null && _planPorGrupo.TryGetValue(grupo, out var p) ? p : _plan;
        var salida = new string?[plan.Length];
        for (var i = 0; i < plan.Length; i++)
            salida[i] = plan[i].Indice switch
            {
                Vacio => null,
                Fijo => plan[i].Valor,
                var indice => valores[indice]
            };
        return salida;
    }

    private bool EsEncabezado(string?[] campos)
    {
        if (campos.Length != _entrada.Length) return false;
        for (var i = 0; i < campos.Length; i++)
            if (!string.Equals(campos[i]?.Trim(), _entrada[i], StringComparison.Ordinal)) return false;
        return true;
    }

    private int Entrada(string columna, string para)
    {
        var i = Array.IndexOf(_entrada, columna.Trim());
        return i >= 0 ? i : throw new DominioException($"La descarga no trae la columna '{columna.Trim()}' ({para}). Revise si cambió el formato del archivo.");
    }
}
