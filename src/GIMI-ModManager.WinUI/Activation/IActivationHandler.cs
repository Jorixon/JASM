namespace GIMI_ModManager.WinUI.Activation;

public interface IActivationHandler
{
    string ActivationName { get; }
    bool CanHandle(object args);

    Task HandleAsync(object args);
}
