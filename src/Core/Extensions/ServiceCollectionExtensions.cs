using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PixelAutomation.Core.Implementations;
using PixelAutomation.Core.Interfaces;
using PixelAutomation.Core.Services;

namespace PixelAutomation.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCaptchaServices(this IServiceCollection services)
    {
        // Register captcha solver
        services.AddSingleton<ICaptchaSolver, TesseractCaptchaSolver>();
        
        // Register captcha detector
        services.AddSingleton<ICaptchaDetector, ColorBasedCaptchaDetector>();
        
        // Register captcha service
        services.AddScoped<CaptchaService>();
        
        // Register click provider with text support
        services.AddSingleton<IClickProvider, WindowsMessageClickProvider>();
        
        return services;
    }
    
    public static IServiceCollection AddCaptchaServices(this IServiceCollection services, 
        Action<CaptchaServiceOptions> configureOptions)
    {
        services.AddCaptchaServices();
        // Note: Microsoft.Extensions.Options needed for Configure method
        // services.Configure(configureOptions);
        return services;
    }
}

public class CaptchaServiceOptions
{
    public string TesseractPath { get; set; } = "";
    public bool EnableLogging { get; set; } = true;
    public int DefaultTimeout { get; set; } = 30000;
    public CaptchaOptions DefaultProcessingOptions { get; set; } = new();
}