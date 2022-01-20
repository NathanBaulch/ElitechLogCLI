using ElitechLog.Models;
using ElitechLog.Monitor;

namespace ElitechLogCLI;

public class Monitor
{
    public event Action<Parameters> Connected;
    public event Action<int, int> Downloading;
    public event Action<Parameters> Downloaded;
    public event Action Updated;
    public event Action Disconnected;

    private readonly EventWaitHandle _stop = new ManualResetEvent(false);
    private readonly CCOM _com = CCOM.GetInstance();
    private readonly CUSB _usb = CUSB.GetInstance();
    private readonly object _lockObj = new();

    private Parameters _parms;
    private string _dsn;
    private Exception _ex;

    public Monitor()
    {
        _com.EParametersLoaded += ParametersLoaded;
        _com.EDownloadProgress += DownloadProgress;
        _com.EDownloadComplete += DownloadComplete;
        _com.EDeviceDisconnected += _ => DeviceDisconnected(true);
        _com.ENotify += Notify;
        _usb.EParametersLoaded += ParametersLoaded;
        _usb.EDownloadProgress += DownloadProgress;
        _usb.EDownloadComplete += DownloadComplete;
        _usb.EDeviceDisconnected += _ => DeviceDisconnected(false);
        _usb.ENotify += Notify;
    }

    public void Run()
    {
        _com.Run();
        _usb.Run();
        _stop.WaitOne();
        if (_ex != null) throw _ex;
    }

    public void Stop()
    {
        _stop.Set();
    }

    private void ParametersLoaded(Parameters parms, string dsn)
    {
        lock (_lockObj)
        {
            try
            {
                if (_dsn == dsn)
                {
                    _parms = parms;
                    Updated?.Invoke();
                }
                else if (_dsn == null)
                {
                    _parms = parms;
                    _dsn = dsn;
                    Connected?.Invoke(parms);
                }
            }
            catch (Exception ex)
            {
                _ex = ex;
                _stop.Set();
            }
        }
    }

    public void Download()
    {
        lock (_lockObj)
        {
            switch (_parms)
            {
                case COMParameters parms:
                    _com.Download(parms);
                    return;
                case USBParameters parms:
                    _usb.Download(parms);
                    return;
                default:
                    return;
            }
        }
    }

    public void UpdateParameters()
    {
        lock (_lockObj)
        {
            switch (_parms)
            {
                case COMParameters p:
                    _com.SetParameters(p, 0);
                    return;
                case USBParameters p:
                    _usb.SetParameters(p, 0);
                    return;
                default:
                    return;
            }
        }
    }

    public void QuickReset()
    {
        lock (_lockObj)
        {
            switch (_parms)
            {
                case COMParameters p:
                    _com.SetParameters(p, 1);
                    return;
                case USBParameters p:
                    _usb.SetParameters(p, 1);
                    return;
                default:
                    return;
            }
        }
    }

    private void DownloadProgress(int current, int total, string msg)
    {
        try
        {
            Downloading?.Invoke(current, total);
        }
        catch (Exception ex)
        {
            _ex = ex;
            _stop.Set();
        }
    }

    private void DownloadComplete(Parameters parms)
    {
        try
        {
            Downloaded?.Invoke(parms);
        }
        catch (Exception ex)
        {
            _ex = ex;
            _stop.Set();
        }
    }

    private void DeviceDisconnected(bool com)
    {
        lock (_lockObj)
        {
            try
            {
                if ((com && _parms is COMParameters) || (!com && _parms is USBParameters))
                {
                    _parms = null;
                    _dsn = null;
                    Disconnected?.Invoke();
                }
            }
            catch (Exception ex)
            {
                _ex = ex;
                _stop.Set();
            }
        }
    }

    private void Notify(bool error, string msg)
    {
        if (error)
        {
            _ex = new ApplicationException(msg);
            _stop.Set();
        }
        else
        {
            Console.WriteLine(msg);
        }
    }
}