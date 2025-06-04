using System;
using System.Windows;
using System.IO;
using System.Net;
using System.Windows.Threading;
using System.Net.Http;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.IO.Compression;
using System.Diagnostics;
using System.Threading.Tasks;
using Newtonsoft.Json;
using LauncherConfig;

namespace CanaryLauncherUpdate
{
	public partial class SplashScreen : Window
	{
		static string launcerConfigUrl = "https://raw.githubusercontent.com/mdbeng/GloryLauncher/main/launcher_config.json";
		// Load informations of launcher_config.json file
		static ClientConfig clientConfig = ClientConfig.loadFromFile(launcerConfigUrl);

		static string clientExecutableName = clientConfig.clientExecutable;
		static string urlClient = clientConfig.newClientUrl;

		static readonly HttpClient httpClient = new HttpClient();
		DispatcherTimer timer = new DispatcherTimer();

		private string GetLauncherPath(bool onlyBaseDirectory = false)
		{
			string launcherPath = "";
			if (string.IsNullOrEmpty(clientConfig.clientFolder) || onlyBaseDirectory) {
				launcherPath = AppDomain.CurrentDomain.BaseDirectory.ToString();
			} else {
				launcherPath = AppDomain.CurrentDomain.BaseDirectory.ToString() + "/" + clientConfig.clientFolder;
			}

			return launcherPath;
		}

		static string GetClientVersion(string path)
		{
			string json = path + "/launcher_config.json";
			StreamReader stream = new StreamReader(json);
			dynamic jsonString = stream.ReadToEnd();
			dynamic versionclient = JsonConvert.DeserializeObject(jsonString);
			foreach (string version in versionclient)
			{
				return version;
			}

			return "";
		}

		private void StartClient()
		{
			// Instead of directly starting the client and closing the launcher,
			// we'll show the main launcher window to ensure update checks happen
			MainWindow mainWindow = new MainWindow();
			mainWindow.Show();
			this.Close();
		}

		public SplashScreen()
		{
			string newVersion = clientConfig.clientVersion;
			if (newVersion == null)
			{
				this.Close();
			}

			// Always show the launcher instead of directly starting the client
			// This ensures users can see update notifications and launcher features

			InitializeComponent();
			timer.Tick += new EventHandler(timer_SplashScreen);
			timer.Interval = new TimeSpan(0, 0, 5);
			timer.Start();
		}

		public async void timer_SplashScreen(object sender, EventArgs e)
		{
			try
			{
				// Check if the client URL is available
				var requestClient = new HttpRequestMessage(HttpMethod.Head, urlClient);
				var response = await httpClient.SendAsync(requestClient);
				
				// Create client directory if it doesn't exist
				if (!Directory.Exists(GetLauncherPath()))
				{
					Directory.CreateDirectory(GetLauncherPath());
				}
				
				// Always show the main launcher window
				MainWindow mainWindow = new MainWindow();
				mainWindow.Show();
				
				// Close the splash screen
				this.Close();
				timer.Stop();
			}
			catch (Exception ex)
			{
				// If there's an error, still try to show the main window
				MainWindow mainWindow = new MainWindow();
				mainWindow.Show();
				this.Close();
				timer.Stop();
			}
		}
	}
}