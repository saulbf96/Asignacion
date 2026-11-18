using Asignacion.Carga.Aplicacion;
using Asignacion.Carga.Consola;
using Asignacion.Carga.Infraestructura;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

// Todo (appsettings, logs\) se resuelve junto al .exe, aunque se ejecute desde otra carpeta o un job
Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var codigo = await EjecutarAsync(args);

if (!args.Contains("--sin-pausa", StringComparer.OrdinalIgnoreCase) && VentanaConsola.SeAbrioConDobleClic())
{
    Console.WriteLine();
    Console.WriteLine(VentanaConsola.MensajeFinal(codigo));
    VentanaConsola.EsperarTecla();
}
return codigo;

static async Task<int> EjecutarAsync(string[] args)
{
    // 1. Parámetros
    if (!ArgumentosConsola.TryParse(args, out var argumentos, out var error))
    {
        Console.Error.WriteLine(error);
        Console.Error.WriteLine();
        Console.Error.WriteLine(ArgumentosConsola.Ayuda);
        return CodigoSalida.ParametrosInvalidos;
    }
    if (argumentos.MostrarAyuda)
    {
        Console.WriteLine(ArgumentosConsola.Ayuda);
        return CodigoSalida.Correcto;
    }

    try
    {
        // 2. Ambiente: --ambiente / ASIGNACION_AMBIENTE / "Ambiente" del appsettings.json / Local
        var ambiente = argumentos.Ambiente ?? AmbienteDelAppsettings() ?? ArgumentosConsola.AmbientePorOmision;

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = ambiente,
            ContentRootPath = AppContext.BaseDirectory,
            Args = null // los parámetros ya se leyeron arriba
        });

        builder.Services.AddSerilog((_, log) => log.ReadFrom.Configuration(builder.Configuration));
        builder.Services
            .AgregarAplicacion()
            .AgregarInfraestructura(builder.Configuration)
            .AddTransient<EjecutorConsola>();

        // 3. Ejecutar (Ctrl+C cancela de forma ordenada)
        using var cancelacion = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancelacion.Cancel(); };

        using var host = builder.Build();
        Log.Information("Ambiente: {Ambiente}", ambiente);
        return await host.Services.GetRequiredService<EjecutorConsola>().EjecutarAsync(argumentos.Solicitud, cancelacion.Token);
    }
    catch (Exception ex)
    {
        // Falla antes de poder registrar nada (p. ej. appsettings mal escrito)
        Console.Error.WriteLine($"Error al iniciar: {ex.Message}");
        return CodigoSalida.Error;
    }
    finally
    {
        await Log.CloseAndFlushAsync();
    }
}

static string? AmbienteDelAppsettings()
{
    var valor = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true)
        .Build()["Ambiente"];
    return string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
