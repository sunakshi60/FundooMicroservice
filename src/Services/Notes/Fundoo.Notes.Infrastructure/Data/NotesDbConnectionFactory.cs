using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fundoo.Notes.Infrastructure.Data
{
    public class NotesDbConnectionFactory
    {
        private readonly string _connectionString;

        public NotesDbConnectionFactory(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("NotesDb")
                ?? throw new InvalidOperationException(
                    "NotesDb connection string is missing."
                );
        }

        public NpgsqlConnection CreateConnection()
        {
            return new NpgsqlConnection(_connectionString);
        }
    }
}
