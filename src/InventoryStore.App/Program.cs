using InventoryStore.App.Extensions;
using InventoryStore.App.Services;
using InventoryStore.App.Modules;
using InventoryStore.Application.Interfaces.Services;
using InventoryStore.Domain.Interfaces.Repositories;
using InventoryStore.Infrastructure.Data;
using InventoryStore.Infrastructure.Extensions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using InventoryStore.App.Middleware;
using InventoryStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Net.Http.Json;
using LettuceEncrypt;
#if !LINUX
using InventoryStore.App.Tray;
using Microsoft.Extensions.Hosting.WindowsServices;
using WinForms = System.Windows.Forms;
#endif

namespace InventoryStore.App;

internal record CheckOutRequest(int ItemId, string CheckedOutBy, int Quantity, string? Notes, int? ClientId = null);
internal record QuickAddRequest(string Name, int Quantity, int ItemType, string? Location, string? Sku, int? MetadataId = null);
internal record QuickCreateClientRequest(string Name);
internal record QuickCreateVendorRequest(string Name);
internal record MaintenanceOutRequest(int ItemId, int Quantity = 1, int? VendorId = null, string? Notes = null);
internal record MaintenanceReturnRequest(int ItemId, string? Notes = null);
internal record MaintenanceScheduleRequest(int ItemId, string? LastMaintainedDate, int IntervalValue, int IntervalUnit, string? Notes);
internal record CheckInRequest(int RecordId, string? Notes);
internal record MarkLostRequest(int RecordId, string? Notes);
internal record ConsumeRequest(int ItemId, int Quantity, string? Notes);
internal record RestockRequest(int ItemId, int Quantity, string? Notes);
internal record KitActionRequest(int KitId, int Quantity = 1, string? CheckedOutBy = null, int? ClientId = null, string? Notes = null, bool AllowPartialFallback = false);
internal record KitCheckoutRefRequest(int KitCheckoutId);
internal record KitConsumableUsageInput(int ConsumableItemId, int UsedQuantity);
internal record KitCheckinRequest(int KitCheckoutId, IReadOnlyList<KitConsumableUsageInput>? Usage = null);
internal record KitReconcileRequest(int KitCheckoutId, IReadOnlyList<KitConsumableUsageInput> Usage);
internal record CostInput(decimal UnitCost, string? PurchaseDate, int? UsefulLifeMonths);
internal record WebhookInput(string Url, string? Events, string? Secret);
internal record UsageReportRequest(string? HowHeard, bool OptOut);

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

// Fetches chemical safety data from PubChem (free, keyless). Resolves a chemical name to a
// PubChem CID, then pulls GHS classification (signal word, pictograms, hazard / precautionary
// statements) and CAS number. Pure fetch — no DB access. Used by the optional SDS module.
internal static class PubChemSdsFetcher
{
    private const string Base = "https://pubchem.ncbi.nlm.nih.gov/rest";

    public static async Task<SafetyDataSheet?> FetchAsync(System.Net.Http.HttpClient http, string name)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return null;

