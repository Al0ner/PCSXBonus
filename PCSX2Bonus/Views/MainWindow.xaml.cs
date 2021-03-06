﻿using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PCSX2Bonus.Properties;
using Extensions = PCSX2Bonus.Legacy.Extensions;

namespace PCSX2Bonus.Views {
	public sealed partial class MainWindow : IStyleConnector {
		private readonly NotifyIcon _notifyIcon = new NotifyIcon();
		private DispatcherTimer _t;

		public MainWindow() {
			InitializeComponent();
			Loaded += MainWindow_Loaded;
			CheckSettings();
		}

		private static void AddDummies() {
			const int num = 500;
			for (var i = num; i < (num + 100); i++) {
				var result = 0;
				var str = Legacy.GameManager.GameDatabase[i];
				var source = str.Trim().Split('\n');
				var str2 = source.FirstOrDefault(s => s.Contains("Compat"));
				if (string.IsNullOrWhiteSpace(str2) == false)
					result = int.TryParse(str2.Replace("Compat = ", ""), out result) ? result : 0;
				var g = new Legacy.Game {
					Serial = source[0],
					Title  = source[1],
					Region = source[2],
					Description = "n/a*",
					Location = "",
					ImagePath = "",
					Compatibility = result
				};
				if (Legacy.Game.AllGames.All(game => game.Serial != g.Serial))
					Legacy.GameManager.AddToLibrary(g);
			}
		}

		private static async void AddFromImageFile() {
			var ofd = new Microsoft.Win32.OpenFileDialog { Multiselect = true };
			if (ofd.ShowDialog() == true)
				await Legacy.GameManager.AddGamesFromImages(ofd.FileNames);
		}

		public void ApplySort(string category, ListSortDirection direction) {
			lvGames.Items.SortDescriptions.Clear();
			lvGames.Items.SortDescriptions.Add(new SortDescription(category, direction));
		}

		private void ApplyWindowSettings() {
			if (Settings.Default.windowSize != Size.Empty) {
				Width = Settings.Default.windowSize.Width;
				Height = Settings.Default.windowSize.Height;
			}
			WindowState = (WindowState)Enum.Parse(typeof(WindowState), Settings.Default.windowState);
			Left = Settings.Default.windowLocation.X;
			Top = Settings.Default.windowLocation.Y;
		}

		private static void CheckSettings() {
			if ((!Extensions.IsEmpty(Settings.Default.pcsx2Dir) && !Extensions.IsEmpty(Settings.Default.pcsx2DataDir)) &&
				(Directory.Exists(Settings.Default.pcsx2Dir) && Directory.Exists(Settings.Default.pcsx2DataDir))) return;
			Legacy.Tools.ShowMessage("Please select the directory containing PCSX2 and the PCSX2 data directory", Legacy.MessageType.Info);
			new wndSetup().ShowDialog();
		}

		private void ClearViewStates(System.Windows.Controls.Primitives.ToggleButton sender) {
			btnStacked.IsChecked = false;
			btnTile.IsChecked = false;
			btnTV.IsChecked = false;
			sender.IsChecked = true;
		}

		private static void CrashMe() {
			// ReSharper disable once PossibleNullReferenceException
			// ReSharper disable once ReturnValueOfPureMethodIsNotUsed
			((object)null).ToString();
		}

		private void DeleteSaveStates() {
			if (lvSaveStates.SelectedItems.Count == 0) return;
			var list = lvSaveStates.ItemsSource.Cast<Legacy.SaveState>().ToList<Legacy.SaveState>();
			foreach (var item in lvSaveStates.SelectedItems.Cast<Legacy.SaveState>()) {
				File.Delete(item.Location);
				list.Remove(item);
			}
			lvSaveStates.ItemsSource = list;
		}

		private void Filter(object sender, EventArgs e) {
			_t.Stop();
			if (lvGames.IsVisible == false) return;
			var query = tbSearch.Text;
			var defaultView = CollectionViewSource.GetDefaultView(Legacy.Game.AllGames);
			if (Extensions.IsEmpty(query) || query == "Search")
				defaultView.Filter = null;
			else {
				defaultView.Filter = (Predicate<object>)Delegate.Combine(defaultView.Filter, (Predicate<object>)(o => Extensions.Contains(((Legacy.Game)o).Title, query, StringComparison.InvariantCultureIgnoreCase)));
				((ScrollViewer)Legacy.Tools.GetDescendantByType(lvGames, typeof(ScrollViewer))).ScrollToVerticalOffset(0.0);
			}
		}

