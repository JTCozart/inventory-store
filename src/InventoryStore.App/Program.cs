using InventoryStore.App.Extensions;
using InventoryStore.App.Services;
using InventoryStore.App.Tray;
using InventoryStore.Application.Interfaces.Services;
using InventoryStore.Infrastructure.Data;
using InventoryStore.Infrastructure.Extensions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Hosting.WindowsServices;
using InventoryStore.App.Middleware;
using InventoryStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WinForms = System.Windows.Forms;

namespace InventoryStore.App;

internal record CheckOutRequest(int ItemId, string CheckedOutBy, int Quantity, string? Notes, int? ClientId = null);
internal record QuickAddRequest(string Name, int Quantity, int ItemType, string? Location, string? Sku, int? MetadataId = null);
internal record QuickCreateClientRequest(string Name);
internal record CheckInRequest(int RecordId, string? Notes);
internal record MarkLostRequest(int RecordId, string? Notes);
internal record ConsumeRequest(int ItemId, int Quantity, string? Notes);
internal record RestockRequest(int ItemId, int Quantity, string? Notes);

// Fetches product metadata from the three public APIs. Shared by the barcode-lookup
// endpoint and the metadata-explorer refresh action. Pure fetch — no DB access.
internal static class BarcodeMetadataFetcher
{
    public static async Task<List<ProductMetadata>> FetchAsync(System.Net.Http.HttpClient http, string barcode)
    {
        var results = new List<ProductMetadata>();

        // 1. Open Library (ISBN-13 prefix 978/979)
        if (barcode.StartsWith("978") || barcode.StartsWith("979"))
        {
            try
            {
                var json = await http.GetStringAsync($"https://openlibrary.org/api/books?bibkeys=ISBN:{barcode}&format=json&jscmd=data");
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty($"ISBN:{barcode}", out var book))
                {
                    var name = book.TryGetProperty("title", out var t) ? t.GetString() : null;
                    string? desc = null;
                    if (book.TryGetProperty("description", out var d))
                        desc = d.ValueKind == JsonValueKind.String ? d.GetString()
                            : d.TryGetProperty("value", out var dv) ? dv.GetString() : null;
                    string? img = null;
                    if (book.TryGetProperty("cover", out var cover))
                        img = cover.TryGetProperty("large", out var cl) ? cl.GetString()
                            : cover.TryGetProperty("medium", out var cm) ? cm.GetString() : null;
                    var publisher = book.TryGetProperty("publishers", out var pubs) && pubs.GetArrayLength() > 0
                        && pubs[0].TryGetProperty("name", out var pn) ? pn.GetString() : null;
                    var subject = book.TryGetProperty("subjects", out var subs) && subs.GetArrayLength() > 0
                        && subs[0].TryGetProperty("name", out var sn) ? sn.GetString() : null;
                    string? pages = book.TryGetProperty("number_of_pages", out var np) && np.ValueKind == JsonValueKind.Number
                        ? np.GetInt32() + " pages" : null;
                    string? weight = book.TryGetProperty("weight", out var wt) ? wt.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(name))
                        results.Add(new ProductMetadata { Barcode = barcode, Source = "openlibrary", Name = name!, Description = desc, ImageUrl = img, Brand = publisher, Category = subject, Size = pages, Weight = weight, FetchedAt = DateTime.UtcNow });
                }
            }
            catch { }
        }

        // 2. UPC Item DB (free trial, general retail)
        try
        {
            var json = await http.GetStringAsync($"https://api.upcitemdb.com/prod/trial/lookup?upc={barcode}");
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("items", out var items) && items.GetArrayLength() > 0)
            {
                var item = items[0];
                var name  = item.TryGetProperty("title",       out var t) ? t.GetString() : null;
                var desc  = item.TryGetProperty("description", out var d) ? d.GetString() : null;
                var brand = item.TryGetProperty("brand",       out var b) ? b.GetString() : null;
                var cat   = item.TryGetProperty("category",    out var c) ? c.GetString() : null;
                var size  = item.TryGetProperty("size",        out var sz) ? sz.GetString() : null;
                var weight = item.TryGetProperty("weight",     out var wt) ? wt.GetString() : null;
                string? img = null;
                if (item.TryGetProperty("images", out var imgs) && imgs.GetArrayLength() > 0)
                    img = imgs[0].GetString();
                if (!string.IsNullOrWhiteSpace(name))
                    results.Add(new ProductMetadata { Barcode = barcode, Source = "upcitemdb", Name = name!, Description = desc, ImageUrl = img, Brand = brand, Category = cat, Size = size, Weight = weight, FetchedAt = DateTime.UtcNow });
            }
        }
        catch { }

        // 3. Open Food Facts (food & beverages)
        try
        {
            var json = await http.GetStringAsync($"https://world.openfoodfacts.org/api/v2/product/{barcode}?fields=product_name,brands,categories_tags,image_url,ingredients_text,quantity");
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("status", out var s) && s.GetInt32() == 1
                && doc.RootElement.TryGetProperty("product", out var prod))
            {
                var name  = prod.TryGetProperty("product_name", out var n)  ? n.GetString() : null;
                var brand = prod.TryGetProperty("brands",        out var b)  ? b.GetString() : null;
                var img   = prod.TryGetProperty("image_url",     out var i)  ? i.GetString() : null;
                var ing   = prod.TryGetProperty("ingredients_text", out var ig) ? ig.GetString() : null;
                var qty   = prod.TryGetProperty("quantity",        out var q)  ? q.GetString() : null;
                string? cat = null;
                if (prod.TryGetProperty("categories_tags", out var cats) && cats.GetArrayLength() > 0)
                    cat = cats[0].GetString()?.Replace("en:", "", StringComparison.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(name))
                    results.Add(new ProductMetadata { Barcode = barcode, Source = "openfoodfacts", Name = name!, Description = ing, ImageUrl = img, Brand = brand, Category = cat, Size = qty, FetchedAt = DateTime.UtcNow });
            }
        }
        catch { }

        return results;
    }
}

