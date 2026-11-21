using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddControllers();

var app = builder.Build();

app.UseForwardedHeaders();

app.Use(async (context, next) =>
{
    if ((HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)) &&
        string.Equals(context.Request.Path.Value, "/portal", StringComparison.OrdinalIgnoreCase))
    {
        var target = "/portal/" + context.Request.QueryString;
        context.Response.Redirect(target, permanent: false);
        return;
    }

    await next();
});

var distDirectory = Directory.Exists(Path.Combine(builder.Environment.ContentRootPath, "dist"))
    ? Path.Combine(builder.Environment.ContentRootPath, "dist")
    : Path.Combine(builder.Environment.ContentRootPath, "src", "dist");

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

var portalStaticFileOptions = new StaticFileOptions
{
    FileProvider = fileProviderOptions,
    RequestPath = "/portal"
};

var portalDefaultFilesOptions = new DefaultFilesOptions
{
    FileProvider = fileProviderOptions,
    RequestPath = "/portal"
};

app.UseDefaultFiles(defaultFilesOptions);
app.UseDefaultFiles(portalDefaultFilesOptions);

app.UseStaticFiles(staticFileOptions);
app.UseStaticFiles(portalStaticFileOptions);

app.UseRouting();

app.MapControllers();

app.MapGet("/", (HttpContext context) => Results.Redirect("/portal/" + context.Request.QueryString));

app.MapFallbackToFile("index.html", staticFileOptions);
app.MapFallbackToFile("/portal/{**slug}", "index.html", staticFileOptions);

app.Run();