		private async void HideRescrape() {
			lvScrape.IsHitTestVisible = false;
			lvGames.Visibility = Visibility.Visible;
			await SlideIn();
			lvScrape.Visibility = Visibility.Collapsed;
		}

		private async void HideSaveStates() {
			await SlideIn();
			lvSaveStates.Visibility = Visibility.Collapsed;
			lvSaveStates.IsHitTestVisible = false;
		}

		private async void HideWidescreenResults() {
			await SlideIn();
			gWideScreenResults.Visibility = Visibility.Collapsed;
			gWideScreenResults.IsHitTestVisible = false;
		}

		public void LaunchGame(Legacy.Game g, bool tvMode = false) {
			if (File.Exists(g.Location) == false) {
				Legacy.Tools.ShowMessage("Unable to find image file!", Legacy.MessageType.Error);
				if (tvMode == false) return;
				var window = System.Windows.Application.Current.Windows.Cast<Window>().FirstOrDefault(w => w.Title == "wndFullScreen");
				if (window != null)
					window.ShowDialog();
			}
			else {
				var pcsxProcess = new Process();
				var path = Legacy.UserSettings.RootDir + string.Format(@"\Configs\{0}", g.FileSafeTitle);
				var str = Directory.Exists(path) ? string.Format(" --cfgpath={0}{2}{0} {0}{1}{0}", "\"", g.Location, path) : string.Format(" {0}{1}{0}", "\"", g.Location);
				str = str.Replace(@"\\", @"\");
				var src = string.Empty;
				var str4 = Settings.Default.pcsx2Dir;
				if (File.Exists(path + @"\PCSX2Bonus.ini")){
					var file = new Legacy.IniFile(path + @"\PCSX2Bonus.ini");
					var str5 = file.Read("Additional Executables", "Default");
					var str6 = Extensions.IsEmpty(str5) ? Settings.Default.pcsx2Exe : str5;
					str4 = Extensions.IsEmpty(str5) ? Settings.Default.pcsx2Dir : Path.GetDirectoryName(str5);
					var str7 = file.Read("Boot", "NoGUI");
					var str8 = file.Read("Boot", "UseCD");
					var str9 = file.Read("Boot", "NoHacks");
					var str10 = file.Read("Boot", "FullBoot");
					src = file.Read("Shader", "Default");
					pcsxProcess.StartInfo.FileName = str6;
					if (str7 == "true") str = str.Insert(0, " --nogui");
					if (str8 == "true") str = str.Insert(0, " --usecd");
					if (str9 == "true") str = str.Insert(0, " --nohacks");
					if (str10 == "true") str = str.Insert(0, " --fullboot");
				}
				else
					pcsxProcess.StartInfo.FileName = Settings.Default.pcsx2Exe;
				pcsxProcess.EnableRaisingEvents = true;
				if (str4 != null) {
					pcsxProcess.StartInfo.WorkingDirectory = str4;
					pcsxProcess.StartInfo.Arguments = str;
					if (Extensions.IsEmpty(src) == false) {
						try {
							File.Copy(src, Path.Combine(str4, "shader.fx"), true);
						}
						catch (Exception exception) {
							Legacy.Tools.ShowMessage("Could not save shader file! Details: " + exception.Message, Legacy.MessageType.Error);
						}
					}
				}
				var timeOpened = DateTime.Now;
				pcsxProcess.Exited += delegate{
					pcsxProcess.Dispose();
					System.Windows.Application.Current.Dispatcher.Invoke(delegate {
						var now = DateTime.Now;
						g.TimePlayed = g.TimePlayed.Add(now.Subtract(timeOpened));
						var element = Legacy.UserSettings.xGames.Descendants("Game").FirstOrDefault(x => {
							var xElement1 = x.Element("Name");
							return xElement1 != null && xElement1.Value == g.Title;
						});
						if ((element != null) && (element.Element("Time") != null)) {
							var xElement = element.Element("Time");
							if (xElement != null) xElement.Value = g.TimePlayed.ToString();
						}
						if (tvMode) {
							var window = System.Windows.Application.Current.Windows.Cast<Window>().FirstOrDefault(w => w.Title == "wndFullScreen");
							if (window != null)
								window.ShowDialog();
						}
						_notifyIcon.Visible = false;
						Show();
						Activate();
					});
				};
				Hide();
				if (Settings.Default.enableGameToast)
					new wndGameNotify{Tag = g}.Show();
				_notifyIcon.Text = string.Format("Currently playing [{0}]", Extensions.Truncate(g.Title, 40));
				_notifyIcon.Visible = true;
				pcsxProcess.Start();
			}
		}

		private void lvGames_ContextMenuOpening(object sender, ContextMenuEventArgs e) {
			switch (lvGames.SelectedItems.Count) {
				case 0:
					e.Handled = true;
					break;
				case 1:
					foreach (var element in lvGames.ContextMenu.Items.Cast<FrameworkElement>())
						element.Visibility = Visibility.Visible;
					break;
				default:
					if (lvGames.SelectedItems.Count > 1)
						foreach (var element2 in lvGames.ContextMenu.Items.Cast<FrameworkElement>())
							element2.Visibility = element2.Name == "miRemove" ? Visibility.Visible : Visibility.Collapsed;
					break;
			}
		}

		private void lvGames_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			var count = lvGames.SelectedItems.Count;
			if ((count > 1) || (count == 0)) return;
			lvGames.Tag = lvGames.SelectedItem;
			var root = (System.Windows.Controls.ListViewItem)lvGames.ItemContainerGenerator.ContainerFromItem(lvGames.SelectedItem);
			var image = Legacy.Tools.FindByName("imgGameCover", root) as Image;
			if (image != null) {
				imgPreview.Source = image.Source;
			}
		}

