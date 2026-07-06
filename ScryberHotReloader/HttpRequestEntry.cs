using System.ComponentModel;

namespace ScryberHotReloader;

public sealed class HttpRequestEntry : INotifyPropertyChanged {
    private bool _enabled = true;
    private string _name = "response";
    private string _method = "GET";
    private string _url = "";
    private string _headers = "";
    private string _body = "";

    public bool Enabled {
        get => _enabled; set {
            _enabled = value;
            Notify();
        }
    }
    public string Name {
        get => _name; set {
            _name = value;
            Notify();
        }
    }
    public string Method {
        get => _method; set {
            _method = value;
            Notify();
        }
    }
    public string Url {
        get => _url; set {
            _url = value;
            Notify();
        }
    }
    public string Headers {
        get => _headers; set {
            _headers = value;
            Notify();
        }
    }
    public string Body {
        get => _body; set {
            _body = value;
            Notify();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([System.Runtime.CompilerServices.CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}