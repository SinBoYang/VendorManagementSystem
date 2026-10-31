using System.Reflection;
using CSP_API.Services.CSP;
using CSP_API.Infrastructure;

namespace CSP_API
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
            builder.Services.AddScoped<ICspService, CspService>();

            // CORS: allow the frontend running on http://localhost:8080 to call this API
            var corsPolicyName = "AllowLocalhostFrontend";
            var frontendOrigin = "http://localhost:8080";
            builder.Services.AddCors(options =>
            {
                options.AddPolicy(name: corsPolicyName, policy =>
                {
                    policy.WithOrigins(frontendOrigin)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .SetIsOriginAllowed(origin => origin == "null" || origin == frontendOrigin);

                    // Note: allowing origin == "null" is less secure and should be used only for
                    // local development when the frontend is opened directly from the filesystem.
                });
            });

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            // Enable CORS BEFORE Authorization and before mapping controllers so OPTIONS requests
            // get handled and the Access-Control-* headers are returned.
            app.UseCors(corsPolicyName);

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}