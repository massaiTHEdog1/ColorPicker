using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ColorPicker.Annotations;
using ColorPicker.Classes;

namespace ColorPicker
{
	/// <summary>
	/// Logique d'interaction pour ColorPickerControl.xaml
	/// </summary>
	public partial class ColorPickerControl : INotifyPropertyChanged
	{
		#region Variables

		private Color _actualColor;

		private Rectangle _rectangleControl;
		private TextBlock _textBlockHexadecimalControl;
		private TextBlock _textBlockRedControl;
		private TextBlock _textBlockGreenControl;
		private TextBlock _textBlockBlueControl;

		#endregion



		#region Properties

		public Color ActualColor
		{
			get { return _actualColor; }
			set
			{
				_actualColor = value;

				Dispatcher.Invoke(() =>
				{
					_rectangleControl.Fill = new SolidColorBrush(Color.FromArgb(value.A, value.R, value.G, value.B));
					_textBlockHexadecimalControl.Text = value.ToString().Remove(1, 2);//On retire l'alpha car il n'y en a pas
					_textBlockRedControl.Text = value.R.ToString();
					_textBlockGreenControl.Text = value.G.ToString();
					_textBlockBlueControl.Text = value.B.ToString();
				});

				OnPropertyChanged();
			}
		}

		#endregion



		#region Commands

		public ICommand CopyHexadecimalCommand { get; private set; }
		public ICommand CopyRedCommand { get; private set; }
		public ICommand CopyGreenCommand { get; private set; }
		public ICommand CopyBlueCommand { get; private set; }

		#endregion



		#region Methods

		#region Constructor

		public ColorPickerControl()
		{
			InitializeComponent();

			_rectangleControl = RectangleToFill;
			_textBlockHexadecimalControl = TextBlockHexadecimal;
			_textBlockRedControl = TextBlockRed;
			_textBlockGreenControl = TextBlockGreen;
			_textBlockBlueControl = TextBlockBlue;

			ActualColor = Color.FromRgb(255, 255, 255);

			CopyRedCommand = new RelayCommand<string>(CopyText);
			CopyGreenCommand = new RelayCommand<string>(CopyText);
			CopyBlueCommand = new RelayCommand<string>(CopyText);
			CopyHexadecimalCommand = new RelayCommand(CopyHexadecimal);
		}

		#endregion

		private void CopyHexadecimal()
		{
			Clipboard.SetText(ActualColor.ToString().Remove(0, 3));
		}

		private void CopyText(string value)
		{
			Clipboard.SetText(value);
		}

		#region Interface INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		[NotifyPropertyChangedInvocator]
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		#endregion

		#endregion
	}
}