        // 1. Resolve name → CID
        string? cid = null;
        try
        {
            var json = await http.GetStringAsync($"{Base}/pug/compound/name/{Uri.EscapeDataString(name)}/cids/JSON");
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("IdentifierList", out var il)
                && il.TryGetProperty("CID", out var cids)
                && cids.ValueKind == JsonValueKind.Array && cids.GetArrayLength() > 0)
                cid = cids[0].ToString();
        }
        catch { }

        if (string.IsNullOrEmpty(cid)) return null;

        var sheet = new SafetyDataSheet
        {
            Source       = "pubchem",
            ChemicalName = name,
            Cid          = cid,
            SdsUrl       = $"https://pubchem.ncbi.nlm.nih.gov/compound/{cid}#section=Safety-and-Hazards",
            FetchedAt    = DateTime.UtcNow
        };

        // 2. GHS classification
        try
        {
            var json = await http.GetStringAsync($"{Base}/pug_view/data/compound/{cid}/JSON?heading=GHS+Classification");
            var doc = JsonDocument.Parse(json);
            var info = new List<JsonElement>();
            CollectInformation(doc.RootElement, info);

            sheet.SignalWord              = FirstStringFor(info, "Signal");
            sheet.Pictograms              = JoinPictograms(info);
            sheet.HazardStatements        = JoinStringsFor(info, "GHS Hazard Statements", "\n");
            sheet.PrecautionaryStatements = JoinStringsFor(info, "Precautionary Statement", "\n");
        }
        catch { }

        // 3. CAS number
        try
        {
            var json = await http.GetStringAsync($"{Base}/pug_view/data/compound/{cid}/JSON?heading=CAS");
            var doc = JsonDocument.Parse(json);
            var info = new List<JsonElement>();
            CollectInformation(doc.RootElement, info);
            sheet.CasNumber = FirstStringFor(info, "CAS");
        }
        catch { }

        return sheet;
    }

    // When an exact name lookup fails, PubChem's autocomplete maps common / partial / product
    // terms to indexed chemical names (e.g. "bleach" → "Household bleach", "aceton" → "acetone").
    public static async Task<List<string>> SuggestAsync(System.Net.Http.HttpClient http, string name, int limit = 6)
    {
        var suggestions = new List<string>();
        try
        {
            var json = await http.GetStringAsync(
                $"https://pubchem.ncbi.nlm.nih.gov/rest/autocomplete/compound/{Uri.EscapeDataString(name)}/json?limit={limit}");
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("dictionary_terms", out var dt)
                && dt.TryGetProperty("compound", out var comp) && comp.ValueKind == JsonValueKind.Array)
                foreach (var c in comp.EnumerateArray())
                    if (c.ValueKind == JsonValueKind.String)
                    {
                        var v = c.GetString();
                        if (!string.IsNullOrWhiteSpace(v)) suggestions.Add(v!);
                    }
        }
        catch { }
        return suggestions.Take(limit).ToList();
    }

    // PubChem PUG-View nests data as Record → Section[] → (Section[] | Information[]).
    // Walk the tree and collect every Information node.
    private static void CollectInformation(JsonElement el, List<JsonElement> acc)
    {
        if (el.ValueKind != JsonValueKind.Object) return;
        if (el.TryGetProperty("Record", out var rec)) CollectInformation(rec, acc);
        if (el.TryGetProperty("Information", out var infoArr) && infoArr.ValueKind == JsonValueKind.Array)
            foreach (var i in infoArr.EnumerateArray()) acc.Add(i);
        if (el.TryGetProperty("Section", out var secArr) && secArr.ValueKind == JsonValueKind.Array)
            foreach (var s in secArr.EnumerateArray()) CollectInformation(s, acc);
    }

    private static IEnumerable<string> StringsOf(JsonElement information)
    {
        if (information.TryGetProperty("Value", out var val)
            && val.TryGetProperty("StringWithMarkup", out var swm)
            && swm.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in swm.EnumerateArray())
                if (s.TryGetProperty("String", out var str) && str.ValueKind == JsonValueKind.String)
                {
                    var v = str.GetString();
                    if (!string.IsNullOrWhiteSpace(v)) yield return v!.Trim();
                }
        }
    }

    private static JsonElement? FindInfo(List<JsonElement> info, string nameContains)
    {
        foreach (var i in info)
            if (i.TryGetProperty("Name", out var n) && n.ValueKind == JsonValueKind.String
                && n.GetString()!.Contains(nameContains, StringComparison.OrdinalIgnoreCase))
                return i;
        return null;
    }

    private static string? FirstStringFor(List<JsonElement> info, string nameContains)
    {
        var match = FindInfo(info, nameContains);
        return match is null ? null : StringsOf(match.Value).FirstOrDefault();
    }

    private static string? JoinStringsFor(List<JsonElement> info, string nameContains, string sep)
    {
        var match = FindInfo(info, nameContains);
        if (match is null) return null;
        var items = StringsOf(match.Value).Distinct().ToList();
        return items.Count > 0 ? string.Join(sep, items) : null;
    }

    // Pictogram names live in the Markup "Extra" field (the String is just whitespace
    // placeholders for the icons), e.g. "Flammable", "Irritant".
    private static string? JoinPictograms(List<JsonElement> info)
    {
        var match = FindInfo(info, "Pictogram");
        if (match is null) return null;
        var names = new List<string>();
        if (match.Value.TryGetProperty("Value", out var val)
            && val.TryGetProperty("StringWithMarkup", out var swm)
            && swm.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in swm.EnumerateArray())
                if (s.TryGetProperty("Markup", out var markup) && markup.ValueKind == JsonValueKind.Array)
                    foreach (var m in markup.EnumerateArray())
                        if (m.TryGetProperty("Extra", out var extra) && extra.ValueKind == JsonValueKind.String)
                        {
                            var v = extra.GetString();
                            if (!string.IsNullOrWhiteSpace(v) && !names.Contains(v!)) names.Add(v!);
                        }
        }
        return names.Count > 0 ? string.Join("; ", names) : null;
    }
}

internal class Program
{
    // Shared JSON shape for an SDS row returned to the browser.
    internal static object SdsJson(SafetyDataSheet s) => new
    {
        s.Id, s.InventoryItemId, s.Source, s.ChemicalName, s.Cid, s.CasNumber,
        s.SignalWord, s.Pictograms, s.HazardStatements, s.PrecautionaryStatements,
        s.SdsUrl, s.FetchedAt
    };

    [STAThread]
    static void Main(string[] args)
    {
#if LINUX
        // Linux runs headless as a systemd service — no tray companion.
        // UseSystemd (unlike UseWindowsService) does not set the content root, so
        // pin it to the executable's directory; otherwise static files (wwwroot)
        // and appsettings.json are resolved against the service's working dir ("/").
        CreateHostBuilder(args)
            .UseContentRoot(AppContext.BaseDirectory)
            .UseSystemd()
            .Build()
            .Run();
#else
        if (WindowsServiceHelpers.IsWindowsService())
        {
            RunAsService(args);
        }
        else
        {
            RunAsTrayApplication(args);
        }
#endif
    }

#if !LINUX
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
#endif

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(web =>
            {
                var isDev = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
                var httpPort = isDev ? 5051 : 5050;
                var https = LoadHttpsConfig();

                var useLetsEncrypt = https is { Enabled: true, Mode: "letsencrypt" }
                    && !string.IsNullOrWhiteSpace(https.Domain) && https.TosAccepted;

                web.ConfigureServices(services =>
                {
                    ConfigureServices(services);

                    if (useLetsEncrypt)
                    {
                        // In-process ACME (HTTP-01). LettuceEncrypt obtains the cert on
                        // startup, serves the challenge via an IStartupFilter, and renews
                        // automatically in the background with SNI hot-reload.
                        services.AddLettuceEncrypt(o =>
                        {
                            o.AcceptTermsOfService = true;
                            o.EmailAddress = https.Email ?? string.Empty;
                            o.DomainNames  = new[] { https.Domain! };
                        })
                        .PersistDataToDirectory(
                            new DirectoryInfo(InventoryStore.App.Utilities.AppPaths.DataDir),
                            https.CertPassword);
                    }
                });

                web.Configure(app => ConfigureApp(app, https));

                web.UseKestrel(options =>
                {
                    options.ListenAnyIP(httpPort);

                    if (https.Enabled && (useLetsEncrypt || File.Exists(https.CertPath)))
                    {
                        // Port 80 carries the ACME HTTP-01 challenge and (see ConfigureApp)
                        // redirects everything else to HTTPS.
                        options.ListenAnyIP(80);

                        options.ListenAnyIP(https.Port, listen =>
                        {
                            if (useLetsEncrypt)
                                listen.UseHttps(h => h.UseLettuceEncrypt(options.ApplicationServices));
                            else
                                listen.UseHttps(https.CertPath, https.CertPassword);
                        });
                    }
                });
            });

