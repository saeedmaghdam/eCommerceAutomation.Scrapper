using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using eCommerceAutomation.Scrapper.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PuppeteerSharp;

namespace eCommerceAutomation.Scrapper
{
    public class FetcherService
    {
        private readonly ILogger<FetcherService> _logger;
        private readonly IOptions<ApplicationOptions> _options;
        private readonly FileRefService _fileRefService;

        private SemaphoreSlim _urlSemaphore = new SemaphoreSlim(3, 3);
        private SemaphoreSlim _telegramSemaphore = new SemaphoreSlim(1, 1);
        private SemaphoreSlim _telegramFileSemaphore = new SemaphoreSlim(1, 1);
        private DateTime? _fileLastModifiedDateTime;

        private const string IsScrollOnFirstPostJavaScriptMethod = @"
function isReachedTheFirstPost() {
    let firstPostDataAttr = document.querySelector('main section.tgme_channel_history > div.tgme_widget_message_wrap > div.tgme_widget_message').attributes[""data-post""].value;
	window.scrollTo(0, 0);
	
	let newFirstPostDataAttr = document.querySelector('main section.tgme_channel_history > div.tgme_widget_message_wrap > div.tgme_widget_message').attributes[""data-post""].value;
	let newFirstPostId = newFirstPostDataAttr.split('/')[1];
	
	if (newFirstPostId == ""1""){
		window.scrollTo(0, 0);
		return true;
	} else if (firstPostDataAttr != newFirstPostDataAttr){
		return false;
	}
    return false;
}
                ";
        private const string IsScrollOnFirstPostJavaScriptMethodName = "isReachedTheFirstPost()";

        public FetcherService(ILogger<FetcherService> logger, IOptions<ApplicationOptions> options, FileRefService fileRefService)
        {
            _logger = logger;
            _options = options;
            _fileRefService = fileRefService;
        }

        public async Task<string> GetUrlContentAsync(string url, CancellationToken cancellationToken)
        {
            string content;

            var currentDirectory = Directory.GetCurrentDirectory();
            var filePath = default(string);

            try
            {
                await _urlSemaphore.WaitAsync();

                var fileRef = await _fileRefService.GetByUrlAsync(url, cancellationToken);
                if (fileRef != null)
                {
                    filePath = Path.Combine(currentDirectory, "TempData", fileRef.FileName);

                    if (File.Exists(filePath))
                    {
                        var urlContentFileLastModifiedDateTime = System.IO.File.GetLastWriteTime(filePath);

                        if ((DateTime.Now - urlContentFileLastModifiedDateTime).TotalMinutes < _options.Value.UrlContentCacheInMinutes)
                            return await File.ReadAllTextAsync(filePath, cancellationToken);
                    }
                }

                using (var client = new HttpClient())
                    content = await client.GetStringAsync(url);

                if (string.IsNullOrEmpty(filePath))
                {
                    var fileName = await _fileRefService.CreateAsync(url, cancellationToken);
                    filePath = Path.Combine(currentDirectory, "TempData", fileName);
                }

                if (!Directory.Exists(Path.Combine(currentDirectory, "TempData")))
                {
                    Directory.CreateDirectory(Path.Combine(currentDirectory, "TempData"));
                    _logger.LogInformation("Create a temp folder behind the service.");
                }
                await File.WriteAllLinesAsync(filePath, new[] { content }, cancellationToken);

                return content;
            }
            finally
            {
                _urlSemaphore.Release();
            }
        }

        public async Task<string> GetTelegramChannelContentAsync(string url, CancellationToken cancellationToken)
        {
            var content = "";

            var currentDirectory = Directory.GetCurrentDirectory();
            var uri = new Uri(url);
            var filePath = Path.Combine(currentDirectory, "TempData", $"{(uri.Host + uri.AbsolutePath).Replace("/", "_")}.txt");

            try
            {
                await _telegramSemaphore.WaitAsync(cancellationToken);

                try
                {
                    await _telegramFileSemaphore.WaitAsync(cancellationToken);
                    if (File.Exists(filePath))
                    {
                        if (_fileLastModifiedDateTime == null)
                            _fileLastModifiedDateTime = System.IO.File.GetLastWriteTime(filePath);

                        if ((DateTime.Now - _fileLastModifiedDateTime).Value.TotalMinutes < _options.Value.TelegramCacheInMinutes)
                            content = await File.ReadAllTextAsync(filePath, cancellationToken);
                    }
                }
                finally
                {
                    _telegramFileSemaphore.Release();
                }

                if (!string.IsNullOrEmpty(content))
                    return content;


                var downloadPath = Path.Combine(currentDirectory, "Chromium");

                _logger.LogInformation($"Attemping to set up puppeteer to use Chromium found under directory {downloadPath} ");

                if (!Directory.Exists(downloadPath))
                {
                    _logger.LogInformation("Custom directory not found. Creating directory");
                    Directory.CreateDirectory(downloadPath);
                }

                _logger.LogInformation("Downloading Chromium");

                var browserFetcherOptions = new BrowserFetcherOptions { Path = downloadPath };
                var browserFetcher = new BrowserFetcher(browserFetcherOptions);
                await browserFetcher.DownloadAsync(BrowserFetcher.DefaultChromiumRevision);

                var executablePath = browserFetcher.GetExecutablePath(BrowserFetcher.DefaultChromiumRevision);

                if (string.IsNullOrEmpty(executablePath))
                {
                    _logger.LogError("Chromium location is empty. Unable to start Chromium. Exiting.");
                    throw new System.Exception("Chromium location is empty. Unable to start Chromium. Exiting.");
                }

                _logger.LogInformation($"Attemping to start Chromium using executable path: {executablePath}");

                var args = new List<string>();
                if (_options.Value.ProxyOptions.Enabled)
                {
                    args.Add($"--proxy-server={_options.Value.ProxyOptions.Address}:{_options.Value.ProxyOptions.Port}");
                }

                var options = new LaunchOptions { Headless = true, ExecutablePath = executablePath, Args = args.ToArray() };

                using (var browser = await Puppeteer.LaunchAsync(options))
                using (var page = await browser.NewPageAsync())
                {
                    if (_options.Value.ProxyOptions.Enabled && _options.Value.ProxyOptions.UserAuthentication)
                    {
                        await page.AuthenticateAsync(new Credentials()
                        {
                            Username = _options.Value.ProxyOptions.Username,
                            Password = _options.Value.ProxyOptions.Password
                        });
                    }

                    await page.GoToAsync(url);

                    await page.AddScriptTagAsync(new AddTagOptions()
                    {
                        Content = IsScrollOnFirstPostJavaScriptMethod
                    });

                    bool results = false;
                    do
                    {
                        results = await page.EvaluateExpressionAsync<bool>(IsScrollOnFirstPostJavaScriptMethodName);
                        await Task.Delay(500);
                    } while (!results);

                    content = await page.GetContentAsync();
                    _logger.LogInformation("Telegram's content fetched successfully.");

                    if (!Directory.Exists(Path.Combine(currentDirectory, "TempData")))
                    {
                        Directory.CreateDirectory(Path.Combine(currentDirectory, "TempData"));
                        _logger.LogInformation("Create a temp folder behind the service.");
                    }
                    await File.WriteAllTextAsync(filePath, content, cancellationToken);
                    _logger.LogInformation("Telegram's cached in a local file successfully.");

                    _fileLastModifiedDateTime = DateTime.Now;

                    return content;
                }
            }
            finally
            {
                _telegramSemaphore.Release();
            }
        }
    }
}
