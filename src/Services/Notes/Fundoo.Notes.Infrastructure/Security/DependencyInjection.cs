using Fundoo.Notes.Application.Interfaces;
using Fundoo.Notes.Application.Repositories;
using Fundoo.Notes.Application.Services;
using Fundoo.Notes.Infrastructure.Data;
using Fundoo.Notes.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fundoo.Notes.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddNotesInfrastructure(this IServiceCollection services,
            IConfiguration configuration)
        {
            // Database connection factory
            services.AddSingleton<NotesDbConnectionFactory>();

            // Note Repository
            services.AddScoped<INotesRepository, NotesRepository>();

            // Application service
            services.AddScoped<INotesService, NotesService>();

            // Label Repository
            services.AddScoped<ILabelsRepository, LabelsRepository>();

            // Label Service
            services.AddScoped<ILabelsService, LabelsService>();

            return services;
        }
    }
}