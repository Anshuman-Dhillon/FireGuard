using FireGuard.ML;
using FireGuard.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient();

builder.Services.AddSingleton<FireRiskModel>(sp =>
{
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    return new FireRiskModel(env);
});

builder.Services.AddScoped<WeatherService>();
builder.Services.AddScoped<NasaFirmsService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(
            "http://localhost:5173",  // Local development
            "https://fire-guard-olive.vercel.app"  // production
        )
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

if (app.Environment.IsDevelopment() || true) // Force it on for debugging
{
    app.UseDeveloperExceptionPage();
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.Run();
