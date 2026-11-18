using Asignacion.Carga.Aplicacion;
using Asignacion.Carga.Consola;
using Asignacion.Carga.Infraestructura;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

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

// 2. Configuración: appsettings.json + appsettings.{Ambiente}.json, siempre junto al .exe
//    (así funciona igual si el job lo ejecuta desde otra carpeta; la carpeta logs/ también queda ahí)
Directory.SetCurrentDirectory(AppContext.BaseDirectory);
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    EnvironmentName = argumentos.Ambiente,
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

try
{
    using var host = builder.Build();
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
