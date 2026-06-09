if (args.Length > 0 && string.Equals(args[0], "export-static", StringComparison.OrdinalIgnoreCase))
{
    var contentRoot = BookBoutique.Services.StaticSiteExporter.ResolveContentRoot(Directory.GetCurrentDirectory());
    var outputRoot = args.Length > 1
        ? Path.GetFullPath(args[1])
        : Path.Combine(contentRoot, "dist");

    var port = GetAvailablePort();
    var exportBuilder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = contentRoot,
        WebRootPath = Path.Combine(contentRoot, "wwwroot"),
        EnvironmentName = Environments.Development
    });

    ConfigureServices(exportBuilder.Services, exportBuilder.Configuration);

    var app = exportBuilder.Build();
    ConfigurePipeline(app);
    app.Urls.Add($"http://127.0.0.1:{port}");
    await app.StartAsync();

    var exporter = new BookBoutique.Services.StaticSiteExporter(contentRoot, outputRoot);
    try
    {
        await exporter.ExportAsync(new Uri($"http://127.0.0.1:{port}"));
        Console.WriteLine($"Static site exported to: {exporter.OutputRoot}");
    }
    finally
    {
        await app.StopAsync();
        await app.DisposeAsync();
    }

    return;
}

var builder = WebApplication.CreateBuilder(args);

ConfigureServices(builder.Services, builder.Configuration);

var webApp = builder.Build();

ConfigurePipeline(webApp);

webApp.Run();

static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    services.AddRazorPages();
    services.AddSingleton<BookBoutique.Services.PreorderStore>();
    services.AddSingleton<BookBoutique.Services.PdfPageImageRenderer>();
    services.AddSingleton<BookBoutique.Services.BonusContentStore>();
    services.AddSingleton<BookBoutique.Services.FirstChaptersStore>();
    services.AddSingleton<BookBoutique.Services.PodcastStore>();
    services.AddSingleton<BookBoutique.Services.GalleryStore>();
}

static void ConfigurePipeline(WebApplication app)
{
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
        app.UseHttpsRedirection();
    }

    app.UseStaticFiles();

    app.UseRouting();

    app.UseAuthorization();

    app.MapRazorPages();
}

static int GetAvailablePort()
{
    var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
    listener.Start();
    var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return port;
}