    internal sealed record HttpsSettings(
        bool Enabled, string Mode, int Port, string CertPath, string? CertPassword,
        string? Domain, string? Email, bool TosAccepted);

    static HttpsSettings LoadHttpsConfig()
    {
        var dataDir  = InventoryStore.App.Utilities.AppPaths.DataDir;
        var certPath = Path.Combine(dataDir, "https.pfx");
        var dbPath   = Path.Combine(dataDir, "inventory.db");
        var disabled = new HttpsSettings(false, "manual", 443, certPath, null, null, null, false);
        if (!File.Exists(dbPath)) return disabled;
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
            var mode     = Get("https.mode") ?? "manual";
            var port     = int.TryParse(Get("https.port"), out var p) ? p : 443;
            var password = Get("https.cert.password");
            var domain   = Get("https.domain");
            var email    = Get("https.letsencrypt.email");
            var tos      = Get("https.letsencrypt.tos") == "true";
            return new HttpsSettings(enabled, mode, port, certPath, password, domain, email, tos);
        }
        catch { return disabled; }
    }

    static void ConfigureServices(IServiceCollection services)
    {
        var isDev = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        var dbFileName = isDev ? "inventory-dev.db" : "inventory.db";
        var dbPath = Path.Combine(InventoryStore.App.Utilities.AppPaths.DataDir, dbFileName);

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        services.AddInfrastructure($"Data Source={dbPath}");
        // Emit every DateTime as explicit UTC (trailing 'Z') so the browser — the single authority
        // for time-zone rendering — parses them correctly. Covers MVC/Razor JsonResult responses…
        services.AddRazorPages().AddJsonOptions(o =>
            o.JsonSerializerOptions.Converters.Add(new InventoryStore.App.Infrastructure.UtcDateTimeConverter()));
        // …and the minimal-API endpoints that return via Results.Ok.
        services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.Converters.Add(new InventoryStore.App.Infrastructure.UtcDateTimeConverter()));
        services.AddHttpContextAccessor();
        services.AddHttpClient("ntfy");
        services.AddHttpClient("barcode", c =>
        {
            c.DefaultRequestHeaders.UserAgent.ParseAdd("InventoryStore/1.0 (barcode-lookup)");
            c.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddHttpClient("sds", c =>
        {
            c.DefaultRequestHeaders.UserAgent.ParseAdd("InventoryStore/1.0 (sds-lookup)");
            c.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddScoped<InventoryStore.Application.Interfaces.Services.INtfyService,
                           InventoryStore.App.Services.NtfyService>();
        services.AddHttpClient("webhook", c =>
        {
            c.DefaultRequestHeaders.UserAgent.ParseAdd("InventoryStore/1.0 (webhooks)");
            c.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddHttpClient("posthog", c =>
        {
            c.DefaultRequestHeaders.UserAgent.ParseAdd("InventoryStore/1.0 (usage)");
            c.Timeout = TimeSpan.FromSeconds(8);
        });
        services.AddScoped<InventoryStore.Application.Interfaces.Services.IWebhookService,
                           InventoryStore.App.Services.WebhookService>();

        services.AddScoped<InventoryStore.App.Modules.IModuleRegistry, InventoryStore.App.Modules.ModuleRegistry>();

        // Deployment flags read from environment variables (e.g. PROFESSIONAL_SERVICES_HOSTED).
        services.AddSingleton<InventoryStore.Application.Interfaces.Services.IHostingMode,
                              InventoryStore.App.Services.HostingMode>();

        services.AddSingleton<TunnelService>();
        services.AddSingleton<AppTimeZone>();
        // Same instance behind the Application-layer interface so services share one source of truth.
        services.AddSingleton<InventoryStore.Application.Interfaces.Services.IAppTimeZone>(
            sp => sp.GetRequiredService<AppTimeZone>());
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
                        // Preserve the originally requested page (e.g. /Terminal) as ReturnUrl so
                        // login sends the user back where they were headed.
                        ctx.Response.Redirect(ctx.RedirectUri);
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

    static void ConfigureApp(IApplicationBuilder app, HttpsSettings https)
    {
        var env = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();

        if (env.IsDevelopment())
            app.UseDeveloperExceptionPage();
        else
            app.UseExceptionHandler("/Error");

        // When HTTPS is enabled, redirect plain HTTP arriving on port 80 to HTTPS. The ACME
        // HTTP-01 challenge (served first by LettuceEncrypt's startup filter) is excluded so
        // certificate issuance and renewal keep working over port 80.
        if (https.Enabled)
        {
            app.Use(async (ctx, next) =>
            {
                if (!ctx.Request.IsHttps
                    && ctx.Connection.LocalPort == 80
                    && !ctx.Request.Path.StartsWithSegments("/.well-known/acme-challenge"))
                {
                    var portSuffix = https.Port == 443 ? "" : $":{https.Port}";
                    var target = $"https://{ctx.Request.Host.Host}{portSuffix}" +
                                 $"{ctx.Request.PathBase}{ctx.Request.Path}{ctx.Request.QueryString}";
                    ctx.Response.Redirect(target, permanent: false);
                    return;
                }
                await next();
            });
        }

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

            // ── Safety Data Sheets module ─────────────────────────────────
            // All endpoints return 404 when the module is disabled in Settings → Modules.

            // Stored SDS for an item (read by the view + edit modals).
            endpoints.MapGet("/api/sds/item/{itemId:int}", [Authorize] async (
                int itemId, IModuleRegistry modules, ISafetyDataSheetRepository sdsRepo) =>
            {
                if (!await modules.IsEnabledAsync("sds")) return Results.NotFound();
                var rows = await sdsRepo.GetByInventoryItemIdAsync(itemId);
                return Results.Ok(rows.Select(SdsJson));
            });

            // Look up safety data from PubChem and (re)attach it to the item.
            endpoints.MapGet("/api/sds/lookup", [Authorize(Roles = "Admin,Manager")] async (
                int itemId, string? name, IModuleRegistry modules,
                ISafetyDataSheetRepository sdsRepo, IHttpClientFactory httpFactory) =>
            {
                if (!await modules.IsEnabledAsync("sds")) return Results.NotFound();
                name = (name ?? "").Trim();
                if (string.IsNullOrWhiteSpace(name)) return Results.BadRequest(new { error = "name required" });

                var http  = httpFactory.CreateClient("sds");
                var sheet = await PubChemSdsFetcher.FetchAsync(http, name);
                if (sheet is null)
                {
                    // Brand / product / common names often aren't indexed — offer chemical-name suggestions.
                    var suggestions = await PubChemSdsFetcher.SuggestAsync(http, name);
                    return Results.Ok(new
                    {
                        success = false,
                        error = $"No exact chemical match for \"{name}\".",
                        suggestions
                    });
                }

                sheet.InventoryItemId = itemId;
                // itemId <= 0 is a preview (the item does not exist yet, e.g. the Add modal): fetch
                // and return the data without persisting. The caller attaches it after the item is created.
                if (itemId > 0)
                {
                    await sdsRepo.DeleteByInventoryItemIdAsync(itemId);
                    await sdsRepo.AddRangeAsync(new[] { sheet });
                }

                return Results.Ok(new { success = true, sheets = new[] { SdsJson(sheet) } });
            });

            // All SDS rows for the Settings → Modules metadata table (with item name).
            endpoints.MapGet("/api/sds", [Authorize(Roles = "Admin,Manager")] async (
                IModuleRegistry modules, AppDbContext db) =>
            {
                if (!await modules.IsEnabledAsync("sds")) return Results.NotFound();

                var rows = await db.SafetyDataSheets.OrderByDescending(s => s.FetchedAt).ToListAsync();
                var itemIds = rows.Select(r => r.InventoryItemId).Distinct().ToList();
                var nameMap = await db.InventoryItems
                    .Where(i => itemIds.Contains(i.Id))
                    .Select(i => new { i.Id, i.Name })
                    .ToDictionaryAsync(i => i.Id, i => i.Name);

                return Results.Ok(rows.Select(s => new
                {
                    s.Id, s.InventoryItemId,
                    ItemName = nameMap.TryGetValue(s.InventoryItemId, out var nm) ? nm : "(deleted item)",
                    s.Source, s.ChemicalName, s.Cid, s.CasNumber, s.SignalWord,
                    s.Pictograms, s.HazardStatements, s.PrecautionaryStatements, s.SdsUrl, s.FetchedAt
                }));
            });

            endpoints.MapPost("/api/sds/{id:int}/delete", [Authorize(Roles = "Admin,Manager")] async (
                int id, IModuleRegistry modules, ISafetyDataSheetRepository sdsRepo) =>
            {
                if (!await modules.IsEnabledAsync("sds")) return Results.NotFound();
                await sdsRepo.DeleteAsync(id);
                return Results.Ok(new { success = true });
            });

            // ── Cost & Valuation module ───────────────────────────────────
            endpoints.MapGet("/api/cost/item/{itemId:int}", [Authorize] async (
                int itemId, IModuleRegistry modules, IItemCostRepository costRepo) =>
            {
                if (!await modules.IsEnabledAsync("cost")) return Results.NotFound();
                var row = await costRepo.GetByInventoryItemIdAsync(itemId);
                return Results.Ok(row is null ? null : new
                {
                    row.InventoryItemId, row.UnitCost, row.PurchaseDate, row.UsefulLifeMonths
                });
            });

            endpoints.MapPost("/api/cost/item/{itemId:int}", [Authorize(Roles = "Admin,Manager")] async (
                int itemId, CostInput body, IModuleRegistry modules, IItemCostRepository costRepo) =>
            {
                if (!await modules.IsEnabledAsync("cost")) return Results.NotFound();
                if (body.UnitCost < 0) return Results.BadRequest(new { error = "Unit cost cannot be negative." });
                DateOnly? purchase = DateOnly.TryParse(body.PurchaseDate, out var d) ? d : null;
                await costRepo.UpsertAsync(itemId, body.UnitCost, purchase, body.UsefulLifeMonths);
                return Results.Ok(new { success = true });
            });

            endpoints.MapGet("/api/cost/valuation", [Authorize(Roles = "Admin,Manager")] async (
                IModuleRegistry modules, ISettingsService settings, AppDbContext db) =>
            {
                if (!await modules.IsEnabledAsync("cost")) return Results.NotFound();
                var currency = await settings.GetAsync("module.cost.currency") ?? "$";

                var costs = await db.ItemCosts.ToListAsync();
                var costMap = costs.ToDictionary(c => c.InventoryItemId);
                var items = await db.InventoryItems
                    .Include(i => i.Category)
                    .ToListAsync();

                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var rows = new List<(decimal LineValue, object Row)>();
                decimal totalValue = 0m, totalBookValue = 0m;
                var byCategory = new Dictionary<string, decimal>();

                foreach (var item in items)
                {
                    if (!costMap.TryGetValue(item.Id, out var cost)) continue;
                    var qty       = item.Quantity;
                    var lineValue = cost.UnitCost * qty;
                    totalValue   += lineValue;

                    // Straight-line book value (reusables with a useful life and purchase date).
                    decimal bookValue = lineValue;
                    if (item is ReusableItem && cost.PurchaseDate is { } pd && cost.UsefulLifeMonths is > 0)
                    {
                        var monthsOwned = (today.Year - pd.Year) * 12 + today.Month - pd.Month;
                        var remaining   = Math.Clamp(1m - (decimal)monthsOwned / cost.UsefulLifeMonths.Value, 0m, 1m);
                        bookValue       = lineValue * remaining;
                    }
                    totalBookValue += bookValue;

                    var catName = item.Category?.Name ?? "Uncategorized";
                    byCategory[catName] = byCategory.GetValueOrDefault(catName) + lineValue;

                    rows.Add((lineValue, new
                    {
                        item.Id, item.Name, item.Quantity, cost.UnitCost,
                        LineValue = lineValue, BookValue = bookValue,
                        cost.PurchaseDate, cost.UsefulLifeMonths, Category = catName
                    }));
                }

                return Results.Ok(new
                {
                    currency,
                    totalValue,
                    totalBookValue,
                    itemsCosted = rows.Count,
                    byCategory = byCategory.OrderByDescending(kv => kv.Value).Select(kv => new { Category = kv.Key, Value = kv.Value }),
                    items = rows.OrderByDescending(r => r.LineValue).Select(r => r.Row)
                });
            });

            // ── Consumption Forecasting module ────────────────────────────
            endpoints.MapGet("/api/forecast", [Authorize(Roles = "Admin,Manager")] async (
                IModuleRegistry modules, ISettingsService settings,
                IStockMovementRepository movements, AppDbContext db) =>
            {
                if (!await modules.IsEnabledAsync("forecast")) return Results.NotFound();

                var windowDays = int.TryParse(await settings.GetAsync("module.forecast.windowdays"), out var w) && w > 0 ? w : 30;
                var cutoff     = DateTime.UtcNow.AddDays(-windowDays);

                var consumes = (await movements.GetConsumeSinceAsync(cutoff)).ToList();
                var usedByItem = consumes.GroupBy(m => m.InventoryItemId)
                    .ToDictionary(g => g.Key, g => g.Sum(m => m.Quantity));

                // Consumables only — reusables don't deplete the same way.
                var items = await db.InventoryItems.OfType<ConsumableItem>().ToListAsync();

                var rows = new List<(double Sort, object Row)>();
                foreach (var item in items)
                {
                    if (!usedByItem.TryGetValue(item.Id, out var used) || used <= 0) continue;
                    var avgDaily = (double)used / windowDays;
                    double? daysRemaining = avgDaily > 0 ? item.Quantity / avgDaily : null;
                    DateTime? runOut = daysRemaining.HasValue ? DateTime.UtcNow.AddDays(daysRemaining.Value) : null;
                    rows.Add((daysRemaining ?? double.MaxValue, new
                    {
                        item.Id, item.Name, item.Quantity,
                        AvgDailyUse = avgDaily, DaysRemaining = daysRemaining,
                        RunOutDate = runOut
                    }));
                }

                return Results.Ok(rows.OrderBy(r => r.Sort).Select(r => r.Row));
            });

            // ── Webhooks module ───────────────────────────────────────────
            endpoints.MapGet("/api/webhooks", [Authorize(Roles = "Admin")] async (
                IModuleRegistry modules, IWebhookEndpointRepository repo) =>
            {
                if (!await modules.IsEnabledAsync("webhooks")) return Results.NotFound();
                var rows = await repo.GetAllAsync();
                return Results.Ok(rows.Select(w => new
                {
                    w.Id, w.Url, w.Events, w.Enabled, w.LastStatus, w.LastSentAt,
                    signed = !string.IsNullOrWhiteSpace(w.Secret)
                }));
            });

            endpoints.MapPost("/api/webhooks", [Authorize(Roles = "Admin")] async (
                WebhookInput body, IModuleRegistry modules, IWebhookEndpointRepository repo) =>
            {
                if (!await modules.IsEnabledAsync("webhooks")) return Results.NotFound();
                if (string.IsNullOrWhiteSpace(body.Url) || !Uri.TryCreate(body.Url, UriKind.Absolute, out _))
                    return Results.BadRequest(new { error = "A valid absolute URL is required." });
                var created = await repo.CreateAsync(new WebhookEndpoint
                {
                    Url = body.Url.Trim(),
                    Events = string.IsNullOrWhiteSpace(body.Events) ? "all" : body.Events.Trim(),
                    Secret = string.IsNullOrWhiteSpace(body.Secret) ? null : body.Secret.Trim(),
                    Enabled = true,
                    CreatedAt = DateTime.UtcNow
                });
                return Results.Ok(new { success = true, created.Id });
            });

            endpoints.MapPost("/api/webhooks/{id:int}/delete", [Authorize(Roles = "Admin")] async (
                int id, IModuleRegistry modules, IWebhookEndpointRepository repo) =>
            {
                if (!await modules.IsEnabledAsync("webhooks")) return Results.NotFound();
                await repo.DeleteAsync(id);
                return Results.Ok(new { success = true });
            });

            endpoints.MapPost("/api/webhooks/{id:int}/test", [Authorize(Roles = "Admin")] async (
                int id, IModuleRegistry modules, IWebhookService webhooks) =>
            {
                if (!await modules.IsEnabledAsync("webhooks")) return Results.NotFound();
                var (ok, status, error) = await webhooks.SendTestAsync(id);
                return Results.Ok(new { ok, status, error });
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
                return Results.Ok(results.Select(i => new { i.Id, i.Name, i.Location, i.SKU, i.ItemType, i.AvailableQuantity, i.CategoryName, i.CategoryColor, Tags = i.Tags.Select(t => t.Name) }));
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

            endpoints.MapPost("/api/inventory/checkout", [Authorize(Roles = "Admin,Manager,Staff")] async (HttpContext ctx, ICheckoutService svc) =>
            {
                var dto = await ctx.Request.ReadFromJsonAsync<CheckOutRequest>();
                if (dto is null) return Results.BadRequest();
                var (uid, uname) = HttpContextExtensions.GetUser(ctx);
                try
                {
                    var record = await svc.CheckOutAsync(new Application.DTOs.CheckOutItemDto(dto.ItemId, dto.CheckedOutBy, dto.Quantity, dto.Notes, dto.ClientId), uid, uname);
                    return Results.Ok(record);
                }
                catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
                {
                    return Results.Text(ex.Message, "text/plain", null, StatusCodes.Status400BadRequest);
                }
            });

            endpoints.MapPost("/api/inventory/checkin", [Authorize(Roles = "Admin,Manager,Staff")] async (HttpContext ctx, ICheckoutService svc) =>
            {
                var dto = await ctx.Request.ReadFromJsonAsync<CheckInRequest>();
                if (dto is null) return Results.BadRequest();
                var (uid, uname) = HttpContextExtensions.GetUser(ctx);
                try
                {
                    var record = await svc.CheckInAsync(new Application.DTOs.CheckInItemDto(dto.RecordId, dto.Notes), uid, uname);
                    return Results.Ok(record);
                }
                catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
                {
                    return Results.Text(ex.Message, "text/plain", null, StatusCodes.Status400BadRequest);
                }
            });

            endpoints.MapPost("/api/inventory/lost", [Authorize(Roles = "Admin,Manager")] async (HttpContext ctx, ICheckoutService svc) =>
            {
                var dto = await ctx.Request.ReadFromJsonAsync<MarkLostRequest>();
                if (dto is null) return Results.BadRequest();
                var (uid, uname) = HttpContextExtensions.GetUser(ctx);
                try
                {
                    var record = await svc.MarkLostAsync(new Application.DTOs.MarkLostDto(dto.RecordId, dto.Notes), uid, uname);
                    return Results.Ok(record);
                }
                catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
                {
                    return Results.Text(ex.Message, "text/plain", null, StatusCodes.Status400BadRequest);
                }
            });

            endpoints.MapPost("/api/inventory/consume", [Authorize(Roles = "Admin,Manager,Staff")] async (HttpContext ctx, ICheckoutService svc) =>
            {
                var dto = await ctx.Request.ReadFromJsonAsync<ConsumeRequest>();
                if (dto is null) return Results.BadRequest();
                var (uid, uname) = HttpContextExtensions.GetUser(ctx);
                try
                {
                    await svc.ConsumeAsync(new Application.DTOs.ConsumeItemDto(dto.ItemId, dto.Quantity, dto.Notes), uid, uname);
                    return Results.Ok();
                }
                catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
                {
                    return Results.Text(ex.Message, "text/plain", null, StatusCodes.Status400BadRequest);
                }
            });

            endpoints.MapPost("/api/inventory/restock", [Authorize(Roles = "Admin,Manager")] async (HttpContext ctx, ICheckoutService svc) =>
            {
                var dto = await ctx.Request.ReadFromJsonAsync<RestockRequest>();
                if (dto is null) return Results.BadRequest();
                var (uid, uname) = HttpContextExtensions.GetUser(ctx);
                try
                {
                    await svc.RestockAsync(new Application.DTOs.RestockItemDto(dto.ItemId, dto.Quantity, dto.Notes), uid, uname);
                    return Results.Ok();
                }
                catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
                {
                    return Results.Text(ex.Message, "text/plain", null, StatusCodes.Status400BadRequest);
                }
            });

            // ── Kits API ──────────────────────────────────────────────────
            // Whole-kit checkout/consume return { completed, needsConfirmation, allowPartial, shortages[] }
            // so the caller can offer cancel / proceed-with-available when a member is short.
            endpoints.MapGet("/api/inventory/kit/status/{id:int}", [Authorize] async (int id, ICheckoutService svc) =>
            {
                try { var status = await svc.GetItemStatusAsync(id); return Results.Ok(status); }
                catch { return Results.Ok((object?)null); }
            });

            endpoints.MapPost("/api/inventory/kit/checkout", [Authorize(Roles = "Admin,Manager,Staff")] async (HttpContext ctx, IKitService svc) =>
            {
                var dto = await ctx.Request.ReadFromJsonAsync<KitActionRequest>();
                if (dto is null) return Results.BadRequest();
                if (string.IsNullOrWhiteSpace(dto.CheckedOutBy))
                    return Results.Text("Enter a borrower name first.", "text/plain", null, StatusCodes.Status400BadRequest);
                var (uid, uname) = HttpContextExtensions.GetUser(ctx);
                try
                {
                    var result = await svc.CheckOutKitAsync(
                        new Application.DTOs.KitActionDto(dto.KitId, dto.Quantity, dto.CheckedOutBy, dto.ClientId, dto.Notes, dto.AllowPartialFallback), uid, uname);
                    return Results.Ok(result);
                }
                catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
                {
                    return Results.Text(ex.Message, "text/plain", null, StatusCodes.Status400BadRequest);
                }
            });

            endpoints.MapPost("/api/inventory/kit/checkin", [Authorize(Roles = "Admin,Manager,Staff")] async (
                HttpContext ctx, IKitService svc, IModuleRegistry modules, ISettingsService settings) =>
            {
                var dto = await ctx.Request.ReadFromJsonAsync<KitCheckinRequest>();
                if (dto is null) return Results.BadRequest();
                var (uid, uname) = HttpContextExtensions.GetUser(ctx);
                try
                {
                    // The kit gets flagged for later reconciliation only when the feature is on and the
                    // user checked it in without recording usage (the Skip path).
                    var reconcileOn = await modules.IsEnabledAsync("kits")
                        && await settings.GetAsync("module.kits.reconcile") == "true";
                    var usage = dto.Usage?
                        .Select(u => new Application.DTOs.KitConsumableUsageDto(u.ConsumableItemId, u.UsedQuantity))
                        .ToList();
                    await svc.CheckInKitAsync(dto.KitCheckoutId, usage, reconcileOn, uid, uname);
                    return Results.Ok();
                }
                catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
                {
                    return Results.Text(ex.Message, "text/plain", null, StatusCodes.Status400BadRequest);
                }
            });

            // Consumable lines + framing for the check-in / reconcile modal. Returns enabled:false
            // (and no lines) when the option is off, so the UI just checks in directly.
            endpoints.MapGet("/api/inventory/kit/reconcile/meta/{kitCheckoutId:int}", [Authorize(Roles = "Admin,Manager,Staff")] async (
                int kitCheckoutId, IKitService svc, IModuleRegistry modules, ISettingsService settings) =>
            {
                var enabled = await modules.IsEnabledAsync("kits")
                    && await settings.GetAsync("module.kits.reconcile") == "true";
                if (!enabled) return Results.Ok(new { enabled = false, mode = "used", lines = Array.Empty<object>() });
                var mode = await settings.GetAsync("module.kits.reconcile.mode") ?? "used";
                var info = await svc.GetReconciliationAsync(kitCheckoutId);
                return Results.Ok(new { enabled = true, mode, lines = info?.Lines ?? (IReadOnlyList<Application.DTOs.KitReconcileLineDto>)Array.Empty<Application.DTOs.KitReconcileLineDto>() });
            });

            // Checked-in kits still awaiting a consumable count (reconciliation report).
            endpoints.MapGet("/api/inventory/kit/reconcile/pending", [Authorize(Roles = "Admin,Manager")] async (
                IKitService svc, IModuleRegistry modules, ISettingsService settings) =>
            {
                var enabled = await modules.IsEnabledAsync("kits")
                    && await settings.GetAsync("module.kits.reconcile") == "true";
                if (!enabled) return Results.Ok(Array.Empty<Application.DTOs.KitReconcileDto>());
                return Results.Ok(await svc.GetPendingReconciliationsAsync());
            });

            // Reconcile a kit that was checked in without recording usage.
            endpoints.MapPost("/api/inventory/kit/reconcile", [Authorize(Roles = "Admin,Manager")] async (
                HttpContext ctx, IKitService svc, IModuleRegistry modules, ISettingsService settings) =>
            {
                var enabled = await modules.IsEnabledAsync("kits")
                    && await settings.GetAsync("module.kits.reconcile") == "true";
                if (!enabled) return Results.NotFound();
                var dto = await ctx.Request.ReadFromJsonAsync<KitReconcileRequest>();
                if (dto is null) return Results.BadRequest();
                var (uid, uname) = HttpContextExtensions.GetUser(ctx);
                try
                {
                    var usage = (dto.Usage ?? new List<KitConsumableUsageInput>())
                        .Select(u => new Application.DTOs.KitConsumableUsageDto(u.ConsumableItemId, u.UsedQuantity))
                        .ToList();
                    await svc.ReconcileKitAsync(dto.KitCheckoutId, usage, uid, uname);
                    return Results.Ok();
                }
                catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
                {
                    return Results.Text(ex.Message, "text/plain", null, StatusCodes.Status400BadRequest);
                }
            });

            endpoints.MapPost("/api/inventory/kit/lost", [Authorize(Roles = "Admin,Manager")] async (HttpContext ctx, IKitService svc) =>
            {
                var dto = await ctx.Request.ReadFromJsonAsync<KitCheckoutRefRequest>();
                if (dto is null) return Results.BadRequest();
                var (uid, uname) = HttpContextExtensions.GetUser(ctx);
                try
                {
                    await svc.MarkKitLostAsync(dto.KitCheckoutId, uid, uname);
                    return Results.Ok();
                }
                catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
                {
                    return Results.Text(ex.Message, "text/plain", null, StatusCodes.Status400BadRequest);
                }
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

            endpoints.MapPost("/api/clients/quick-create", [Authorize(Roles = "Admin,Manager,Staff")] async (HttpContext ctx, IClientService svc) =>
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

            // ── Vendors API (Maintenance module) ──────────────────────────
            // Mirrors the Clients API: searchable, case-insensitive, quick-create.
            endpoints.MapGet("/api/vendors/search", [Authorize] async (string? q, IVendorService svc) =>
            {
                if (string.IsNullOrWhiteSpace(q)) return Results.Ok(Array.Empty<object>());
                var vendors = await svc.SearchAsync(q);
                return Results.Ok(vendors.Select(v => new { v.Id, v.Name, v.Phone, v.Email }));
            });

            endpoints.MapGet("/api/vendors", [Authorize] async (IVendorService svc) =>
                Results.Ok(await svc.GetAllAsync()));

            endpoints.MapGet("/api/vendors/{id:int}", [Authorize] async (int id, IVendorService svc) =>
            {
                var vendor = await svc.GetByIdAsync(id);
                return vendor is not null ? Results.Ok(vendor) : Results.NotFound();
            });

            endpoints.MapPost("/api/vendors", [Authorize(Roles = "Admin,Manager")] async (HttpContext ctx, IVendorService svc) =>
            {
                var dto = await ctx.Request.ReadFromJsonAsync<Application.DTOs.CreateVendorDto>();
                if (dto is null || string.IsNullOrWhiteSpace(dto.Name))
                    return Results.BadRequest(new { error = "Name is required." });
                var vendor = await svc.CreateAsync(dto);
                return Results.Ok(vendor);
            });

            endpoints.MapPut("/api/vendors/{id:int}", [Authorize(Roles = "Admin,Manager")] async (int id, HttpContext ctx, IVendorService svc) =>
            {
                var dto = await ctx.Request.ReadFromJsonAsync<Application.DTOs.UpdateVendorDto>();
                if (dto is null || string.IsNullOrWhiteSpace(dto.Name))
                    return Results.BadRequest(new { error = "Name is required." });
                try { await svc.UpdateAsync(id, dto); return Results.Ok(); }
                catch (KeyNotFoundException) { return Results.NotFound(); }
            });

            endpoints.MapDelete("/api/vendors/{id:int}", [Authorize(Roles = "Admin,Manager")] async (int id, IVendorService svc) =>
            {
                await svc.DeleteAsync(id);
                return Results.Ok();
            });

            endpoints.MapPost("/api/vendors/quick-create", [Authorize(Roles = "Admin,Manager,Staff")] async (HttpContext ctx, IVendorService svc) =>
            {
                var body = await ctx.Request.ReadFromJsonAsync<QuickCreateVendorRequest>();
                if (body is null || string.IsNullOrWhiteSpace(body.Name))
                    return Results.BadRequest(new { error = "Name is required." });
                var vendor = await svc.QuickCreateAsync(body.Name);
                return Results.Ok(vendor);
            });

            // ── Maintenance module ────────────────────────────────────────
            // All endpoints 404 when the module is disabled, matching the SDS/Cost modules.
            endpoints.MapGet("/api/inventory/maintenance/status/{id:int}", [Authorize] async (
                int id, IModuleRegistry modules, IMaintenanceService svc) =>
            {
                if (!await modules.IsEnabledAsync("maintenance")) return Results.NotFound();
                return Results.Ok(await svc.GetItemStatusAsync(id));
            });

            endpoints.MapPost("/api/inventory/maintenance/schedule", [Authorize(Roles = "Admin,Manager")] async (
                HttpContext ctx, IModuleRegistry modules, IMaintenanceService svc) =>
            {
                if (!await modules.IsEnabledAsync("maintenance")) return Results.NotFound();
                var dto = await ctx.Request.ReadFromJsonAsync<MaintenanceScheduleRequest>();
                if (dto is null) return Results.BadRequest();
                var (uid, uname) = HttpContextExtensions.GetUser(ctx);
                DateOnly? last = DateOnly.TryParse(dto.LastMaintainedDate, out var d) ? d : null;
                var unit = Enum.IsDefined(typeof(InventoryStore.Domain.Enums.MaintenanceIntervalUnit), dto.IntervalUnit)
                    ? (InventoryStore.Domain.Enums.MaintenanceIntervalUnit)dto.IntervalUnit
                    : InventoryStore.Domain.Enums.MaintenanceIntervalUnit.Months;
                try
                {
                    await svc.SaveScheduleAsync(
                        new Application.DTOs.SaveMaintenanceScheduleDto(dto.ItemId, last, dto.IntervalValue, unit, dto.Notes), uid, uname);
                    return Results.Ok(new { success = true });
                }
                catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
                {
                    return Results.Text(ex.Message, "text/plain", null, StatusCodes.Status400BadRequest);
                }
            });

            endpoints.MapPost("/api/inventory/maintenance/out", [Authorize(Roles = "Admin,Manager,Staff")] async (
                HttpContext ctx, IModuleRegistry modules, IMaintenanceService svc) =>
            {
                if (!await modules.IsEnabledAsync("maintenance")) return Results.NotFound();
                var dto = await ctx.Request.ReadFromJsonAsync<MaintenanceOutRequest>();
                if (dto is null) return Results.BadRequest();
                var (uid, uname) = HttpContextExtensions.GetUser(ctx);
                try
                {
                    await svc.MarkOutAsync(
                        new Application.DTOs.MarkOutForMaintenanceDto(dto.ItemId, dto.Quantity, dto.VendorId, dto.Notes), uid, uname);
                    return Results.Ok();
                }
                catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
                {
                    return Results.Text(ex.Message, "text/plain", null, StatusCodes.Status400BadRequest);
                }
            });

            endpoints.MapPost("/api/inventory/maintenance/return", [Authorize(Roles = "Admin,Manager,Staff")] async (
                HttpContext ctx, IModuleRegistry modules, IMaintenanceService svc) =>
            {
                if (!await modules.IsEnabledAsync("maintenance")) return Results.NotFound();
                var dto = await ctx.Request.ReadFromJsonAsync<MaintenanceReturnRequest>();
                if (dto is null) return Results.BadRequest();
                var (uid, uname) = HttpContextExtensions.GetUser(ctx);
                try
                {
                    await svc.ReturnAsync(dto.ItemId, dto.Notes, uid, uname);
                    return Results.Ok();
                }
                catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
                {
                    return Results.Text(ex.Message, "text/plain", null, StatusCodes.Status400BadRequest);
                }
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

            // ── One-time usage check-in ───────────────────────────────────
            // Shown once per install to the first admin. "OK" sends a single anonymous
            // "someone is using the app" event to PostHog; opting out (or OK) marks it
            // resolved app-wide so no admin is ever asked again.
            endpoints.MapPost("/api/usage/report", [Authorize(Roles = "Admin")] async (
                UsageReportRequest body, ISettingsService settings, IHttpClientFactory httpFactory, UpdateInfo updateInfo) =>
            {
                await settings.SetAsync("usage.prompt.resolved", "true");

                if (body is { OptOut: true }) return Results.Ok(new { success = true });

                // Stable anonymous id so one install counts as one user, with nothing identifying.
                var installId = await settings.GetAsync("usage.installid");
                if (string.IsNullOrWhiteSpace(installId))
                {
                    installId = Guid.NewGuid().ToString("N");
                    await settings.SetAsync("usage.installid", installId);
                }

                var payload = new
                {
                    api_key     = "phc_u3KvbsAKsLL7jghUPb9i59NpcW5uHrS7wNn9TELwCTrN",
                    @event      = "Device usage",
                    distinct_id = installId,
                    properties  = new
                    {
                        how_heard   = string.IsNullOrWhiteSpace(body?.HowHeard) ? null : body!.HowHeard!.Trim(),
                        app_version = updateInfo.CurrentVersion
                    }
                };

                // Best effort: never let a telemetry hiccup affect the user.
                try
                {
                    var http = httpFactory.CreateClient("posthog");
                    await http.PostAsJsonAsync("https://us.i.posthog.com/capture/", payload);
                }
                catch { }

                return Results.Ok(new { success = true });
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
