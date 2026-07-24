using System.Reflection;

namespace ZktecoRelay.Hosting;

public static partial class RelayApplication
{
    private const string DocumentationHtml = """
        <!doctype html>
        <html lang="zh-CN">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>ZKTeco Relay API</title>
          <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/swagger-ui-dist@5/swagger-ui.css">
          <style>
            html { box-sizing: border-box; overflow-y: scroll; }
            *, *::before, *::after { box-sizing: inherit; }
            body { margin: 0; background: #fafafa; }
          </style>
        </head>
        <body>
          <div id="swagger-ui"></div>
          <script src="https://cdn.jsdelivr.net/npm/swagger-ui-dist@5/swagger-ui-bundle.js"></script>
          <script>
            window.addEventListener("load", () => {
              SwaggerUIBundle({
                url: "/openapi.yaml",
                dom_id: "#swagger-ui",
                deepLinking: true,
                displayRequestDuration: true,
                persistAuthorization: true,
                tryItOutEnabled: true
              });
            });
          </script>
        </body>
        </html>
        """;

    private static void MapDocumentationEndpoints(WebApplication app)
    {
        app.MapGet("/docs", () => Results.Content(DocumentationHtml, "text/html; charset=utf-8"));
        app.MapGet("/openapi.yaml", OpenApiDefinition);
    }

    private static IResult OpenApiDefinition()
    {
        var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("ZktecoRelay.OpenApi.yaml");
        return stream is null
            ? Results.Problem("The embedded OpenAPI definition is unavailable.")
            : Results.Stream(stream, "application/yaml; charset=utf-8");
    }
}
