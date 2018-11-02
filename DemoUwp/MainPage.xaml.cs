using IO.Ably;
using IO.Ably.Realtime;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using LogLevel = IO.Ably.LogLevel;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace DemoUwp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public class PresenceData
    {
        public string[] Data { get; set; }
    }
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainPage : Page
    {
        private TcpSocketUwp _client;
        private class CustomLoggerSink : ILoggerSink
        {
            private readonly Action<string> _logEvent;

            public CustomLoggerSink(Action<string> logEvent)
            {
                _logEvent = logEvent;
            }

            public void LogEvent(LogLevel level, string message)
            {
                _logEvent($"{level}:{message}");
            }
        }

        public MainPage()
        {
            InitializeComponent();
            _client = new TcpSocketUwp("logs7.papertrailapp.com", 33860);
            DefaultLogger.LogLevel = LogLevel.Debug;
            var context = SynchronizationContext.Current;
            DefaultLogger.LoggerSink = new CustomLoggerSink(message =>
            {
                context.Post(state =>
                {
                    logBox.Items.Add(message);
                    SendMessage(message);
                }, null);
            });
            payloadBox.Text = "{\"handle\":\"Your Name\",\"message\":\"Testing Message\"}";
            Test();
        }

        private async void SendMessage(string entry)
        {
            if (!await _client.SendAsync(SyslogSerialize(entry)).ConfigureAwait(false))
            {
                //Try to connect and send once more
                await _client.ConnectAsync();
                if (!await _client.SendAsync(SyslogSerialize(entry)))
                {
                    Debug.Write("Lost connection to logging service");
                }
            }
        }
        private AblyRealtime client;
        private IRealtimeChannel channel;

        private async void Test()
        {
            await _client.ConnectAsync();
            await Connect();
            Trigger_Click(null, null);
        }

        private async void Subscribe_Click(object sender, RoutedEventArgs e)
        {
            await Connect();
        }

        private async Task Connect()
        {
            string channelName = this.channelBox.Text.Trim();
            if (string.IsNullOrEmpty(channelName))
            {
                return;
            }

            string key = "lNj80Q.iGyVcQ:2QKX7FFASfX-7H9H";
            string clientId = "Martin";
            var options = new ClientOptions(key) { UseBinaryProtocol = true, Tls = true, AutoConnect = false, ClientId = clientId };
            this.client = new AblyRealtime(options);
            this.client.Connection.ConnectionStateChanged += this.connection_ConnectionStateChanged;
            this.client.Connect();

            this.channel = this.client.Channels.Get(channelName);
            this.channel.StateChanged += channel_ChannelStateChanged;
            this.channel.Subscribe(Handler);
            this.channel.Presence.Subscribe(Presence_MessageReceived);
            try
            {
                await channel.AttachAsync();
                await channel.Presence.EnterAsync(new PresenceData() { Data = new[] { "data1", "data2" } });
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }

        }
        #region Syslog

        private enum Severity
        {
            Emergency = 0,
            Alert = 1,
            Critical = 2,
            Error = 3,
            Warning = 4,
            Notice = 5,
            Informational = 6,
            Debug = 7
        }

        private enum Facility
        {
            KernelMessages = 0,
            UserLevelMessages = 1,
            MailSystem = 2,
            SystemDaemons = 3,
            SecurityOrAuthorizationMessages1 = 4,
            InternalMessages = 5,
            LinePrinterSubsystem = 6,
            NetworkNewsSubsystem = 7,
            UUCPSubsystem = 8,
            ClockDaemon1 = 9,
            SecurityOrAuthorizationMessages2 = 10,
            FTPDaemon = 11,
            NTPSubsystem = 12,
            LogAudit = 13,
            LogAlert = 14,
            ClockDaemon2 = 15,
            LocalUse0 = 16,
            LocalUse1 = 17,
            LocalUse2 = 18,
            LocalUse3 = 19,
            LocalUse4 = 20,
            LocalUse5 = 21,
            LocalUse6 = 22,
            LocalUse7 = 23
        }

        private const int Priority = (int)Facility.UserLevelMessages * 8 + (int)Severity.Informational;

        private static string SyslogSerialize(string msg)
        {
            return $"<{Priority}>{DateTime.UtcNow:s} {msg}";
        }

        #endregion

        public class TcpSocketUwp
        {
            private readonly HostName _hostName;
            private readonly int _port;

            private StreamSocket _tcpSocket;
            private DataWriter _writer;

            public TcpSocketUwp(string hostname, int port)
            {
                _hostName = new HostName(hostname);
                _port = port;
            }

            public async Task ConnectAsync()
            {
                Dispose();

                try
                {
                    _tcpSocket = new StreamSocket();
                    await _tcpSocket.ConnectAsync(_hostName, _port.ToString(), SocketProtectionLevel.Tls12);
                    _writer = new DataWriter(_tcpSocket.OutputStream)
                    {
                        UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8
                    };
                }
                catch (Exception e)
                {
                    // If this is an unknown status it means that the error is fatal and retry will likely fail.
                    if (SocketError.GetStatus(e.HResult) == SocketErrorStatus.Unknown)
                    {
                        Dispose();
                        throw;
                    }

                    // If the exception was caused by an SSL error that is ignorable we are going to prompt the user
                    // with an enumeration of the errors and ask for permission to ignore.
                    if (_tcpSocket.Information.ServerCertificateErrorSeverity != SocketSslErrorSeverity.Ignorable)
                    {
                        Debug.WriteLine("SSL certificate error while connecting to server");
                    }
                    else
                    {
                        Debug.WriteLine("SSL error (ignorable) while connecting to server");
                    }
                }
            }

            public async Task<bool> SendAsync(string msg)
            {
                if (_writer == null)
                {
                    return false;
                }

                try
                {
                    _writer.WriteString(msg);
                    _writer.WriteString(Environment.NewLine);
                    await _writer.StoreAsync();
                    return true;
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"{nameof(TcpSocketUwp)}: Exception while SendAsync.\n{e.Message}");
                    return false;
                }
            }

            public void Dispose()
            {
                _writer?.Dispose();
                _writer = null;
                _tcpSocket?.Dispose();
                _tcpSocket = null;
            }
        }

        private void Handler(IO.Ably.Message message1)
        {
            outputBox.Items.Add($"Message: {message1}");
            SendMessage(message1.Data.ToString());
        }

        private void Trigger_Click(object sender, RoutedEventArgs e)
        {
            string eventName = this.eventBox.Text.Trim();
            string payload = this.payloadBox.Text.Trim();

            if (this.channel == null || string.IsNullOrEmpty(eventName))
            {
                return;
            }

            channel.PublishAsync(eventName, payload);
        }

        private void connection_ConnectionStateChanged(object sender, ConnectionStateChange e)
        {
            outputBox.Items.Add(string.Format("Connection: {0}", e.Current));
            SendMessage(string.Format("Connection: {0}", e.Current));
        }

        private void channel_ChannelStateChanged(object sender, ChannelStateChange e)
        {
            outputBox.Items.Add(string.Format("Channel: {0}", e.Current));
            SendMessage(string.Format("Channel: {0}", e.Current));
        }

        private void Presence_MessageReceived(PresenceMessage message)
        {
            outputBox.Items.Add(string.Format("{0}: {1} {2}", message.Data, message.Action, message.ClientId));
            SendMessage(string.Format("{0}: {1} {2}", message.Data, message.Action, message.ClientId));
        }
    }
}
