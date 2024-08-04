namespace GIMI_ModManager.Core.Services.GameBanana;

public class InvalidGameBananaUrlException : Exception
{
    public InvalidGameBananaUrlException(Uri? url) : base($"Invalid GameBanana url: {url}")
    {
    }

    public InvalidGameBananaUrlException() : base("Invalid GameBanana url.")
    {
    }

    public InvalidGameBananaUrlException(string message) : base(message)
    {
    }
}