internal class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (WindowsServiceHelpers.IsWindowsService())
        {
            RunAsService(args);
        }
        else
        {
            RunAsTrayApplication(args);
        }
    }

    static void RunAsService(string[] args)
    {
        CreateHostBuilder(args)
            .UseWindowsService()
            .Build()
            .Run();
    }

    static void RunAsTrayApplication(string[] args)
    {
        WinForms.Application.EnableVisualStyles();
        WinForms.Application.SetCompatibleTextRenderingDefault(false);

        var host = CreateHostBuilder(args).Build();
        var cts = new CancellationTokenSource();

        _ = Task.Run(() => host.RunAsync(cts.Token));
        Thread.Sleep(800);

        WinForms.Application.Run(new TrayApplicationContext(host.Services, cts));

        cts.Cancel();
        try { host.WaitForShutdown(); } catch { }
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(web =>
            {
                web.ConfigureServices(ConfigureServices);
                web.Configure(ConfigureApp);
                var isDev = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
                var httpPort = isDev ? 5051 : 5050;
                var https = LoadHttpsConfig();

                web.UseKestrel(options =>
                {
                    options.ListenAnyIP(httpPort);
                    if (https.Enabled && File.Exists(https.CertPath))
                    {
                        options.ListenAnyIP(https.Port, listen =>
                        {
                            listen.UseHttps(https.CertPath, https.CertPassword);
                        });
                    }
                });
            });

    static (bool Enabled, int Port, string CertPath, string? CertPassword) LoadHttpsConfig()
    {
        var dataDir  = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "InventoryStore");
        var certPath = Path.Combine(dataDir, "https.pfx");
        var dbPath   = Path.Combine(dataDir, "inventory.db");
        if (!File.Exists(dbPath)) return (false, 443, certPath, null);
        try
        {
            using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}");
            conn.Open();
            string? Get(string key)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Value FROM AppSettings WHERE Key = @k";
                cmd.Parameters.AddWithValue("@k", key);
                return cmd.ExecuteScalar() as string;
            }
            var enabled  = Get("https.enabled") == "true";
            var port     = int.TryParse(Get("https.port"), out var p) ? p : 443;
            var password = Get("https.cert.password");
            return (enabled, port, certPath, password);
        }
        catch { return (false, 443, certPath, null); }
    }

    static void ConfigureServices(IServiceCollection services)
    {
        var isDev = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        var dbFileName = isDev ? "inventory-dev.db" : "inventory.db";
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "InventoryStore",
            dbFileName);

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        services.AddInfrastructure($"Data Source={dbPath}");
        services.AddRazorPages();
        services.AddHttpContextAccessor();
        services.AddHttpClient("ntfy");
        services.AddHttpClient("barcode", c =>
        {
            c.DefaultRequestHeaders.UserAgent.ParseAdd("InventoryStore/1.0 (barcode-lookup)");
            c.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddScoped<InventoryStore.Application.Interfaces.Services.INtfyService,
                           InventoryStore.App.Services.NtfyService>();

        services.AddSingleton<TunnelService>();
        services.AddSingleton<UpdateInfo>();
        services.AddSingleton<UpdateCheckService>();
        services.AddHostedService(sp => sp.GetRequiredService<UpdateCheckService>());

        services.AddAntiforgery(options =>
        {
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.None;
        });

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Auth/Login";
                options.LogoutPath = "/Auth/Logout";
                options.AccessDeniedPath = "/Auth/Login";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.None;
                options.Events = new Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationEvents
                {
                    OnRedirectToLogin = ctx =>
                    {
                        ctx.Response.Redirect("/Auth/Login");
                        return Task.CompletedTask;
                    },
                    OnRedirectToAccessDenied = ctx =>
                    {
                        ctx.Response.Redirect("/Auth/Login");
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();
    }

    static void ConfigureApp(IApplicationBuilder app)
    {
        var env = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();

        if (env.IsDevelopment())
            app.UseDeveloperExceptionPage();
        else
            app.UseExceptionHandler("/Error");

        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<FirstRunMiddleware>();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapRazorPages();

            // ── Local tray-companion API (localhost only, no auth) ────────
            endpoints.MapGet("/api/local/status", (HttpContext ctx, TunnelService tunnel) =>
            {
                if (!LocalApiGuard.IsLoopback(ctx)) return Results.Forbid();
                return Results.Ok(new
                {
                    networkUrl  = $"http://{InventoryStore.App.Utilities.NetworkUtility.GetLocalIpAddress()}:5050",
                    tunnelState = tunnel.State.ToString(),
                    tunnelUrl   = tunnel.PublicUrl,
                });
            });

            endpoints.MapPost("/api/local/reset-admin", async (HttpContext ctx, IUserAuthService auth) =>
            {
                if (!LocalApiGuard.IsLoopback(ctx)) return Results.Forbid();
                var body = await ctx.Request.ReadFromJsonAsync<ResetAdminRequest>();
                if (body is null || string.IsNullOrWhiteSpace(body.NewPassword))
                    return Results.BadRequest("newPassword is required.");
                if (body.NewPassword.Length < 8)
                    return Results.BadRequest("Password must be at least 8 characters.");
                var username = await auth.ResetAdminPasswordAsync(body.NewPassword);
                return Results.Ok(new { username });
            });

            // ── Update check API ─────────────────────────────────────────
            endpoints.MapPost("/api/updates/check", [Authorize] async (UpdateCheckService svc, UpdateInfo info) =>
            {
                await svc.CheckNowAsync();
                return Results.Ok(new
                {
                    hasUpdate     = info.HasUpdate,
                    latestVersion = info.LatestVersion,
                    releaseUrl    = info.ReleaseUrl,
                    currentVersion= info.CurrentVersion,
                });
            });

            // ── Tunnel API ───────────────────────────────────────────────
            endpoints.MapGet("/api/tunnel/status", [Authorize] (TunnelService tunnel) =>
                Results.Ok(new
                {
                    state   = tunnel.State.ToString(),
                    url     = tunnel.PublicUrl,
                    error   = tunnel.Error
                }));

            endpoints.MapPost("/api/tunnel/start-quick", [Authorize] (TunnelService tunnel) =>
            {
                _ = Task.Run(() => tunnel.StartQuickAsync());
                return Results.Ok();
            });

            endpoints.MapPost("/api/tunnel/start-named", [Authorize] async (HttpContext ctx, TunnelService tunnel) =>
            {
                var body = await ctx.Request.ReadFromJsonAsync<StartNamedTunnelRequest>();
                if (body is null || string.IsNullOrWhiteSpace(body.Token))
                    return Results.BadRequest("Token is required.");
                _ = Task.Run(() => tunnel.StartNamedAsync(body.Token, body.PublicUrl));
                return Results.Ok();
            });

            endpoints.MapPost("/api/tunnel/start-localtunnel", [Authorize] async (HttpContext ctx, TunnelService tunnel) =>
            {
                var body = await ctx.Request.ReadFromJsonAsync<StartLocalTunnelRequest>();
                if (body is null || string.IsNullOrWhiteSpace(body.Subdomain))
                    return Results.BadRequest("Subdomain is required.");
                _ = Task.Run(() => tunnel.StartLocalTunnelAsync(body.Subdomain.Trim().ToLower()));
                return Results.Ok();
            });

            endpoints.MapPost("/api/tunnel/start-serveo", [Authorize] async (HttpContext ctx, TunnelService tunnel) =>
            {
                var body = await ctx.Request.ReadFromJsonAsync<StartServeoRequest>();
                if (body is null || string.IsNullOrWhiteSpace(body.Subdomain))
                    return Results.BadRequest("Subdomain is required.");
                _ = Task.Run(() => tunnel.StartServeoAsync(body.Subdomain.Trim().ToLower()));
                return Results.Ok();
            });

            endpoints.MapPost("/api/tunnel/generate-serveo-key", [Authorize] async (TunnelService tunnel) =>
            {
                var publicKey = await tunnel.EnsureServeoKeyAsync();
                return Results.Ok(new { publicKey });
            });

            endpoints.MapPost("/api/tunnel/regenerate-serveo-key", [Authorize] async (TunnelService tunnel) =>
            {
                var publicKey = await tunnel.RegenerateServeoKeyAsync();
                return Results.Ok(new { publicKey });
            });

            endpoints.MapPost("/api/tunnel/stop", [Authorize] async (TunnelService tunnel) =>
            {
                await tunnel.StopAsync();
                return Results.Ok();
            });

            // ── Barcode Product Lookup ────────────────────────────────────
            endpoints.MapGet("/api/barcode-lookup", [Authorize] async (string barcode, IServiceScopeFactory scopeFactory, IHttpClientFactory httpFactory) =>
            {
                barcode = barcode.Trim();
                if (string.IsNullOrWhiteSpace(barcode)) return Results.BadRequest("barcode required");

                // Return cached results if available
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var cached = await db.ProductMetadata
                    .Where(p => p.Barcode == barcode)
                    .OrderBy(p => p.Source)
                    .ToListAsync();

                if (cached.Count > 0)
                    return Results.Ok(cached.Select(p => new { p.Id, p.Source, p.Name, p.Description, p.ImageUrl, p.Brand, p.Category, p.Size, p.Weight }));

                var http = httpFactory.CreateClient("barcode");
                var results = await BarcodeMetadataFetcher.FetchAsync(http, barcode);

                if (results.Count > 0)
                {
                    db.ProductMetadata.AddRange(results);
                    await db.SaveChangesAsync();
                }

                return Results.Ok(results.Select(p => new { p.Id, p.Source, p.Name, p.Description, p.ImageUrl, p.Brand, p.Category, p.Size, p.Weight }));
            });

            // ── Product metadata explorer ─────────────────────────────────
            endpoints.MapGet("/api/metadata", [Authorize] async (IServiceScopeFactory scopeFactory) =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var rows = await db.ProductMetadata
                    .OrderBy(p => p.Barcode).ThenBy(p => p.Source)
                    .ToListAsync();

                var counts = await db.InventoryItems
                    .Where(i => i.SelectedMetadataId != null)
                    .GroupBy(i => i.SelectedMetadataId)
                    .Select(g => new { Id = g.Key, Count = g.Count() })
                    .ToListAsync();
                var countMap = counts.Where(x => x.Id.HasValue).ToDictionary(x => x.Id!.Value, x => x.Count);

                return Results.Ok(rows.Select(p => new
                {
                    p.Id, p.Barcode, p.Source, p.Name, p.Description, p.ImageUrl,
                    p.Brand, p.Category, p.Size, p.Weight, p.FetchedAt,
                    LinkedCount = countMap.TryGetValue(p.Id, out var c) ? c : 0
                }));
            });

            endpoints.MapPost("/api/metadata/{id:int}/refresh", [Authorize(Roles = "Admin,Manager")] async (
                int id, IServiceScopeFactory scopeFactory, IHttpClientFactory httpFactory) =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var row = await db.ProductMetadata.FindAsync(id);
                if (row is null) return Results.NotFound(new { error = "Metadata not found." });

                var http = httpFactory.CreateClient("barcode");
                var fresh = await BarcodeMetadataFetcher.FetchAsync(http, row.Barcode);
                var match = fresh.FirstOrDefault(f => string.Equals(f.Source, row.Source, StringComparison.OrdinalIgnoreCase));
                if (match is null)
                    return Results.Ok(new { success = false, error = "No data returned from the source." });

                row.Name = match.Name;
                row.Description = match.Description;
                row.ImageUrl = match.ImageUrl;
                row.Brand = match.Brand;
                row.Category = match.Category;
                row.Size = match.Size;
                row.Weight = match.Weight;
                row.FetchedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();

                var linked = await db.InventoryItems.CountAsync(i => i.SelectedMetadataId == id);
                return Results.Ok(new
                {
                    success = true,
                    item = new
                    {
                        row.Id, row.Barcode, row.Source, row.Name, row.Description, row.ImageUrl,
                        row.Brand, row.Category, row.Size, row.Weight, row.FetchedAt,
                        LinkedCount = linked
                    }
                });
            });

            endpoints.MapPost("/api/metadata/{id:int}/delete", [Authorize(Roles = "Admin,Manager")] async (
                int id, IServiceScopeFactory scopeFactory) =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var row = await db.ProductMetadata.FindAsync(id);
                if (row is null) return Results.NotFound(new { error = "Metadata not found." });

                // Unlink any items pointing at this metadata before removing it.
                var linked = await db.InventoryItems.Where(i => i.SelectedMetadataId == id).ToListAsync();
                foreach (var it in linked)
                {
                    it.SelectedMetadataId = null;
                    it.IsMetadataMatched = false;
                }

                db.ProductMetadata.Remove(row);
                await db.SaveChangesAsync();

                return Results.Ok(new { success = true, unlinked = linked.Count });
            });

            // ── Inventory API ─────────────────────────────────────────────
            endpoints.MapGet("/api/inventory/status", [Authorize] async (string sku, ICheckoutService svc) =>
            {
                var status = await svc.GetItemStatusBySkuAsync(sku);
                return status is not null ? Results.Ok(status) : Results.Ok((object?)null);
            });

            endpoints.MapGet("/api/inventory/status/{id:int}", [Authorize] async (int id, ICheckoutService svc) =>
            {
                try { var status = await svc.GetItemStatusAsync(id); return Results.Ok(status); }
                catch { return Results.Ok((object?)null); }
            });

            endpoints.MapGet("/api/inventory/search", [Authorize] async (string? q, IInventoryService svc) =>
            {
                if (string.IsNullOrWhiteSpace(q)) return Results.Ok(Array.Empty<object>());
                var results = await svc.SearchItemsAsync(q);
                return Results.Ok(results.Select(i => new { i.Id, i.Name, i.Location, i.SKU, i.ItemType, i.AvailableQuantity, i.CategoryName, i.CategoryColor }));
            });

            endpoints.MapGet("/api/inventory/available", [Authorize] async (IInventoryService svc) =>
            {
                var items = await svc.GetAllItemsAsync();
                var reusable = items
                    .Where(i => i.ItemType == InventoryStore.Domain.Enums.ItemType.Reusable)
                    .OrderBy(i => i.Name);
                return Results.Ok(reusable.Select(i => new { i.Id, i.Name, i.Location, i.SKU, i.AvailableQuantity, i.CategoryName, i.CategoryColor, i.IsLowStock }));
            });

            endpoints.MapPost("/api/inventory/quick-add", [Authorize(Roles = "Admin,Manager")] async (HttpContext ctx, IInventoryService inv, ICheckoutService checkout) =>
            {
                var dto = await ctx.Request.ReadFromJsonAsync<QuickAddRequest>();
                if (dto is null || string.IsNullOrWhiteSpace(dto.Name)) return Results.BadRequest(new { error = "Name is required." });
                var (uid, uname) = HttpContextExtensions.GetUser(ctx);
                var itemType = dto.ItemType == 0 ? InventoryStore.Domain.Enums.ItemType.Consumable : InventoryStore.Domain.Enums.ItemType.Reusable;
                var created = await inv.CreateItemAsync(
                    new Application.DTOs.CreateInventoryItemDto(dto.Name, dto.Quantity, null, dto.Location, dto.Sku, 0, itemType, null, null, null, dto.MetadataId),
                    uid, uname);
                var status = await checkout.GetItemStatusAsync(created.Id);
                return Results.Ok(status);
            });

            endpoints.MapPost("/api/inventory/checkout", [Authorize(Roles = "Admin,Manager")] async (HttpContext ctx, ICheckoutService svc) =>
            {
                var dto = await ctx.Request.ReadFromJsonAsync<CheckOutRequest>();
                if (dto is null) return Results.BadRequest();
                var (uid, uname) = HttpContextExtensions.GetUser(ctx);
                var record = await svc.CheckOutAsync(new Application.DTOs.CheckOutItemDto(dto.ItemId, dto.CheckedOutBy, dto.Quantity, dto.Notes, dto.ClientId), uid, uname);
                return Results.Ok(record);
            });

            endpoints.MapPost("/api/inventory/checkin", [Authorize(Roles = "Admin,Manager")] async (HttpContext ctx, ICheckoutService svc) =>
            {
                var dto = await ctx.Request.ReadFromJsonAsync<CheckInRequest>();
                if (dto is null) return Results.BadRequest();
                var (uid, uname) = HttpContextExtensions.GetUser(ctx);
                var record = await svc.CheckInAsync(new Application.DTOs.CheckInItemDto(dto.RecordId, dto.Notes), uid, uname);
                return Results.Ok(record);
            });

            endpoints.MapPost("/api/inventory/lost", [Authorize(Roles = "Admin,Manager")] async (HttpContext ctx, ICheckoutService svc) =>
            {
                var dto = await ctx.Request.ReadFromJsonAsync<MarkLostRequest>();
                if (dto is null) return Results.BadRequest();
                var (uid, uname) = HttpContextExtensions.GetUser(ctx);
                var record = await svc.MarkLostAsync(new Application.DTOs.MarkLostDto(dto.RecordId, dto.Notes), uid, uname);
                return Results.Ok(record);
            });

            endpoints.MapPost("/api/inventory/consume", [Authorize(Roles = "Admin,Manager")] async (HttpContext ctx, ICheckoutService svc) =>
            {
                var dto = await ctx.Request.ReadFromJsonAsync<ConsumeRequest>();
                if (dto is null) return Results.BadRequest();
                var (uid, uname) = HttpContextExtensions.GetUser(ctx);
                await svc.ConsumeAsync(new Application.DTOs.ConsumeItemDto(dto.ItemId, dto.Quantity, dto.Notes), uid, uname);
                return Results.Ok();
            });

            endpoints.MapPost("/api/inventory/restock", [Authorize(Roles = "Admin,Manager")] async (HttpContext ctx, ICheckoutService svc) =>
            {
                var dto = await ctx.Request.ReadFromJsonAsync<RestockRequest>();
                if (dto is null) return Results.BadRequest();
                var (uid, uname) = HttpContextExtensions.GetUser(ctx);
                await svc.RestockAsync(new Application.DTOs.RestockItemDto(dto.ItemId, dto.Quantity, dto.Notes), uid, uname);
                return Results.Ok();
            });

            // ── Clients API ───────────────────────────────────────────────
            endpoints.MapGet("/api/clients/search", [Authorize] async (string? q, IClientService svc) =>
            {
                if (string.IsNullOrWhiteSpace(q)) return Results.Ok(Array.Empty<object>());
                var clients = await svc.SearchAsync(q);
                return Results.Ok(clients.Select(c => new { c.Id, c.DisplayName, c.FirstName, c.LastName, c.Phone }));
            });

            endpoints.MapGet("/api/clients/{id:int}", [Authorize] async (int id, IClientService svc) =>
            {
                var client = await svc.GetByIdAsync(id);
                return client is not null ? Results.Ok(client) : Results.NotFound();
            });

            endpoints.MapPost("/api/clients", [Authorize(Roles = "Admin,Manager")] async (HttpContext ctx, IClientService svc) =>
            {
                var dto = await ctx.Request.ReadFromJsonAsync<Application.DTOs.CreateClientDto>();
                if (dto is null || string.IsNullOrWhiteSpace(dto.FirstName))
                    return Results.BadRequest(new { error = "FirstName is required." });
                var client = await svc.CreateAsync(dto);
                return Results.Ok(client);
            });

            endpoints.MapPut("/api/clients/{id:int}", [Authorize(Roles = "Admin,Manager")] async (int id, HttpContext ctx, IClientService svc) =>
            {
                var dto = await ctx.Request.ReadFromJsonAsync<Application.DTOs.UpdateClientDto>();
                if (dto is null || string.IsNullOrWhiteSpace(dto.FirstName))
                    return Results.BadRequest(new { error = "FirstName is required." });
                try { await svc.UpdateAsync(id, dto); return Results.Ok(); }
                catch (KeyNotFoundException) { return Results.NotFound(); }
            });

            endpoints.MapDelete("/api/clients/{id:int}", [Authorize(Roles = "Admin,Manager")] async (int id, IClientService svc) =>
            {
                await svc.DeleteAsync(id);
                return Results.Ok();
            });

            endpoints.MapPost("/api/clients/quick-create", [Authorize(Roles = "Admin,Manager")] async (HttpContext ctx, IClientService svc) =>
            {
                var body = await ctx.Request.ReadFromJsonAsync<QuickCreateClientRequest>();
                if (body is null || string.IsNullOrWhiteSpace(body.Name))
                    return Results.BadRequest(new { error = "Name is required." });
                var client = await svc.QuickCreateAsync(body.Name);
                return Results.Ok(client);
            });

            endpoints.MapGet("/api/clients/{id:int}/history", [Authorize] async (int id, ICheckoutService svc) =>
            {
                var history = await svc.GetClientHistoryAsync(id);
                return Results.Ok(history);
            });

            endpoints.MapPost("/api/ntfy/test", [Authorize(Roles = "Admin")] async (INtfyService ntfy) =>
            {
                var (ok, code) = await ntfy.SendTestAsync();
                return Results.Ok(new { ok, code });
            });

            endpoints.MapPost("/api/admin/restart", [Authorize(Roles = "Admin")] (IHostApplicationLifetime lifetime) =>
            {
                _ = Task.Run(async () => { await Task.Delay(800); lifetime.StopApplication(); });
                return Results.Ok();
            });
        });

        using var scope = app.ApplicationServices.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        initializer.InitializeAsync().GetAwaiter().GetResult();

        // Auto-start tunnel if previously enabled
        var lifetime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStarted.Register(() =>
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var s = app.ApplicationServices.CreateScope();
                    var settings = s.ServiceProvider.GetRequiredService<ISettingsService>();
                    var mode     = await settings.GetAsync("tunnel.autostart");
                    var tunnel = app.ApplicationServices.GetRequiredService<TunnelService>();
                    if (mode == "quick")
                    {
                        await tunnel.StartQuickAsync();
                    }
                    else if (mode == "named")
                    {
                        var token = await settings.GetAsync("tunnel.token");
                        var url   = await settings.GetAsync("tunnel.url");
                        if (!string.IsNullOrWhiteSpace(token))
                            await tunnel.StartNamedAsync(token, url);
                    }
                    else if (mode == "localtunnel")
                    {
                        var sub = await settings.GetAsync("tunnel.lt.subdomain");
                        if (!string.IsNullOrWhiteSpace(sub))
                            await tunnel.StartLocalTunnelAsync(sub);
                    }
                    else if (mode == "serveo")
                    {
                        var sub = await settings.GetAsync("tunnel.serveo.subdomain");
                        if (!string.IsNullOrWhiteSpace(sub))
                            await tunnel.StartServeoAsync(sub);
                    }
                }
                catch { }
            });
        });
    }
}

internal record StartNamedTunnelRequest(string Token, string? PublicUrl);
internal record StartLocalTunnelRequest(string Subdomain);
internal record StartServeoRequest(string Subdomain);
internal record ResetAdminRequest(string NewPassword);

internal static class LocalApiGuard
{
    internal static bool IsLoopback(HttpContext ctx)
    {
        var ip = ctx.Connection.RemoteIpAddress;
        return ip is not null && (ip.Equals(System.Net.IPAddress.Loopback) || ip.Equals(System.Net.IPAddress.IPv6Loopback));
    }
}

internal static class HttpContextExtensions
{
    internal static (int userId, string username) GetUser(HttpContext ctx)
        => ctx.User.GetIdentity();
}
