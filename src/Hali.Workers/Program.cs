using Hali.Application.Clusters;
using Hali.Infrastructure.Clusters;
using Hali.Infrastructure.Data.Clusters;
using Hali.Workers;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

// PostgreSQL / Clusters DB
builder.Services.AddDbContext<ClustersDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Clusters")));

// Cluster services
builder.Services.AddSingleton<IH3CellService, H3CellService>();
builder.Services.AddScoped<IClusterRepository, ClusterRepository>();
builder.Services.AddScoped<ICivisEvaluationService, CivisEvaluationService>();

builder.Services.Configure<CivisOptions>(builder.Configuration.GetSection(CivisOptions.Section));

// Hosted services
builder.Services.AddHostedService<DecayActiveClustersJob>();

var host = builder.Build();
host.Run();
