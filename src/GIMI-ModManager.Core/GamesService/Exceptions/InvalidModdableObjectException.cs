namespace GIMI_ModManager.Core.GamesService.Exceptions;

public class InvalidModdableObjectException : GameServiceException
{
    public InvalidModdableObjectException(string message) : base(message)
    {
    }

    public InvalidModdableObjectException(string message, Exception innerException) : base(message, innerException)
    {
    }
}