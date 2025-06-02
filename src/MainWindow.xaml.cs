using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Controls;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Newtonsoft.Json;
using System.Net.Http;
using System.Threading.Tasks;
using Ionic.Zip;
using LauncherConfig;

namespace CanaryLauncherUpdate
{
	public partial class MainWindow : Window
	{
		static string launcerConfigUrl = "https://raw.githubusercontent.com/mdbeng/GloryLauncher/main/launcher_config.json";
		// Load informations of launcher_config.json file
		static ClientConfig clientConfig = ClientConfig.loadFromFile(launcerConfigUrl);

		static string clientExecutableName = clientConfig.clientExecutable;
		static string urlClient = clientConfig.newClientUrl;
		static string programVersion = clientConfig.launcherVersion;

		string newVersion = "";
		bool clientDownloaded = false;
		bool needUpdate = false;

		static readonly HttpClient httpClient = new HttpClient();
		WebClient webClient = new WebClient();

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

		public MainWindow()
		{
			InitializeComponent();
		}

		private void UpdateButtonToPlayState()
		{
			buttonPlay.Background = new LinearGradientBrush(
				new GradientStopCollection
				{
					new GradientStop(Color.FromRgb(76, 175, 80), 0),
					new GradientStop(Color.FromRgb(46, 125, 50), 1)
				},
				new Point(0, 0),
				new Point(0, 1)
			);
			buttonPlayIcon.Source = new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/icon_play.png"));
			
			// Update the text in the button
			var stackPanel = buttonPlay.Content as StackPanel;
			if (stackPanel != null)
			{
				var textBlock = stackPanel.Children.OfType<TextBlock>().FirstOrDefault();
				if (textBlock != null)
				{
					textBlock.Text = "PLAY";
				}
			}
		}

		private void UpdateButtonToUpdateState()
		{
			buttonPlay.Background = new LinearGradientBrush(
				new GradientStopCollection
				{
					new GradientStop(Color.FromRgb(255, 152, 0), 0),
					new GradientStop(Color.FromRgb(255, 87, 34), 1)
				},
				new Point(0, 0),
				new Point(0, 1)
			);
			buttonPlayIcon.Source = new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/icon_update.png"));
			
			// Update the text in the button
			var stackPanel = buttonPlay.Content as StackPanel;
			if (stackPanel != null)
			{
				var textBlock = stackPanel.Children.OfType<TextBlock>().FirstOrDefault();
				if (textBlock != null)
				{
					textBlock.Text = "UPDATE";
				}
			}
		}

		static void CreateShortcut()
		{
			string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
			string shortcutPath = Path.Combine(desktopPath, clientConfig.clientFolder + ".lnk");
			Type t = Type.GetTypeFromProgID("WScript.Shell");
			dynamic shell = Activator.CreateInstance(t);
			var lnk = shell.CreateShortcut(shortcutPath);
			try
			{
				lnk.TargetPath = Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe");
				lnk.Description = clientConfig.clientFolder;
				lnk.Save();
			}
			finally
			{
				System.Runtime.InteropServices.Marshal.FinalReleaseComObject(lnk);
			}
		}

