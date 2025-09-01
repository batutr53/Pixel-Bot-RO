using PixelAutomation.Core.Interfaces;

namespace PixelAutomation.Capture.Win.Providers;

public class ClickProviderFactory
{
    public static IClickProvider Create(ClickMode mode, bool enableFallback = true)
    {
        IClickProvider provider = mode switch
        {
            ClickMode.CursorJump => new SendInputClickProvider(false),
            ClickMode.CursorReturn => new SendInputClickProvider(true),
            _ => new MessageClickProvider()
        };

        if (enableFallback && provider is MessageClickProvider messageProvider)
        {
            messageProvider.SetFallbackProvider(new SendInputClickProvider(true));
        }

        return provider;
    }

    public static IClickProvider CreateWithFallback(string modeString)
    {
        var mode = modeString.ToLowerInvariant() switch
        {
            "cursor-jump" => ClickMode.CursorJump,
            "cursor-return" => ClickMode.CursorReturn,
            _ => ClickMode.Message
        };

        return Create(mode, true);
    }
}