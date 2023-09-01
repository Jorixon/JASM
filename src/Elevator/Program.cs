using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using WindowsInput;


// Exit codes:
// 0: Success
// 1: Unhandled exception
// 2: Bad arguments

// Commands:
// -2: Alive check
// -1: Exit
// 0: RefreshActiveGenshinMods

internal class Program
{
    public static void Main(string[] args)
    {
        var userName = "";
        try
        {
            userName = args.First();
        }
        catch
        {
            Console.Error.WriteLine("Please provide a username");
            Environment.Exit(2);
        }

        try
        {
            StartPipeServer(userName);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            Environment.Exit(1);
        }
    }


    static void StartPipeServer(string userName)
    {
        var specificUserAccount = new NTAccount(userName);
        var specificUserSid = (SecurityIdentifier)specificUserAccount.Translate(typeof(SecurityIdentifier));

        var ps = new PipeSecurity();

        var userAccessRule = new PipeAccessRule(specificUserSid,
            PipeAccessRights.FullControl, AccessControlType.Allow);
        ps.AddAccessRule(userAccessRule);

        while (true)
        {
            using var pipeServer = NamedPipeServerStreamConstructors.New("MyPipess", PipeDirection.In, 1,
                PipeTransmissionMode.Message,
                PipeOptions.Asynchronous, pipeSecurity: ps);
            Console.WriteLine("Waiting for connection...");

            pipeServer.WaitForConnection();
            Console.WriteLine("Connected!");
            Console.WriteLine("----------------------");


            using var reader = new StreamReader(pipeServer);
            var command = reader.ReadLine();
            Console.WriteLine("Received command: " + command);
            Console.WriteLine("From user: " + pipeServer.GetImpersonationUserName());

            switch (command)
            {
                case "-2":
                    break;
                case "-1":
                    Console.WriteLine("Exiting");
                    Environment.Exit(0);
                    return;
                case "0":
                    Console.WriteLine("Refreshing Genshin Mods");
                    RefreshGenshinMods();
                    break;

                default:
                    Console.Error.WriteLine($"Unknown command: {command}");
                    break;
            }
        }
    }

    [DllImport("User32.dll")]
    static extern int SetForegroundWindow(IntPtr point);


    static void RefreshGenshinMods()
    {
        var ptr = GetGenshinProcess();

        if (ptr == null) return;


        _ = SetForegroundWindow(ptr.Value);

        new InputSimulator().Keyboard
            .KeyDown(VirtualKeyCode.F10)
            .Sleep(100)
            .KeyUp(VirtualKeyCode.F10)
            .Sleep(100);
    }


    static IntPtr? GetGenshinProcess()
    {
        var processes = Process.GetProcessesByName("GenshinImpact");

        foreach (var process in processes)
        {
            Console.WriteLine("Title: " + process.MainWindowTitle);
        }

        if (processes.Length > 1)
        {
            Console.Error.WriteLine("Multiple GenshinImpact.exe processes found");
            return null;
        }

        var ptr = processes.FirstOrDefault()?.MainWindowHandle;
        if (ptr == IntPtr.Zero)
        {
            Console.Error.WriteLine("GenshinImpact.exe process not found");
            return null;
        }

        return ptr;
    }
}

/*[DllImport("user32.dll")]
static extern bool PostMessage(IntPtr hWnd, UInt32 Msg, int wParam, int lParam);

const UInt32 WM_KEYDOWN = 0x0100;
const int VK_F10 = 0x79;

async Task RefreshGenshinMods()
{
    var ptr = GetGenshinProcess().MainWindowHandle;


    SetForegroundWindow(ptr);
    await Task.Delay(100);

    var success = PostMessage(ptr, WM_KEYDOWN, VK_F10, 0);

    Console.WriteLine(!success ? "Failed to send message" : "Sent message");
}*/

/*async Task RefreshGenshinModsWinInput()
{
    var ptr = GetGenshinProcess().MainWindowHandle;

    SetForegroundWindow(ptr);
    await Task.Delay(1000);

    await WindowsInput.Simulate.Events()
        .Click(KeyCode.F10)
        .Invoke();
}*/