		public void LviDoubleClick(object sender, MouseButtonEventArgs e) {
			var source = (System.Windows.Controls.ListViewItem)e.Source;
			var dataContext = (Legacy.Game)source.DataContext;
			LaunchGame(dataContext);
		}

		private void MainWindow_Loaded(object sender, RoutedEventArgs e) {
			Setup();
		}

		private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e) {
			switch (e.Key) {
				case Key.Escape:
					if (!lvGames.IsVisible && lvScrape.IsVisible)
						HideRescrape();
					else if (!lvGames.IsVisible && lvSaveStates.IsVisible)
						HideSaveStates();
					else if (!lvGames.IsVisible && gWideScreenResults.IsVisible)
						HideWidescreenResults();
					break;
				case Key.Oem3:
					switch (tbDebug.Visibility) {
						case Visibility.Collapsed:
							tbDebug.Text = string.Empty;
							tbDebug.Visibility = Visibility.Visible;
							tbDebug.Focus();
							e.Handled = true;
							break;
						case Visibility.Visible:
							tbDebug.Visibility = Visibility.Collapsed;
							break;
					}
					break;
			}
		}

		private void miAddFromImage_Click(object sender, RoutedEventArgs e) {
			AddFromImageFile();
		}

		private void miRescrape_Click(object sender, RoutedEventArgs e) {
			ShowScrapeResults();
		}

		private static void RemoveSelectedGames(IEnumerable<Legacy.Game> games) {
			foreach (var game in games)
				Legacy.GameManager.Remove(game);
		}

		private async void Rescrape() {
			var selectedItem = (Legacy.GameSearchResult)lvScrape.SelectedItem;
			var tag = (Legacy.Game)lvGames.Tag;
			var mbr = System.Windows.MessageBox.Show(string.Format("Are you sure you want to update the game {0} to {1}?", tag.Title, selectedItem.Name), "PCSX2Bonus - Confirm Game Update", MessageBoxButton.YesNo, MessageBoxImage.Question);
			if (mbr != MessageBoxResult.No) {
				lvScrape.IsHitTestVisible = false;
				Legacy.Toaster.Instance.ShowToast("Fetching info for: " + selectedItem.Name);
			}
			else
				return;
			await Legacy.GameManager.ReFetchInfo(tag, selectedItem.Link);
			Legacy.Toaster.Instance.ShowToast("Successfully updated info for: " + tag.Title, 0xbb8);
			HideRescrape();
		}

