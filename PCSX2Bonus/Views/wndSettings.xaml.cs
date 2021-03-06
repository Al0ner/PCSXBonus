﻿using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using PCSX2Bonus.Properties;

namespace PCSX2Bonus.Views {
	public sealed partial class wndSettings {
		private Legacy.Gamepad _gamepad;

		public wndSettings() {
			InitializeComponent();
			Owner = System.Windows.Application.Current.MainWindow;
			Loaded += wndSettings_Loaded;
		}

		private void btnBrowseData_Click(object sender, RoutedEventArgs e) {
			var dialog = new FolderBrowserDialog {
				Description = "Select the directory containing the PCSX2 data folders (bios, inis, logs, memcards, snaps, sstates)"
			};
			if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
			string[] first = { "inis", "bios", "logs", "memcards", "snaps", "sstates" };
			var second = (
				from d in Directory.GetDirectories(dialog.SelectedPath)
				select new DirectoryInfo(d).Name
			).ToArray();
			if (first.Except(second).Any())
				Legacy.Tools.ShowMessage("A required folder has not been found!", Legacy.MessageType.Error);
			else
				tbPcsx2DataDir.Text = dialog.SelectedPath;
		}

		private void btnBrowseDir_Click(object sender, RoutedEventArgs e) {
			var dialog = new FolderBrowserDialog {
				Description = "Select the directory containing PCSX2"
			};
			if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
				tbPcsx2Dir.Text = dialog.SelectedPath;
		}

		private void btnBrowseExe_Click(object sender, RoutedEventArgs e) {
			var dialog = new Microsoft.Win32.OpenFileDialog {
				Filter = "Executables | *.exe",
				Multiselect = false
			};
			if (dialog.ShowDialog() == true)
				tbPcsx2Exe.Text = dialog.FileName;
		}

		private void btnCancel_Click(object sender, RoutedEventArgs e) {
			Close();
		}

		private void btnCancelSet_Click(object sender, RoutedEventArgs e) {
			IsHitTestVisible = false;
			tbButtonCancel.Text = "Waiting...";
			_gamepad.PollAsync();
			EventHandler handler = null;
			handler = (o, x) => Dispatcher.Invoke(delegate {
				Settings.Default.buttonCancel = (int)o;
				_gamepad.CancelPollAsync();
				_gamepad.ButtonPressed -= handler;
				IsHitTestVisible = true;
			});
			_gamepad.ButtonPressed += handler;
		}

		private void btnConfirmSet_Click(object sender, RoutedEventArgs e) {
			IsHitTestVisible = false;
			tbButtonComfirm.Text = "Waiting...";
			_gamepad.PollAsync();
			EventHandler handler = null;
			handler = (o, x) => Dispatcher.Invoke(delegate {
				Settings.Default.buttonOk = (int)o;
				_gamepad.CancelPollAsync();
				_gamepad.ButtonPressed -= handler;
				IsHitTestVisible = true;
			});
			_gamepad.ButtonPressed += handler;
		}

		private void btnEditTheme_Click(object sender, RoutedEventArgs e) {
			if (cbTheme.SelectedItem == null) {
				Legacy.Tools.ShowMessage("No theme selected", Legacy.MessageType.Error);
			}
			else {
				var xmlFile = cbTheme.SelectedItem.ToString();
				Legacy.UserStyles.LoadTheme(xmlFile);
				new wndThemeEditor { Tag = Path.Combine(Legacy.UserSettings.ThemesDir, xmlFile) }.Show();
			}
		}

		private void btnOk_Click(object sender, RoutedEventArgs e) {
			Settings.Default.defaultSort = ((ComboBoxItem)cbSortType.SelectedItem).Content.ToString() != "Unsorted" ? ((ComboBoxItem)cbSortType.SelectedItem).Content.ToString() : "Default";
			Settings.Default.defaultView = ((ComboBoxItem)cbViewType.SelectedItem).Content.ToString();
			Settings.Default.pcsx2Dir = tbPcsx2Dir.Text;
			Settings.Default.pcsx2DataDir = tbPcsx2DataDir.Text;
			Settings.Default.pcsx2Exe = tbPcsx2Exe.Text;
			Settings.Default.defaultTheme = cbTheme.SelectedItem.ToString();

			if (cbUseGameToast	.IsChecked != null) Settings.Default.enableGameToast	= cbUseGameToast .IsChecked.Value;
			if (cbSaveInfo		.IsChecked != null) Settings.Default.saveInfo			= cbSaveInfo	 .IsChecked.Value;
			if (cbUseUpdated	.IsChecked != null) Settings.Default.useUpdatedCompat	= cbUseUpdated	 .IsChecked.Value;
			if (cbEnableGamepad	.IsChecked != null) Settings.Default.enableGamepad		= cbEnableGamepad.IsChecked.Value;

			Close();
		}

		private void cbEnableGamepad_Checked(object sender, RoutedEventArgs e) {
			if (cbEnableGamepad.IsChecked != null && cbEnableGamepad.IsChecked.Value) {
				_gamepad = new Legacy.Gamepad(this);
				if (_gamepad.IsValid) return;
				cbEnableGamepad.IsChecked = false;
				Legacy.Tools.ShowMessage("Error enabling gamepad", Legacy.MessageType.Error);
			}
			else {
				cbEnableGamepad.IsChecked = false;
				Legacy.Tools.ShowMessage("Error enabling gamepad [Error code: 10045]", Legacy.MessageType.Error);
			}
		}

		private void FetchThemes() {
			foreach (var str in Directory.GetFiles(Legacy.UserSettings.ThemesDir, "*.xml").ToArray())
				cbTheme.Items.Add(Path.GetFileName(str));
			cbTheme.SelectedItem = Settings.Default.defaultTheme;
		}

		private void Setup() {
			FetchThemes();
			var defaultView = Settings.Default.defaultView;
			if (defaultView != null)
				if (defaultView != "Stacked")
					switch (defaultView) {
						case "Tile":
							cbViewType.SelectedIndex = 1;
							break;
						case "TV":
							cbViewType.SelectedIndex = 2;
							break;
					}
				else
					cbViewType.SelectedIndex = 0;
			var defaultSort = Settings.Default.defaultSort;
			if (defaultSort == null) return;
			if (defaultSort != "Alphabetical") {
				if (defaultSort == "Serial") return;
				if (defaultSort == "Default")
					cbSortType.SelectedIndex = 2;
				return;
			}
			cbSortType.SelectedIndex = 0;
		}

		private void Window_Closing(object sender, CancelEventArgs e) {
			DialogResult = true;
			if (_gamepad == null) return;
			_gamepad.CancelPollAsync();
			_gamepad.Dispose();
		}

		private void wndSettings_Loaded(object sender, RoutedEventArgs e) {
			Setup();
		}
	}
}

