using Microsoft.Extensions.Options;
using payroll.API.Models;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin() 
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();


app.Use(async (context, next) =>
{
    
    if (context.Request.Path.StartsWithSegments("/swagger"))
    {
        
        if (!context.Request.Headers.ContainsKey("Authorization"))
        {
            context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Secure Payroll Swagger\"";
            context.Response.StatusCode = 401;
            return;
        }

        var authHeader = context.Request.Headers["Authorization"].ToString();
        if (authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            var credentials = System.Text.Encoding.UTF8.GetString(
                Convert.FromBase64String(authHeader.Substring(6)))
                .Split(':');

           
            // Username: admin | Password: sekyurswaggerpassword!
            if (credentials[0] == "admin" && credentials[1] == "sekyurswaggerpassword!")
            {
                await next();
                return;
            }
        }

        context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Secure Payroll Swagger\"";
        context.Response.StatusCode = 401;
        return;
    }

    await next();
});


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();