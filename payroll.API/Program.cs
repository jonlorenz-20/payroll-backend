using Microsoft.Extensions.Options;
using payroll.API.Models;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        
        policy.WithOrigins(
                    "https://reverend-squint-parish.ngrok-free.dev",
                    "http://localhost",
                    "https://localhost"
              )
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});


builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));


builder.Services.AddControllers();


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    
    app.MapGet("/", () => "Sekyur-Link Payroll API Gateway - Secure Operational Mode Active.");
}


app.UseCors("AllowAll");
app.UseAuthorization();


app.MapControllers();

app.Run();