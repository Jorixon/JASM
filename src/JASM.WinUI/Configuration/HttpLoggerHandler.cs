using System.Reflection;
using Serilog;

namespace GIMI_ModManager.WinUI.Configuration;

public class HttpLoggerHandler : DelegatingHandler
{
    private readonly ILogger _logger;
    private readonly string _version = Assembly.GetExecutingAssembly().GetName().Version!.ToString();

    public HttpLoggerHandler(ILogger logger)
    {
        _logger = logger.ForContext<HttpLoggerHandler>();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _logger.Debug("Sending Request: {METHOD} -> {URI}", request.Method, request.RequestUri);

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);


        if (response.IsSuccessStatusCode)
        {
            _logger.Debug("Received Response: {Uri} -> {StatusCode}", request.RequestUri, response.StatusCode);
        }
        else
        {
            _logger.Information("Non Success Response Received: {METHOD} {Uri} -> {StatusCode}\n\tJASM-version: {Version}",
                request.Method, request.RequestUri, response.StatusCode, _version);
        }

        return response;
    }
}