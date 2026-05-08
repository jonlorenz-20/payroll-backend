var builder = WebApplication.CreateBuilder(args);

// 1. ADD CORS POLICY (Importante para sa MAUI at Swagger connection)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin() // Pinapayagan ang lahat ng connection (MAUI, Browser, etc.)
              .AllowAnyMethod() // Pinapayagan ang GET, POST, DELETE, etc.
              .AllowAnyHeader();
    });
});

// 2. ADD CONTROLLERS
builder.Services.AddControllers();

// 3. ADD SWAGGER
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 4. PAGPAPAGANA NG SWAGGER UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 5. MIDDLEWARE SETTINGS
// Gamitin ang CORS bago ang Authorization at MapControllers
app.UseCors("AllowAll");

// Kung gagamit ka ng HTTP (port 5016), pwedeng i-comment out muna ang HttpsRedirection 
// para iwas SSL connection issues sa emulator.
// app.UseHttpsRedirection(); 

app.UseAuthorization();

// 6. MAP CONTROLLERS
app.MapControllers();

app.Run();