		private void TibiaLauncher_Load(object sender, RoutedEventArgs e)
		{
			ImageLogoServer.Source = new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/logo.png"));
			ImageLogoCompany.Source = new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/logo_company.png"));

			newVersion = clientConfig.clientVersion;
			progressbarDownload.Visibility = Visibility.Collapsed;
			labelClientVersion.Visibility = Visibility.Collapsed;
			labelDownloadPercent.Visibility = Visibility.Collapsed;

			if (File.Exists(GetLauncherPath(true) + "/launcher_config.json"))
			{
				// Read actual client version
				string actualVersion = GetClientVersion(GetLauncherPath(true));
				labelVersion.Text = "v" + programVersion;

				if (newVersion != actualVersion)
				{
					// Update button to show update state
					UpdateButtonToUpdateState();
					labelClientVersion.Text = newVersion;
					labelClientVersion.Visibility = Visibility.Visible;
					buttonPlay.Visibility = Visibility.Visible;
					buttonPlay_tooltip.Text = "Update";
					needUpdate = true;
				}
			}
			if (!File.Exists(GetLauncherPath(true) + "/launcher_config.json") || Directory.Exists(GetLauncherPath()) && Directory.GetFiles(GetLauncherPath()).Length == 0 && Directory.GetDirectories(GetLauncherPath()).Length == 0)
			{
				labelVersion.Text = "v" + programVersion;
				// Update button to show download state
				UpdateButtonToUpdateState();
				labelClientVersion.Text = "Download";
				labelClientVersion.Visibility = Visibility.Visible;
				buttonPlay.Visibility = Visibility.Visible;
				buttonPlay_tooltip.Text = "Download";
				needUpdate = true;
			}
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

		private void AddReadOnly()
		{
			// If the files "eventschedule/boostedcreature/onlinenumbers" exist, set them as read-only
			string eventSchedulePath = GetLauncherPath() + "/cache/eventschedule.json";
			if (File.Exists(eventSchedulePath)) {
				File.SetAttributes(eventSchedulePath, FileAttributes.ReadOnly);
			}
			string boostedCreaturePath = GetLauncherPath() + "/cache/boostedcreature.json";
			if (File.Exists(boostedCreaturePath)) {
				File.SetAttributes(boostedCreaturePath, FileAttributes.ReadOnly);
			}
			string onlineNumbersPath = GetLauncherPath() + "/cache/onlinenumbers.json";
			if (File.Exists(onlineNumbersPath)) {
				File.SetAttributes(onlineNumbersPath, FileAttributes.ReadOnly);
			}
		}

		private void UpdateClient()
		{
			if (!Directory.Exists(GetLauncherPath(true)))
			{
				Directory.CreateDirectory(GetLauncherPath());
			}
			labelDownloadPercent.Visibility = Visibility.Visible;
			progressbarDownload.Visibility = Visibility.Visible;
			labelClientVersion.Visibility = Visibility.Collapsed;
			buttonPlay.Visibility = Visibility.Collapsed;
			webClient.DownloadProgressChanged += Client_DownloadProgressChanged;
			webClient.DownloadFileCompleted += Client_DownloadFileCompleted;
			webClient.DownloadFileAsync(new Uri(urlClient), GetLauncherPath() + "/tibia.zip");
		}

		private void buttonPlay_Click(object sender, RoutedEventArgs e)
		{
			if (needUpdate == true || !Directory.Exists(GetLauncherPath()))
			{
				try
				{
					UpdateClient();
				}
				catch (Exception ex)
				{
					labelVersion.Text = ex.ToString();
				}
			}
			else
			{
				if (clientDownloaded == true || !Directory.Exists(GetLauncherPath(true)))
				{
					Process.Start(GetLauncherPath() + "/bin/" + clientExecutableName);
					this.Close();
				}
				else
				{
					try
					{
						UpdateClient();
					}
					catch (Exception ex)
					{
						labelVersion.Text = ex.ToString();
					}
				}
			}
		}

		private void ExtractZip(string path, ExtractExistingFileAction existingFileAction)
		{
			using (ZipFile modZip = ZipFile.Read(path))
			{
				foreach (ZipEntry zipEntry in modZip)
				{
					zipEntry.Extract(GetLauncherPath(), existingFileAction);
				}
			}
		}

		private async void Client_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
		{
			// Update button to show play state
			UpdateButtonToPlayState();

			if (clientConfig.replaceFolders)
			{
				foreach (ReplaceFolderName folderName in clientConfig.replaceFolderName)
				{
					string folderPath = Path.Combine(GetLauncherPath(), folderName.name);
					if (Directory.Exists(folderPath))
					{
						Directory.Delete(folderPath, true);
					}
				}
			}

			// Adds the task to a secondary task to prevent the program from crashing while this is running
			await Task.Run(() =>
			{
				Directory.CreateDirectory(GetLauncherPath());
				ExtractZip(GetLauncherPath() + "/tibia.zip", ExtractExistingFileAction.OverwriteSilently);
				File.Delete(GetLauncherPath() + "/tibia.zip");
			});
			progressbarDownload.Value = 100;

			// Download launcher_config.json from url to the launcher path
			WebClient webClient = new WebClient();
			string localPath = Path.Combine(GetLauncherPath(true), "launcher_config.json");
			webClient.DownloadFile(launcerConfigUrl, localPath);

			AddReadOnly();
			CreateShortcut();

			needUpdate = false;
			clientDownloaded = true;
			labelClientVersion.Text = GetClientVersion(GetLauncherPath(true));
			buttonPlay_tooltip.Text = GetClientVersion(GetLauncherPath(true));
			labelClientVersion.Visibility = Visibility.Visible;
			buttonPlay.Visibility = Visibility.Visible;
			progressbarDownload.Visibility = Visibility.Collapsed;
			labelDownloadPercent.Visibility = Visibility.Collapsed;
		}

		private void Client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
		{
			progressbarDownload.Value = e.ProgressPercentage;
			if (progressbarDownload.Value == 100) {
				labelDownloadPercent.Text = "Finishing, wait...";
			} else {
				labelDownloadPercent.Text = SizeSuffix(e.BytesReceived) + " / " + SizeSuffix(e.TotalBytesToReceive);
			}
		}

		static readonly string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
		static string SizeSuffix(Int64 value, int decimalPlaces = 1)
		{
			if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException("decimalPlaces"); }
			if (value < 0) { return "-" + SizeSuffix(-value, decimalPlaces); }
			if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }

			int mag = (int)Math.Log(value, 1024);
			decimal adjustedSize = (decimal)value / (1L << (mag * 10));

			if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
			{
				mag += 1;
				adjustedSize /= 1024;
			}
			return string.Format("{0:n" + decimalPlaces + "} {1}",
				adjustedSize,
				SizeSuffixes[mag]);
		}

		private void buttonPlay_MouseEnter(object sender, MouseEventArgs e)
		{
			// The hover effects are now handled by the XAML animations
			// We can add additional logic here if needed
		}

		private void buttonPlay_MouseLeave(object sender, MouseEventArgs e)
		{
			// The hover effects are now handled by the XAML animations
			// We can add additional logic here if needed
		}

		private void CloseButton_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private void RestoreButton_Click(object sender, RoutedEventArgs e)
		{
			if (ResizeMode != ResizeMode.NoResize)
			{
				if (WindowState == WindowState.Normal)
					WindowState = WindowState.Maximized;
				else
					WindowState = WindowState.Normal;
			}
		}

		private void MinimizeButton_Click(object sender, RoutedEventArgs e)
		{
			WindowState = WindowState.Minimized;
		}

	}
}