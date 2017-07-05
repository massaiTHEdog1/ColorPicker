using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Drawing;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Interop;
using ColorPicker.Annotations;
using ColorPicker.Classes;
using Color = System.Windows.Media.Color;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace ColorPicker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INotifyPropertyChanged
    {
		#region Variables

		private const int WM_HOTKEY = 0x0312;
	    private const int VK_ESCAPE = 0x1B;
		private ObservableCollection<ColorPickerControl> _listColors;
		private ColorPickerControl _currentColorPickerControl;
		private Thread _threadColorDetection;
		private List<Process> _processesToRestore = new List<Process>();
	    private bool _isBlockMode;

	    #endregion



		#region Properties

		/// <summary>
		/// List of color pickers
		/// </summary>
		public ObservableCollection<ColorPickerControl> ListColors
		{
			get { return _listColors; }
			set
			{
				_listColors = value;
				OnPropertyChanged();
			}
		}

		/// <summary>
		/// Is block mode active?
		/// </summary>
	    public bool IsBlockMode
	    {
			get { return _isBlockMode; }
			set
			{
				_isBlockMode = value;
				OnPropertyChanged();
			}
		}

		#endregion



		#region Commands

		public RelayCommand<ColorPickerControl> StartPickingColorCommand { get; private set; }
		public RelayCommand CloseApplicationCommand { get; private set; }
		public RelayCommand MinimizeApplicationCommand { get; private set; }

		#endregion



		#region Methods

		#region Constructor

		public MainWindow()
		{
			StartPickingColorCommand = new RelayCommand<ColorPickerControl>(StartPickingColor);
			CloseApplicationCommand = new RelayCommand(CloseApplication);
			MinimizeApplicationCommand = new RelayCommand(MinimizeApplication);

			ComponentDispatcher.ThreadPreprocessMessage += ComponentDispatcher_ThreadPreprocessMessage;

			ListColors = new ObservableCollection<ColorPickerControl>()
			{
				new ColorPickerControl(),
				new ColorPickerControl(),
				new ColorPickerControl(),
				new ColorPickerControl()
			};

			InitializeComponent();
		}

		#endregion

		#region Drag methods

		private void MainWindow1_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			DragMove();
		}

		#endregion

		#region Color detection

		[DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]
		public static extern int BitBlt(IntPtr hDC, int x, int y, int nWidth, int nHeight, IntPtr hSrcDC, int xSrc, int ySrc, int dwRop);

		[DllImport("user32.dll")]
		static extern bool GetCursorPos(ref System.Drawing.Point lpPoint);

		[DllImport("user32.dll")]
		static extern bool EnableWindow(IntPtr hWnd, bool bEnable);

		[DllImport("user32.dll")]
		public static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vlc);

		[DllImport("user32.dll")]
		public static extern bool UnregisterHotKey(IntPtr hWnd, int id);



		public struct IconInfo
		{
			public bool fIcon;
			public int xHotspot;
			public int yHotspot;
			public IntPtr hbmMask;
			public IntPtr hbmColor;
		}

		

		System.Drawing.Color GetColorAt(System.Drawing.Point location)
		{
			Bitmap screenPixel = new Bitmap(1, 1, PixelFormat.Format32bppArgb);

			using (Graphics gdest = Graphics.FromImage(screenPixel))
			{
				using (Graphics gsrc = Graphics.FromHwnd(IntPtr.Zero))
				{
					IntPtr hSrcDC = gsrc.GetHdc();
					IntPtr hDC = gdest.GetHdc();
					BitBlt(hDC, 0, 0, 1, 1, hSrcDC, location.X, location.Y, (int)CopyPixelOperation.SourceCopy);
					gdest.ReleaseHdc();
					gsrc.ReleaseHdc();
				}
			}

			return screenPixel.GetPixel(0, 0);
		}



		private void StartPickingColor(ColorPickerControl control)
		{
			_currentColorPickerControl = control;

			RegisterHotKey(new WindowInteropHelper(this).Handle, GetType().GetHashCode(), 0, VK_ESCAPE);

			if (IsBlockMode)
			{
				_processesToRestore.Clear();
				Process[] processes = Process.GetProcesses();

				foreach (Process element in processes)
				{
					_processesToRestore.Add(element);
					EnableWindow(element.MainWindowHandle, false);
				}
			}

			_threadColorDetection = new Thread(ColorDetectionThread);
			_threadColorDetection.Start();
		}



		private void StopColorDetection()//Appelée par clique gauche, échap
		{
			_threadColorDetection.Abort();

			if (IsBlockMode)
			{
				foreach (Process element in _processesToRestore)
				{
					EnableWindow(element.MainWindowHandle, true);
				}
			}
		}



		private void ComponentDispatcher_ThreadPreprocessMessage(ref MSG msg, ref bool handled)
		{
			if (msg.message == WM_HOTKEY)
			{
				UnregisterHotKey(new WindowInteropHelper(this).Handle, VK_ESCAPE);
				StopColorDetection();
			}
		}



		private void ColorDetectionThread()
		{
			while (true)
			{
				System.Drawing.Point mouseCoordinates = new System.Drawing.Point();
				GetCursorPos(ref mouseCoordinates);

				System.Drawing.Color color = GetColorAt(mouseCoordinates);

				_currentColorPickerControl.ActualColor = Color.FromArgb(color.A, color.R, color.G, color.B);

				Thread.Sleep(100);
			}
		}

		#endregion

		private void CloseApplication()
		{
			Close();
		}



		private void MinimizeApplication()
		{
			WindowState = WindowState.Minimized;
		}

		#endregion



		#region Interface INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		[NotifyPropertyChangedInvocator]
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		#endregion
	}
}