using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.Model;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromMemory(
        [
            // Movies API
            new RouteConfig
            {
                RouteId = "movies",
                ClusterId = "movies-cluster",
                Match = new RouteMatch { Path = "/api/movies/{**catch-all}" }
            },
            // Все остальное
            new RouteConfig
            {
                RouteId = "default",
                ClusterId = "monolith-cluster",
                Match = new RouteMatch { Path = "/{**catch-all}" }
            }
        ],
        [
            new ClusterConfig
            {
                ClusterId = "movies-cluster",
                LoadBalancingPolicy = "MoviesMigration",
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    ["monolith"] = new() { Address = builder.Configuration["MONOLITH_URL"] ?? "http://localhost:8080" },
                    ["movies-service"] = new() { Address = builder.Configuration["MOVIES_SERVICE_URL"] ?? "http://localhost:8081" }
                }
            },
            new ClusterConfig
            {
                ClusterId = "monolith-cluster",
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    ["monolith"] = new() { Address = builder.Configuration["MONOLITH_URL"] ?? "http://localhost:8080" }
                }
            }
        ]);

// Простая политика балансировки
builder.Services.AddSingleton<MoviesMigrationLoadBalancingPolicy>();
builder.Services.AddSingleton<ILoadBalancingPolicy>(sp => sp.GetRequiredService<MoviesMigrationLoadBalancingPolicy>());

var app = builder.Build();

app.MapReverseProxy();

app.Run();

// Упрощенная политика балансировки
public class MoviesMigrationLoadBalancingPolicy : ILoadBalancingPolicy
{
    private readonly IConfiguration _configuration;
    private readonly Random _random = new();
    private readonly ILogger<MoviesMigrationLoadBalancingPolicy> _logger;

    public MoviesMigrationLoadBalancingPolicy(
        IConfiguration configuration,
        ILogger<MoviesMigrationLoadBalancingPolicy> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public string Name => "MoviesMigration";

    public DestinationState? PickDestination(
        HttpContext context,
        ClusterState cluster,
        IReadOnlyList<DestinationState> availableDestinations)
    {
        if (cluster.ClusterId != "movies-cluster")
        {
            _logger.LogInformation("Current cluster: {ClusterId}", cluster.ClusterId);
            return availableDestinations.First();
        }

        var gradualMigration = bool.Parse(_configuration["GRADUAL_MIGRATION"] ?? "false");
        var migrationPercent = int.Parse(_configuration["MOVIES_MIGRATION_PERCENT"] ?? "50");

        if (!gradualMigration)
        {
            _logger.LogInformation("Migration not started, using monolith");
            return availableDestinations.First(d => d.DestinationId == "monolith");
        }

        // Генерируем случайное число от 1 до 100
        var randomValue = _random.Next(1, 101);

        _logger.LogInformation("Random value: {RandomValue}, Migration percent: {Percent}",
            randomValue, migrationPercent);

        if (randomValue <= migrationPercent)
        {
            // randomValue в диапазоне 1-1 (1% случаев)
            _logger.LogInformation("Routing to movies-service");
            return availableDestinations.First(d => d.DestinationId == "movies-service");
        }
        else
        {
            // randomValue в диапазоне 2-100 (99% случаев)
            _logger.LogInformation("Routing to monolith");
            return availableDestinations.First(d => d.DestinationId == "monolith");
        }
    }
}