		private void SaveWindowSettings() {
			if (WindowState != WindowState.Minimized) {
				Settings.Default.windowLocation = new Point(Left, Top);
				Settings.Default.windowSize = new Size(Width, Height);
				Settings.Default.windowState = WindowState.Normal.ToString();
			}
			else if (WindowState == WindowState.Maximized)
				Settings.Default.windowState = WindowState.Maximized.ToString();
		}

		private async void Setup(){
			ApplyWindowSettings();
			tbDebug.KeyDown += tbDebug_KeyDown;
			SetupNotifyIcon();
			lvGames.SelectionChanged += lvGames_SelectionChanged;
			var contextMenu = lvGames.ContextMenu;
			NameScope.SetNameScope(contextMenu, NameScope.GetNameScope(this));
			var miPlay		 = (System.Windows.Controls.MenuItem) contextMenu.Items[ 0];
			var miRemove	 = (System.Windows.Controls.MenuItem) contextMenu.Items[ 1];
			var miRescrape   = (System.Windows.Controls.MenuItem) contextMenu.Items[ 3];
			var miSaveStates = (System.Windows.Controls.MenuItem) contextMenu.Items[ 4];
			var miWideScreen = (System.Windows.Controls.MenuItem) contextMenu.Items[13];
			miRescrape.Click += miRescrape_Click;
			lvGames.ContextMenuOpening += lvGames_ContextMenuOpening;
			miRemoveStates.Click += (o, e) => DeleteSaveStates();
			lvGames.AllowDrop = true;
			lvGames.Drop += async delegate(object o, System.Windows.DragEventArgs e){
				var data = (string[]) e.Data.GetData(System.Windows.DataFormats.FileDrop);
				var files = (
					from s in data
					let extension = Path.GetExtension(s)
					where Legacy.GameData.AcceptableFormats.Any(frm =>
						extension != null
						&& extension.Equals(frm, StringComparison.InvariantCultureIgnoreCase)
					)
					select s
				).ToArray();
				await Legacy.GameManager.AddGamesFromImages(files);
			};
			miPlay.Click += delegate{
				var count = lvGames.SelectedItems.Count;
				if ((count != 0) && (count <= 1))
					LaunchGame((Legacy.Game) lvGames.SelectedItem);
			};
			miRemove.Click += delegate{
				var games = lvGames.SelectedItems.Cast<Legacy.Game>().ToList<Legacy.Game>();
				RemoveSelectedGames(games);
			};
			miSaveStates.Click += (o, e) => ShowSaveStates();
			miWideScreen.Click += (o, e) => ShowWideScreenResults();
			_t = new DispatcherTimer(TimeSpan.FromMilliseconds(200.0), DispatcherPriority.DataBind, Filter, Dispatcher);
			btnSaveWidePatch.Click += async delegate{
				var g = (Legacy.Game) lvGames.Tag;
				if (g == null) return;
				var crc = await Legacy.GameManager.FetchCRC(g);
				if (!Directory.Exists(Settings.Default.pcsx2Dir + @"\Cheats"))
					Directory.CreateDirectory(Settings.Default.pcsx2Dir + @"\Cheats");
				var contents = new TextRange(rtbResults.Document.ContentStart, rtbResults.Document.ContentEnd).Text;
				try{
					File.WriteAllText(Settings.Default.pcsx2Dir + @"\Cheats\" + crc + ".pnach", contents);
					Legacy.Toaster.Instance.ShowToast(string.Format("Successfully saved patch for {0} as {1}.pnatch", g.Title, crc),
						0xdac);
					HideWidescreenResults();
				}
				catch (Exception exception){
					Legacy.Toaster.Instance.ShowToast("An error occured when trying to save the patch: " + exception.Message);
				}
			};
			btnStacked.Click += delegate{
				ClearViewStates(btnStacked);
				SwitchView("gridView");
			};
			btnTile.Click += delegate{
				ClearViewStates(btnTile);
				SwitchView("tileView");
			};
			btnTV.Click += delegate { SwitchView("tv"); };
			lvGames.MouseDown += (o, e) =>{
				if (e.LeftButton == MouseButtonState.Pressed)
					lvGames.UnselectAll();
			};
			PreviewKeyDown += MainWindow_PreviewKeyDown;
			lvGames.MouseDown += (o, e) =>{
				if (e.MiddleButton == MouseButtonState.Pressed)
					lvGames.UnselectAll();
			};
			lvScrape.MouseDoubleClick += delegate{
				var count = lvScrape.SelectedItems.Count;
				if ((count != 0) && (count <= 1))
					Rescrape();
			};
			tbSearch.GotFocus += delegate{
				if ((tbSearch.Text == "Search") || Extensions.IsEmpty(tbSearch.Text))
					tbSearch.Text = string.Empty;
			};
			tbSearch.LostFocus += delegate{
				if (Extensions.IsEmpty(tbSearch.Text))
					tbSearch.Text = "Search";
			};
			tbSearch.TextChanged += tbSearch_TextChanged;
			tbSearch.KeyDown += (o, e) =>{
				if (e.Key != Key.Escape) return;
				tbSearch.Text = "Search";
				lvGames.Focus();
			};
			await Legacy.GameManager.BuildDatabase();
			Legacy.GameManager.GenerateDirectories();
			Legacy.GameManager.LoadXml();
			await Legacy.GameManager.GenerateUserLibrary();
			Legacy.GameManager.UpdateGamesToLatestCompatibility();
			var defaultView = Settings.Default.defaultView;
			if (defaultView != null){
				if (defaultView != "Stacked"){
					switch (defaultView){
						case "Tile":
							SwitchView("tileView");
							ClearViewStates(btnTile);
							break;
						case "TV":
							SwitchView("tv");
							ClearViewStates(btnStacked);
							break;
					}
				}
				else{
					SwitchView("gridView");
					ClearViewStates(btnStacked);
				}
			}
			var defaultSort = Settings.Default.defaultSort;
			if (defaultSort == null) return;
			if (defaultSort != "Alphabetical")
				switch (defaultSort){
					case "Serial":
						ApplySort("Serial", ListSortDirection.Ascending);
						break;
					case "Default":
						break;
				}
			else
				ApplySort("Title", ListSortDirection.Ascending);
		}

		private void SetupNotifyIcon() {
			_notifyIcon.Icon = Properties.Resources.icon;
		}

		private async void ShowSaveStates() {
			var selectedItem = (Legacy.Game)lvGames.SelectedItem;
			if (selectedItem == null) return;
			var states = Legacy.GameManager.FetchSaveStates(selectedItem);
			if (states.Count != 0){
				lvSaveStates.Visibility = Visibility.Visible;
				lvSaveStates.ItemsSource = states;
				lvSaveStates.IsHitTestVisible = true;
				lvGames.UnselectAll();
				await SlideOut();
			}
			else
				Legacy.Toaster.Instance.ShowToast("No save states found", 0x9c4);
		}

		private async void ShowScrapeResults() {
			lvScrape.ItemsSource = null;
			var selectedItem = (Legacy.Game)lvGames.SelectedItem;
			if (selectedItem == null) return;
			lvScrape.Visibility = Visibility.Visible;
			lvScrape.IsHitTestVisible = true;
			lvGames.UnselectAll();
			lvGames.IsHitTestVisible = false;
			Legacy.Toaster.Instance.ShowToast("Fetching results for: " + selectedItem.Title);
			var results = await Legacy.GameManager.FetchSearchResults(selectedItem);
			if (results.Count == 0) {
				HideRescrape();
				Legacy.Toaster.Instance.ShowToast("Error fetching results", 0x9c4);
			}
			else {
				lvScrape.ItemsSource = results;
				lvScrape.Items.SortDescriptions.Clear();
				lvScrape.Items.SortDescriptions.Add(new SortDescription("Rating", ListSortDirection.Ascending));
				Console.WriteLine(lvScrape.Items.Count);
				await SlideOut();
				Legacy.Toaster.Instance.HideToast();
			}
		}

		private async void ShowWideScreenResults() {
			var selectedItem = (Legacy.Game)lvGames.SelectedItem;
			if (selectedItem == null) return;
			rtbResults.Document.Blocks.Clear();
			lvGames.IsHitTestVisible = false;
			Legacy.Toaster.Instance.ShowToast("Fetching wide screen patches");
			var src = await Legacy.GameManager.FetchWideScreenPatches(selectedItem);
			if (Extensions.IsEmpty(src)) {
				Legacy.Toaster.Instance.ShowToast("No patches found", 0x5dc);
				lvGames.IsHitTestVisible = true;
			}
			else {
				rtbResults.AppendText(src);
				gWideScreenResults.Visibility = Visibility.Visible;
				gWideScreenResults.IsHitTestVisible = true;
				lvGames.UnselectAll();
				await SlideOut();
				Legacy.Toaster.Instance.HideToast();
			}
		}

		private async Task SlideIn() {
			lvGames.Visibility = Visibility.Visible;
			lvGames.IsHitTestVisible = true;
			var a = new ThicknessAnimation {
				From = new Thickness(0.0, Height, 0.0, 0.0),
				To = new Thickness(0.0),
				Duration = new Duration(TimeSpan.FromSeconds(0.5))
			};
			var storyboard = new Storyboard();
			storyboard.Children.Add(a);
			Storyboard.SetTarget(a, lvGames);
			Storyboard.SetTargetProperty(a, new PropertyPath(MarginProperty));
			await Extensions.BeginAsync(storyboard);
		}

		private async Task SlideOut() {
			lvGames.IsHitTestVisible = false;
			var a = new ThicknessAnimation {
				From = new Thickness(0.0),
				To = new Thickness(0.0, Height, 0.0, 0.0),
				Duration = new Duration(TimeSpan.FromSeconds(0.5))
			};
			var storyboard = new Storyboard();
			storyboard.Children.Add(a);
			Storyboard.SetTarget(a, lvGames);
			Storyboard.SetTargetProperty(a, new PropertyPath(MarginProperty));
			await Extensions.BeginAsync(storyboard);
			lvGames.Visibility = Visibility.Collapsed;
		}

		private void SwitchView(string view){
			if (lvGames.View == null) {
				var base2 = System.Windows.Application.Current.Resources["gridView"] as ViewBase;
				lvGames.View = base2;
			}
			if (view != "tv") {
				var base3 = System.Windows.Application.Current.Resources[view] as ViewBase;
				lvGames.View = base3;
			}
			switch (view) {
				case "gridView":
					var style = Resources["lvDarkItemStyleLocal"] as Style;
					lvGames.ItemContainerStyle = style;
					break;
				case "tileView":
					var style2 = Resources["lvGenericItemStyleLocal"] as Style;
					lvGames.ItemContainerStyle = style2;
					break;
				case "tv":
					Hide();
					btnTV.IsChecked = false;
					var screen = new wndFullScreen();
					if (screen.ShowDialog() == true)
						Show();
					break;
			}
		}

		[GeneratedCode("PresentationBuildTasks", "4.0.0.0"), DebuggerNonUserCode, EditorBrowsable(EditorBrowsableState.Never)]
		void IStyleConnector.Connect(int connectionId, object target) {
			EventSetter setter;
			switch (connectionId) {
				case 2:
					setter = new EventSetter {
						Event = MouseDoubleClickEvent,
						Handler = new MouseButtonEventHandler(LviDoubleClick)
					};
					((Style)target).Setters.Add(setter);
					return;

				case 3:
					setter = new EventSetter {
						Event = MouseDoubleClickEvent,
						Handler = new MouseButtonEventHandler(LviDoubleClick)
					};
					((Style)target).Setters.Add(setter);
					return;
			}
		}

		private void tbDebug_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) {
			if (e.Key != Key.Return) return;
			Func<string, string, bool> func = (s, ss) => s.Equals(ss, StringComparison.InvariantCultureIgnoreCase);
			var str = tbDebug.Text.Trim();
			if (func(str, "add dummies"))
				AddDummies();
			else if (func(str, "clear library")){
				var list = Legacy.Game.AllGames.ToList();
				foreach (var game2 in list)
					Legacy.GameManager.Remove(game2);
			}
			else if (func(str, "crash"))
				CrashMe();
		}

		private void tbSearch_TextChanged(object sender, TextChangedEventArgs e) {
			_t.Stop();
			_t.Start();
		}

		private void Window_Closing(object sender, CancelEventArgs e) {
			if (Legacy.UserSettings.xGames != null)
				Legacy.UserSettings.xGames.Save(Legacy.UserSettings.BonusXml);
			SaveWindowSettings();
			System.Windows.Application.Current.Shutdown();
		}
	}
}