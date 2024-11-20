using CommunityToolkit.Mvvm.ComponentModel;
using GIMI_ModManager.WinUI.Helpers;
using Microsoft.UI.Xaml;

namespace GIMI_ModManager.WinUI.Views;

public sealed partial class ErrorWindow : WindowEx
{
    public ErrorWindowViewModel ViewModel { get; }


    public ErrorWindow(Exception exception, Action onErrorWindowClose)
    {
        if (App.MainWindow is not null)
        {
            App.MainWindow.Closed += (sender, args) => Close();
        }

        Closed += (sender, args) => onErrorWindowClose();

        InitializeComponent();
        ViewModel = new ErrorWindowViewModel(exception);
    }
}

public partial class ErrorWindowViewModel : ObservableRecipient
{
    public Uri JASM_GITHUB { get; } = Constants.JASM_GITHUB;
    public Uri JASM_GAMEBANANA { get; } = Constants.JASM_GAMEBANANA;

    public ErrorWindowViewModel(Exception exception)
    {
        ExceptionMessage = StripUsername(exception.Message);
        ExceptionStackTrace = StripUsername(exception?.ToString() ?? "No stack trace available");
        HasInnerException = exception?.InnerException != null ? Visibility.Visible : Visibility.Collapsed;
        InnerExceptionMessage = StripUsername(exception?.InnerException?.Message ?? "No inner exception available");
        InnerExceptionStackTrace =
            StripUsername(exception?.InnerException?.ToString() ?? "No inner stack trace available");
    }

    [ObservableProperty] private string _exceptionMessage;
    [ObservableProperty] private string _exceptionStackTrace;

    [ObservableProperty] private Visibility _hasInnerException;
    [ObservableProperty] private string _innerExceptionMessage;
    [ObservableProperty] private string _innerExceptionStackTrace;


    private string StripUsername(string @string)
    {
        var userName = System.Security.Principal.WindowsIdentity.GetCurrent().Name;

        if (userName.Contains("\\"))
        {
            var name = userName.Split('\\').LastOrDefault();

            if (name != null)
                userName = name;
        }


        return @string.Replace(userName, "<username>", StringComparison.CurrentCultureIgnoreCase);
    }
}