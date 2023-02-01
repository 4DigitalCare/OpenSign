var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.UseHttpsRedirection();
app.MapGet("/", (HttpRequest aRequest) => MainClass.Hello(aRequest));
app.MapPost("/sign", (HttpRequest aRequest, System.Text.Json.Nodes.JsonObject? aPedido) => MainClass.Assinar(aRequest, aPedido));
app.Run();