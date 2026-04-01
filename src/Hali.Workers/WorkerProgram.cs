using Hali.Application.Clusters;
using Hali.Application.Advisories;
using Hali.Application.Participation;
using Hali.Infrastructure.Clusters;
using Hali.Infrastructure.Data.Clusters;
using Hali.Infrastructure.Data.Advisories;
using Hali.Infrastructure.Data.Participation;
using Hali.Infrastructure.Advisories;
using Hali.Infrastructure.Participation;
using Hali.Infrastructure.Extensions;
using Hali.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<DecayActiveClustersJob>();
builder.Services.AddHostedService<ExpireOfficialPostsJob>();
builder.Services.AddHostedService<EvaluatePossibleRestorationJob>();

var host = builder.Build();
host.Run();
