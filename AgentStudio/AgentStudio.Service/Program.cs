using AgentStudio.Service.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();

var app = builder.Build();

app.MapGrpcService<AgentFlowServiceImplementation>();

app.Run();