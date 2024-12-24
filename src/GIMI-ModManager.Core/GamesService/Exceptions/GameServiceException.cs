namespace GIMI_ModManager.Core.GamesService.Exceptions;

public class GameServiceException : Exception
{
    public GameServiceException()
    {
    }

    public GameServiceException(string message) : base(message)
    {
    }

    public GameServiceException(string message, Exception? innerException) : base(message, innerException)
    {
    }
}