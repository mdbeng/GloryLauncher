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
using System.Windows.Documents;
using System.Windows.Threading;

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
		private List<NewsItem> currentNewsItems = new List<NewsItem>();
		private int currentNewsIndex = 0;
		private BoostedCreature currentBoostedCreature;
		private BoostedCreature currentBoostedBoss;
		private List<CountdownEvent> currentCountdowns = new List<CountdownEvent>();
		private DispatcherTimer countdownTimer;

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

		private async void TibiaLauncher_Load(object sender, RoutedEventArgs e)
		{
			ImageLogoServer.Source = new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/logo.png"));
			ImageLogoCompany.Source = new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/logo_company.png"));

			newVersion = clientConfig.clientVersion;
			progressbarDownload.Visibility = Visibility.Collapsed;
			labelClientVersion.Visibility = Visibility.Collapsed;
			labelDownloadPercent.Visibility = Visibility.Collapsed;

			// Load news, boosted creatures, and countdowns asynchronously
			await LoadNewsAsync();
			await LoadBoostedCreaturesAsync();
			await LoadCountdownsAsync();
			
			// Start the countdown timer to update every second
			StartCountdownTimer();
			
			// The countdown section is already positioned in XAML
			// You can still use RepositionCountdowns() if you need to change it programmatically

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
			// Stop the countdown timer when closing
			if (countdownTimer != null)
			{
				countdownTimer.Stop();
			}
			
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

		private async void HintsBox_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
		{
			// Right-click to refresh news
			await LoadNewsAsync();
		}

		private async void HintsBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			// Check if we have news items
			if (currentNewsItems != null && currentNewsItems.Count > 0)
			{
				try
				{
					// Open the current news item
					await OpenNewsUrl(currentNewsItems[currentNewsIndex].Url);
					
					// Move to next news item for next click
					currentNewsIndex = (currentNewsIndex + 1) % currentNewsItems.Count;
					
					// Update display to show which news will be opened next
					UpdateNewsDisplay();
				}
				catch (Exception)
				{
					// If opening URL fails, refresh news instead
					await LoadNewsAsync();
				}
			}
			else
			{
				// If no news items, refresh news
				await LoadNewsAsync();
			}
		}

		private void UpdateNewsDisplay()
		{
			if (currentNewsItems != null && currentNewsItems.Count > 0)
			{
				// Update the display to highlight which news will be opened next
				string formattedNews = NewsService.FormatNewsForDisplayWithHighlight(currentNewsItems, currentNewsIndex);
				Dispatcher.Invoke(() =>
				{
					hintsBox.Text = formattedNews;
				});
			}
		}

		private async Task OpenNewsUrl(string url)
		{
			try
			{
				if (!url.StartsWith("http"))
				{
					url = "https://gloryot.com/" + url.TrimStart('?');
				}
				
				// Open the URL in the default browser
				Process.Start(new ProcessStartInfo
				{
					FileName = url,
					UseShellExecute = true
				});
			}
			catch (Exception)
			{
				// If opening URL fails, refresh news instead
				await LoadNewsAsync();
			}
		}

		private async Task LoadNewsAsync()
		{
			try
			{
				// Show loading message
				hintsBox.Text = "Loading news...";

				// Fetch news from the website
				var newsItems = await NewsService.FetchNewsAsync();
				
				// Store the news items for click handling
				currentNewsItems = newsItems;
				currentNewsIndex = 0; // Reset to first news item
				
				// Format and display the news with highlight
				string formattedNews = NewsService.FormatNewsForDisplayWithHighlight(newsItems, currentNewsIndex);
				
				// Update the UI on the main thread
				Dispatcher.Invoke(() =>
				{
					hintsBox.Text = formattedNews;
				});
			}
			catch (Exception)
			{
				// Fallback to default content if news loading fails
				currentNewsItems = new List<NewsItem>();
				currentNewsIndex = 0;
				Dispatcher.Invoke(() =>
				{
					hintsBox.Text = GetDefaultNewsContent();
				});
			}
		}

		private string GetDefaultNewsContent()
		{
			return "🏆 BIENVENIDOS A GLORYOT!\n\n" +
				   "🎮 Nuevas Características:\n" +
				   "• Sistema de Battle Royale mejorado\n" +
				   "• Duelos 1 vs 1 con ranking\n" +
				   "• Nuevas zonas de PvP y eventos\n" +
				   "• Sistema de guilds renovado\n\n" +
				   "⚡ Actualizaciones Recientes:\n" +
				   "• Balance de clases mejorado\n" +
				   "• Nuevos items y equipamiento épico\n" +
				   "• Optimización de rendimiento\n" +
				   "• Corrección de bugs críticos\n\n" +
				   "⚠️ Importante:\n" +
				   "GloryOT puede ser peligroso. ¡Mantente alerta!\n\n" +
				   "📅 Próximos Eventos:\n" +
				   "• Torneo de guilds este fin de semana\n" +
				   "• Evento de experiencia doble\n" +
				   "• Nueva quest épica disponible\n\n" +
				   "Servidor en constante desarrollo y mejora.";
		}

		private async Task LoadBoostedCreaturesAsync(bool forceRefresh = false)
		{
			try
			{
				// Fetch boosted creatures from the website
				var (creature, boss) = await BoostedCreatureService.FetchBoostedCreaturesAsync(forceRefresh);
				
				// Store the boosted creatures
				currentBoostedCreature = creature;
				currentBoostedBoss = boss;
				
				// Update the UI on the main thread
				Dispatcher.Invoke(() =>
				{
					UpdateBoostedCreaturesDisplay();
				});
			}
			catch (Exception)
			{
				// Use fallback data if loading fails
				Dispatcher.Invoke(() =>
				{
					LoadFallbackBoostedCreatures();
				});
			}
		}

		private void UpdateBoostedCreaturesDisplay()
		{
			try
			{
				if (currentBoostedCreature != null)
				{
					BoostedCreatureName.Text = currentBoostedCreature.Name;
					LoadImageAsync(BoostedCreatureImage, currentBoostedCreature.ImageUrl);
				}

				if (currentBoostedBoss != null)
				{
					BoostedBossName.Text = currentBoostedBoss.Name;
					LoadImageAsync(BoostedBossImage, currentBoostedBoss.ImageUrl);
				}
			}
			catch (Exception)
			{
				LoadFallbackBoostedCreatures();
			}
		}

		private void LoadFallbackBoostedCreatures()
		{
			BoostedCreatureName.Text = "Loading...";
			BoostedBossName.Text = "Loading...";
			
			// Clear images when loading
			BoostedCreatureImage.Source = null;
			BoostedBossImage.Source = null;
		}

		private async void LoadImageAsync(Image imageControl, string imageUrl)
		{
			// If no URL provided, clear the image
			if (string.IsNullOrEmpty(imageUrl))
			{
				Dispatcher.Invoke(() =>
				{
					imageControl.Source = null;
				});
				return;
			}

			try
			{
				using (var client = new HttpClient())
				{
					client.DefaultRequestHeaders.Add("User-Agent", 
						"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
					
					var imageBytes = await client.GetByteArrayAsync(imageUrl);
					
					Dispatcher.Invoke(() =>
					{
						var bitmap = new BitmapImage();
						bitmap.BeginInit();
						bitmap.StreamSource = new MemoryStream(imageBytes);
						bitmap.CacheOption = BitmapCacheOption.OnLoad;
						bitmap.EndInit();
						bitmap.Freeze();
						
						imageControl.Source = bitmap;
					});
				}
			}
			catch (Exception)
			{
				// If image loading fails, we'll just leave it empty or use a placeholder
				Dispatcher.Invoke(() =>
				{
					imageControl.Source = null;
				});
			}
		}

		private async void BoostedCreaturesPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			// Force refresh boosted creatures when clicked
			await LoadBoostedCreaturesAsync(forceRefresh: true);
		}
		
		private async void CountdownsPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			// Force refresh countdowns when clicked
			await LoadCountdownsAsync(forceRefresh: true);
		}
		
		private async Task LoadCountdownsAsync(bool forceRefresh = false)
		{
			try
			{
				// Show loading state
				Dispatcher.Invoke(() =>
				{
					FirstCountdownName.Text = "Loading...";
					FirstCountdownTime.Text = "--:--:--";
					SecondCountdownName.Text = "Loading...";
					SecondCountdownTime.Text = "--:--:--";
				});
				
				// Fetch countdowns from the website
				var countdowns = await CountdownService.FetchCountdownsAsync(forceRefresh);
				
				// Store the countdowns
				currentCountdowns = countdowns;
				
				// Update the UI on the main thread
				Dispatcher.Invoke(() =>
				{
					UpdateCountdownsDisplay();
				});
			}
			catch (Exception)
			{
				// Use fallback data if loading fails
				Dispatcher.Invoke(() =>
				{
					LoadFallbackCountdowns();
				});
			}
		}
		
		private void UpdateCountdownsDisplay()
		{
			try
			{
				// Hide containers if no countdowns
				if (currentCountdowns == null || currentCountdowns.Count == 0)
				{
					FirstCountdownContainer.Visibility = Visibility.Collapsed;
					SecondCountdownContainer.Visibility = Visibility.Collapsed;
					return;
				}
				
				// First countdown
				if (currentCountdowns.Count > 0)
				{
					FirstCountdownContainer.Visibility = Visibility.Visible;
					FirstCountdownName.Text = currentCountdowns[0].Name;
					FirstCountdownTime.Text = currentCountdowns[0].GetFormattedRemainingTime();
				}
				else
				{
					FirstCountdownContainer.Visibility = Visibility.Collapsed;
				}
				
				// Second countdown
				if (currentCountdowns.Count > 1)
				{
					SecondCountdownContainer.Visibility = Visibility.Visible;
					SecondCountdownName.Text = currentCountdowns[1].Name;
					SecondCountdownTime.Text = currentCountdowns[1].GetFormattedRemainingTime();
				}
				else
				{
					SecondCountdownContainer.Visibility = Visibility.Collapsed;
				}
			}
			catch (Exception)
			{
				LoadFallbackCountdowns();
			}
		}
		
		private void LoadFallbackCountdowns()
		{
			FirstCountdownName.Text = "Battle Royale";
			FirstCountdownTime.Text = "Loading...";
			SecondCountdownName.Text = "Double XP";
			SecondCountdownTime.Text = "Loading...";
		}
		
		private void StartCountdownTimer()
		{
			// Create and start a timer that ticks every second
			countdownTimer = new DispatcherTimer();
			countdownTimer.Interval = TimeSpan.FromSeconds(1);
			countdownTimer.Tick += CountdownTimer_Tick;
			countdownTimer.Start();
		}
		
		private void CountdownTimer_Tick(object sender, EventArgs e)
		{
			// Update the countdown displays every tick
			if (currentCountdowns != null && currentCountdowns.Count > 0)
			{
				UpdateCountdownsDisplay();
			}
		}
		
		/// <summary>
		/// Repositions the countdown section to a new location in the launcher
		/// </summary>
		/// <param name="row">The grid row to place the countdown section</param>
		/// <param name="rowSpan">How many rows the countdown section should span</param>
		/// <param name="verticalAlignment">Vertical alignment (Top, Center, Bottom)</param>
		/// <param name="horizontalAlignment">Horizontal alignment (Left, Center, Right)</param>
		/// <param name="margin">Margin around the countdown section (left,top,right,bottom)</param>
		public void RepositionCountdowns(int row, int rowSpan, VerticalAlignment verticalAlignment, 
			HorizontalAlignment horizontalAlignment, Thickness margin)
		{
			// Set the grid row and row span
			Grid.SetRow(CountdownsGrid, row);
			Grid.SetRowSpan(CountdownsGrid, rowSpan);
			
			// Set the alignment for the entire grid
			CountdownsGrid.VerticalAlignment = verticalAlignment;
			CountdownsGrid.HorizontalAlignment = horizontalAlignment;
			
			// Set the margin for the entire grid
			CountdownsGrid.Margin = margin;
			
			// Reset the container alignments to stretch to fill the grid
			CountdownsBackgroundContainer.VerticalAlignment = VerticalAlignment.Stretch;
			CountdownsBackgroundContainer.HorizontalAlignment = HorizontalAlignment.Stretch;
			CountdownsBackgroundContainer.Margin = new Thickness(0);
			
			// Adjust the title and content positions based on vertical alignment
			if (verticalAlignment == VerticalAlignment.Top)
			{
				// Position title at the top
				CountdownsTitle.VerticalAlignment = VerticalAlignment.Top;
				CountdownsTitle.Margin = new Thickness(0, -15, 0, 0);
				
				// Position content below the title
				CountdownsStackPanel.VerticalAlignment = VerticalAlignment.Top;
				CountdownsStackPanel.Margin = new Thickness(0, 35, 0, 0);
			}
			else if (verticalAlignment == VerticalAlignment.Bottom)
			{
				// Position title at the top of the container
				CountdownsTitle.VerticalAlignment = VerticalAlignment.Top;
				CountdownsTitle.Margin = new Thickness(0, -15, 0, 0);
				
				// Position content below the title
				CountdownsStackPanel.VerticalAlignment = VerticalAlignment.Top;
				CountdownsStackPanel.Margin = new Thickness(0, 35, 0, 0);
			}
			else // Center
			{
				// Position title at the top of the container
				CountdownsTitle.VerticalAlignment = VerticalAlignment.Top;
				CountdownsTitle.Margin = new Thickness(0, -15, 0, 0);
				
				// Position content below the title
				CountdownsStackPanel.VerticalAlignment = VerticalAlignment.Top;
				CountdownsStackPanel.Margin = new Thickness(0, 35, 0, 0);
			}
		}

		private void buttonBuyCoins_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				// Open the Glory Coins purchase page in the default browser
				string buyCoinsUrl = "https://gloryot.com/shop";
				
				Process.Start(new ProcessStartInfo
				{
					FileName = buyCoinsUrl,
					UseShellExecute = true
				});
			}
			catch (Exception)
			{
				// If opening URL fails, show error in version label temporarily
				string originalText = labelVersion.Text;
				labelVersion.Text = "Error opening shop";
				
				// Reset the text after 3 seconds
				var timer = new System.Windows.Threading.DispatcherTimer();
				timer.Interval = TimeSpan.FromSeconds(3);
				timer.Tick += (s, args) =>
				{
					labelVersion.Text = originalText;
					timer.Stop();
				};
				timer.Start();
			}
		}

		private void DiscordLogo_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			try
			{
				// Open the Discord invite link in the default browser
				string discordInviteUrl = "https://discord.com/invite/zgumtCmEbz";
				
				Process.Start(new ProcessStartInfo
				{
					FileName = discordInviteUrl,
					UseShellExecute = true
				});
			}
			catch (Exception)
			{
				// If opening URL fails, show error in version label temporarily
				string originalText = labelVersion.Text;
				labelVersion.Text = "Error opening Discord";
				
				// Reset the text after 3 seconds
				var timer = new System.Windows.Threading.DispatcherTimer();
				timer.Interval = TimeSpan.FromSeconds(3);
				timer.Tick += (s, args) =>
				{
					labelVersion.Text = originalText;
					timer.Stop();
				};
				timer.Start();
			}
		}

		private void ImageLogoServer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			// Check if the click is on a non-transparent pixel
			if (IsClickOnVisiblePixel(sender as Image, e))
			{
				try
				{
					// Open the gloryot.com website in the default browser
					string gloryotUrl = "https://gloryot.com";
					
					Process.Start(new ProcessStartInfo
					{
						FileName = gloryotUrl,
						UseShellExecute = true
					});
				}
				catch (Exception)
				{
					// If opening URL fails, show error in version label temporarily
					string originalText = labelVersion.Text;
					labelVersion.Text = "Error opening website";
					
					// Reset the text after 3 seconds
					var timer = new System.Windows.Threading.DispatcherTimer();
					timer.Interval = TimeSpan.FromSeconds(3);
					timer.Tick += (s, args) =>
					{
						labelVersion.Text = originalText;
						timer.Stop();
					};
					timer.Start();
				}
			}
		}

		private void ImageLogoServer_MouseMove(object sender, MouseEventArgs e)
		{
			var image = sender as Image;
			if (image == null) return;

			// Check if the mouse is over a visible pixel and update cursor and tooltip accordingly
			if (IsMouseOverVisiblePixel(image, e))
			{
				image.Cursor = Cursors.Hand;
				
				// Show tooltip only over visible pixels
				if (image.ToolTip == null)
				{
					var tooltip = new ToolTip
					{
						Background = new SolidColorBrush(Color.FromRgb(42, 42, 42)),
						BorderBrush = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
						Foreground = new SolidColorBrush(Colors.White),
						Content = new TextBlock
						{
							Text = "Click to visit gloryot.com",
							FontWeight = FontWeights.Bold
						}
					};
					image.ToolTip = tooltip;
				}
			}
			else
			{
				image.Cursor = Cursors.Arrow;
				
				// Hide tooltip when over transparent pixels
				image.ToolTip = null;
			}
		}

		private void ImageLogoServer_MouseLeave(object sender, MouseEventArgs e)
		{
			var image = sender as Image;
			if (image == null) return;

			// Reset cursor and hide tooltip when leaving the image
			image.Cursor = Cursors.Arrow;
			image.ToolTip = null;
		}

		private bool IsClickOnVisiblePixel(Image image, MouseButtonEventArgs e)
		{
			if (image?.Source == null) return false;

			try
			{
				// Get the position of the click relative to the image
				Point clickPosition = e.GetPosition(image);
				
				// Get the image source as BitmapSource
				BitmapSource bitmapSource = image.Source as BitmapSource;
				if (bitmapSource == null) return true; // If we can't check, allow the click
				
				// Calculate the actual pixel coordinates considering the image scaling
				double scaleX = bitmapSource.PixelWidth / image.ActualWidth;
				double scaleY = bitmapSource.PixelHeight / image.ActualHeight;
				
				int pixelX = (int)(clickPosition.X * scaleX);
				int pixelY = (int)(clickPosition.Y * scaleY);
				
				// Check bounds
				if (pixelX < 0 || pixelX >= bitmapSource.PixelWidth || 
					pixelY < 0 || pixelY >= bitmapSource.PixelHeight)
					return false;
				
				// Create a cropped bitmap of just the clicked pixel
				var croppedBitmap = new CroppedBitmap(bitmapSource, new Int32Rect(pixelX, pixelY, 1, 1));
				
				// Get the pixel data
				byte[] pixelData = new byte[4]; // RGBA
				croppedBitmap.CopyPixels(pixelData, 4, 0);
				
				// Check if the alpha channel indicates the pixel is visible (not fully transparent)
				// For most image formats, alpha is in the 4th byte (index 3)
				byte alpha = pixelData[3];
				
				// Return true if the pixel is not fully transparent (alpha > 0)
				return alpha > 0;
			}
			catch (Exception)
			{
				// If there's any error in pixel checking, allow the click
				return true;
			}
		}

		private bool IsMouseOverVisiblePixel(Image image, MouseEventArgs e)
		{
			if (image?.Source == null) return false;

			try
			{
				// Get the position of the mouse relative to the image
				Point mousePosition = e.GetPosition(image);
				
				// Get the image source as BitmapSource
				BitmapSource bitmapSource = image.Source as BitmapSource;
				if (bitmapSource == null) return true; // If we can't check, assume visible
				
				// Calculate the actual pixel coordinates considering the image scaling
				double scaleX = bitmapSource.PixelWidth / image.ActualWidth;
				double scaleY = bitmapSource.PixelHeight / image.ActualHeight;
				
				int pixelX = (int)(mousePosition.X * scaleX);
				int pixelY = (int)(mousePosition.Y * scaleY);
				
				// Check bounds
				if (pixelX < 0 || pixelX >= bitmapSource.PixelWidth || 
					pixelY < 0 || pixelY >= bitmapSource.PixelHeight)
					return false;
				
				// Create a cropped bitmap of just the pixel under the mouse
				var croppedBitmap = new CroppedBitmap(bitmapSource, new Int32Rect(pixelX, pixelY, 1, 1));
				
				// Get the pixel data
				byte[] pixelData = new byte[4]; // RGBA
				croppedBitmap.CopyPixels(pixelData, 4, 0);
				
				// Check if the alpha channel indicates the pixel is visible (not fully transparent)
				// For most image formats, alpha is in the 4th byte (index 3)
				byte alpha = pixelData[3];
				
				// Return true if the pixel is not fully transparent (alpha > 0)
				return alpha > 0;
			}
			catch (Exception)
			{
				// If there's any error in pixel checking, assume visible
				return true;
			}
		}

	}
}