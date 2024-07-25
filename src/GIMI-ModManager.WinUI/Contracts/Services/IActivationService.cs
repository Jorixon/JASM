namespace GIMI_ModManager.WinUI.Contracts.Services;

public interface IActivationService
{
    Task ActivateAsync(object activationArgs);
}