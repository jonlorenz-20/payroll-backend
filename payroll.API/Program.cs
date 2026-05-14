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

//  ADD CONTROLLERS
builder.Services.AddControllers();

//  ADD SWAGGER
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

//  PAGPAPAGANA NG SWAGGER UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//  MIDDLEWARE SETTINGS

app.UseCors("AllowAll");


app.UseAuthorization();

// MAP CONTROLLERS
app.MapControllers();

app.Run();