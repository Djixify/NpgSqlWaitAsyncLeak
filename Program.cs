
using Npgsql;
using System.Data.Common;
using System.Text.RegularExpressions;

namespace NpgsqlWaitAsyncLeak
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger("Main");

                var connstring = builder.Configuration.GetConnectionString("DefaultConnection");

                var _connection = new NpgsqlConnection(connstring);
                _connection.Open();
                _connection.Notice += (_, e) => logger.LogInformation(e.Notice.MessageText);
                _connection.StateChange += (_, e) => logger.LogInformation($"Connection state changed from {e.OriginalState} to {e.CurrentState}");
                _connection.Notification += (_, e) => logger.LogInformation(e.PID + ": " + e.Channel + ", payload: " + e.Payload);


                CancellationTokenSource tokenSource = new CancellationTokenSource();
                CancellationToken token = tokenSource.Token;

                Task.Run(async () =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        if (_connection.State != System.Data.ConnectionState.Open)
                        {
                            _connection.Open();
                            await Task.Delay(1000);
                        }
                        else
                        {
                            await _connection.WaitAsync(100, token);
                        }
                    }
                }, token);
            }

            app.Run();
        }
    }
}
