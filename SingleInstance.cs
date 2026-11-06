using System.IO;
using System.IO.Pipes;
using System.Security.Principal;

namespace Radiomat;

internal sealed class SingleInstance : IDisposable
{
    private readonly string _name = EditionConfiguration.Current.InstanceName + "-" + WindowsIdentity.GetCurrent().User!.Value
        + "-" + System.Diagnostics.Process.GetCurrentProcess().SessionId;
    private readonly CancellationTokenSource _stop = new();
    private readonly Mutex _mutex;
    private bool _ownsMutex;

    public SingleInstance() => _mutex = new Mutex(false, @"Local\" + _name);

    public bool TryAcquire()
    {
        try { _ownsMutex = _mutex.WaitOne(0); }
        catch (AbandonedMutexException) { _ownsMutex = true; }
        return _ownsMutex;
    }

    public void ShowExisting()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _name, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            client.Connect(3000);
            client.WriteByte(1);
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException) { }
    }

    public void Listen(Action showWindow) => _ = Task.Run(async () =>
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(_name, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(_stop.Token);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                deadline.CancelAfter(TimeSpan.FromSeconds(2));
                var command = new byte[1];
                if (await pipe.ReadAsync(command, deadline.Token) == 1 && command[0] == 1) showWindow();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                try { await Task.Delay(250, _stop.Token); } catch (OperationCanceledException) { }
            }
        }
    });

    public void Dispose()
    {
        _stop.Cancel();
        if (_ownsMutex) _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
