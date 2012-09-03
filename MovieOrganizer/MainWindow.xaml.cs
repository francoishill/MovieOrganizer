using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Microsoft.Win32;
using System.IO;
using System.Diagnostics;
using MovieOrganizerSettings = SharedClasses.OnlineSettings.MovieOrganizerSettings;
using SharedClasses;

namespace MovieOrganizer
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			treeView1.Items.Clear();

			var movieFileExtensions = MovieOrganizerSettings.Instance.MovieFileExtensions;
			var dir = GlobalSettings.MovieOrganizerSettings.Instance.MoviesRootDirectory;//@"F:\Movies";
			var allFiles = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
			var allExtensions =
				allFiles
				.Where(s => movieFileExtensions.Contains(Path.GetExtension(s).ToLower().Substring(1)))
				.Select(f => new MovieName(f.ToLower()));

			SortedDictionary<string, List<MovieName>> sameWordsAndFilepaths = new SortedDictionary<string, List<MovieName>>();
			foreach (MovieName mn in allExtensions)
			{
				if (!sameWordsAndFilepaths.ContainsKey(mn.SortedWordsConcatenated))
					sameWordsAndFilepaths.Add(mn.SortedWordsConcatenated, new List<MovieName>());
				if (!sameWordsAndFilepaths[mn.SortedWordsConcatenated].Contains(mn))
				{
					if (sameWordsAndFilepaths[mn.SortedWordsConcatenated].Count(m => m.fullPath == mn.fullPath) == 0)
						if (sameWordsAndFilepaths[mn.SortedWordsConcatenated].Count == 0 || mn.isFolder || (sameWordsAndFilepaths[mn.SortedWordsConcatenated].Count(m => m.parentFolderPath == mn.parentFolderPath) == 0))
							sameWordsAndFilepaths[mn.SortedWordsConcatenated].Add(mn);
				}
			}

			List<string> tmpListboxItems = new List<string>();
			foreach (string key in sameWordsAndFilepaths.Keys)
			{
				if (sameWordsAndFilepaths[key].Count > 1)
				{
					int newindex = treeView1.Items.Add(new TreeViewItem() { Header = key });
					(treeView1.Items[newindex] as TreeViewItem).MouseDoubleClick += (s, ev) =>
					{
						var tvi = s as TreeViewItem;
						if (tvi.IsExpanded)
							ev.Handled = true;
						foreach (TreeViewItem child in tvi.Items)
						{
							MovieName childMovie = child.Tag as MovieName;
							if (childMovie != null)
								childMovie.SelectInExplorer();
						}
					};
					foreach (MovieName movie in sameWordsAndFilepaths[key])
					{
						TreeViewItem tvi = new TreeViewItem() { Header = movie.fullPath, Tag = movie };
						tvi.MouseDoubleClick += (s, ev) =>
						{
							TreeViewItem thisTvi = s as TreeViewItem;
							if (thisTvi == null) return;
							MovieName thisMovie = thisTvi.Tag as MovieName;
							if (thisMovie == null) return;
							ev.Handled = true;
							thisMovie.SelectInExplorer();
						};
						((TreeViewItem)treeView1.Items[newindex]).Items.Add(tvi);
					}
				}
				else
				{
					var lbi = new ListBoxItem() { Content = key + " => " + sameWordsAndFilepaths[key][0], Tag = sameWordsAndFilepaths[key][0] };
					lbi.MouseDoubleClick += (s, ev) =>
					{
						ListBoxItem thisLbi = s as ListBoxItem;
						if (thisLbi == null) return;
						MovieName thisMovie = thisLbi.Tag as MovieName;
						if (thisMovie == null) return;
						thisMovie.SelectInExplorer();
					};
					listBox1.Items.Add(lbi);
				}
			}
		}

		class MovieName
		{
			public string fullPath;
			public string parentFolderPath;
			public string parentFolderName;
			public List<string> Words;
			public List<string> SortedWords;
			public string SortedWordsConcatenated;
			public bool isFolder { get; private set; }
			public MovieName(string fullMoviePath)
			{
				string filenameOnly = fullMoviePath.Substring(fullMoviePath.LastIndexOf("\\") + 1);
				fullPath = fullMoviePath;
				if (Path.GetFileName(Path.GetDirectoryName(fullMoviePath)).Equals("VIDEO_TS", StringComparison.InvariantCultureIgnoreCase)
					|| (Path.GetFileName(fullMoviePath).StartsWith("VTS_", StringComparison.InvariantCultureIgnoreCase) && (fullMoviePath.EndsWith(".IFO", StringComparison.InvariantCultureIgnoreCase) || fullMoviePath.EndsWith(".VOB", StringComparison.InvariantCultureIgnoreCase))))
				{
					isFolder = true;
					string movieMainDir = Path.GetDirectoryName(Path.GetDirectoryName(fullMoviePath));
					filenameOnly = Path.GetFileName(movieMainDir);
					fullPath = movieMainDir;
					parentFolderPath = fullPath;
					parentFolderName = Path.GetFileName(fullPath);
				}
				else
				{
					isFolder = false;
					parentFolderPath = Path.GetDirectoryName(fullPath);
					parentFolderName = Path.GetFileName(Path.GetDirectoryName(fullPath));
				}

				Words = GetWords(filenameOnly.Substring(0, filenameOnly.LastIndexOf('.') != -1 ? filenameOnly.LastIndexOf('.') : filenameOnly.Length));
				SortedWords = new List<string>(Words); SortedWords.Sort();
				SortedWordsConcatenated = string.Join(",", SortedWords);
			}

			public override string ToString()
			{
				return string.Join(",", SortedWords) + " (" + string.Join(" ", Words) + ")";
			}
			private List<string> GetWords(string filenameWithoutExtension)
			{
				string tmpFilename = filenameWithoutExtension;
				var irrelevantPhrases = MovieOrganizerSettings.Instance.IrrelevantPhrases;
				foreach (string ph in irrelevantPhrases)
					tmpFilename = tmpFilename.Replace(ph, "");
				char[] nonwordChars = MovieOrganizerSettings.Instance.NonWordChars.ToCharArray();
				List<string> tmplist = tmpFilename.Split(nonwordChars, StringSplitOptions.RemoveEmptyEntries).ToList();
				RemoveIrrelevantWords(tmplist);
				return tmplist;
			}
			private void RemoveIrrelevantWords(List<string> list)
			{
				var irrelevantWords = MovieOrganizerSettings.Instance.IrrelevantWords;
				for (int i = list.Count - 1; i >= 0; i--)
				{
					if (irrelevantWords.Contains(list[i]))
						if (!parentFolderName.Equals(list[i], StringComparison.InvariantCultureIgnoreCase) || parentFolderName.Equals("cd1", StringComparison.InvariantCultureIgnoreCase) || parentFolderName.Equals("cd2", StringComparison.InvariantCultureIgnoreCase))
							list.RemoveAt(i);
				}
			}

			public void SelectInExplorer()
			{
				if (File.Exists(this.fullPath) || Directory.Exists(this.fullPath))
					Process.Start("explorer", "/select, \"" + this.fullPath + "\"");
			}
		}
	}
}
