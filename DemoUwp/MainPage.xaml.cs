using IO.Ably;
using IO.Ably.Realtime;
using System;
using System.Threading;
using System.Threading.Tasks;
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
            DefaultLogger.LogLevel = LogLevel.Debug;
            var context = SynchronizationContext.Current;
            DefaultLogger.LoggerSink = new CustomLoggerSink(message => context.Post(state => logBox.Items.Add(message), null));
            payloadBox.Text = "{\"handle\":\"Your Name\",\"message\":\"Testing Message\"}";
            Test();

        }


        private AblyRealtime client;
        private IRealtimeChannel channel;

        private async void Test()
        {
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

        private void Handler(IO.Ably.Message message1)
        {
            outputBox.Items.Add($"Message: {message1}");
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
        }

        private void channel_ChannelStateChanged(object sender, ChannelStateChange e)
        {
            outputBox.Items.Add(string.Format("Channel: {0}", e.Current));
        }

        private void Presence_MessageReceived(PresenceMessage message)
        {
            outputBox.Items.Add(string.Format("{0}: {1} {2}", message.Data, message.Action, message.ClientId));
        }
    }
}
