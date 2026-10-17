using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var app = builder.Build();

var distDirectory = Path.Combine(builder.Environment.ContentRootPath, "src", "dist");

if (!Directory.Exists(distDirectory))
{
    Directory.CreateDirectory(distDirectory);
}

var fileProviderOptions = new PhysicalFileProvider(distDirectory);

var defaultFilesOptions = new DefaultFilesOptions
{
    FileProvider = fileProviderOptions
};

var staticFileOptions = new StaticFileOptions
{
    FileProvider = fileProviderOptions,
    RequestPath = string.Empty
};

app.UseDefaultFiles(defaultFilesOptions);
app.UseStaticFiles(staticFileOptions);

app.UseRouting();

app.MapControllers();

app.MapFallbackToFile("index.html", staticFileOptions);

app.Run();