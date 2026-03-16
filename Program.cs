using Azure.Search.Documents;
using System.Net.Http.Headers;
using FelderBot.Components;
using FelderBot.Options;
using FelderBot.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<OpenAIOptions>(
    builder.Configuration.GetSection(OpenAIOptions.SectionName));
builder.Services.Configure<AzureSearchOptions>(
    builder.Configuration.GetSection(AzureSearchOptions.SectionName));

builder.Services.AddSingleton<IInstructionsLoader, InstructionsLoader>();
builder.Services.AddSingleton<SearchClient>(sp =>
{
    var opt = sp.GetRequiredService<IOptions<AzureSearchOptions>>().Value;
    if (string.IsNullOrWhiteSpace(opt.Endpoint) || string.IsNullOrWhiteSpace(opt.IndexName) || string.IsNullOrWhiteSpace(opt.ApiKey))
        return new SearchClient(new Uri("https://localhost/"), "placeholder", new Azure.AzureKeyCredential("placeholder"));
    var endpoint = new Uri(opt.Endpoint.TrimEnd('/'));
    return new SearchClient(endpoint, opt.IndexName, new Azure.AzureKeyCredential(opt.ApiKey));
});
builder.Services.AddScoped<ISearchService, AzureSearchService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IOpenAIResponsesService, OpenAIResponsesService>();
builder.Services.AddHttpClient<IOpenAIResponsesService, OpenAIResponsesService>((sp, client) =>
{
    var opt = sp.GetRequiredService<IOptions<OpenAIOptions>>().Value;
    var baseUrl = (opt.BaseUrl ?? "https://api.openai.com/v1").TrimEnd('/') + "/";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", opt.ApiKey ?? "");
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
});

builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseSession();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
