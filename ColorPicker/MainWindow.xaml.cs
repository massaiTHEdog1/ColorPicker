using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KeyboardAndMouseHook;
using System.Windows.Forms;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Interop;
using Clipboard = System.Windows.Forms.Clipboard;
using Color = System.Windows.Media.Color;
using KeyEventArgs = System.Windows.Forms.KeyEventArgs;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace ColorPicker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
	    [DllImport("user32.dll")]
	    public static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vlc);

	    [DllImport("user32.dll")]
	    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);


	    bool inDrag = false;
        KeyboardAndMouseHookObject hookManager;

        public MainWindow()
        {
            InitializeComponent();
			ComponentDispatcher.ThreadPreprocessMessage += ComponentDispatcher_ThreadPreprocessMessage;
			RegisterHotKey(new WindowInteropHelper(this).Handle, GetType().GetHashCode(), 0, 0x41);
			hookManager = new KeyboardAndMouseHookObject();
		}



		public const int WM_HOTKEY = 0x0312;

		void ComponentDispatcher_ThreadPreprocessMessage(ref MSG msg, ref bool handled)
		{
			if (msg.message == WM_HOTKEY)
			{
				Debugger.Break();
			}
		}








		//################################################################
		#region Drag methods


		Point dragStartPosition;
        Point formPosition;

        private void MainWindow1_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Source == MainWindow1)
            {
                inDrag = true;

                hookManager.OnMouseMove += DragWindow;
                hookManager.OnMouseButtonUp += StopDrag;

                dragStartPosition = MainWindow1.PointToScreen(e.GetPosition(MainWindow1));
                formPosition = new Point(MainWindow1.Left, MainWindow1.Top);
            }
        }


        private void DragWindow(object sender, MouseEventArgs e)
        {
            MainWindow1.Left = e.X - dragStartPosition.X + formPosition.X;
            MainWindow1.Top = e.Y - dragStartPosition.Y + formPosition.Y;
        }


        private void StopDrag(object sender, MouseEventArgs e)
        {
            if(e.Button == MouseButtons.Left)
            {
                inDrag = false;
                hookManager.OnMouseMove -= DragWindow;
                hookManager.OnMouseButtonUp -= StopDrag;
            }
        }


        #endregion







        //################################################################
        #region Color detection

        [DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]
        public static extern int BitBlt(IntPtr hDC, int x, int y, int nWidth, int nHeight, IntPtr hSrcDC, int xSrc, int ySrc, int dwRop);

        [DllImport("user32.dll")]
        static extern bool GetCursorPos(ref System.Drawing.Point lpPoint);

        [DllImport("user32.dll")]
        static extern bool EnableWindow(IntPtr hWnd, bool bEnable);



        public struct IconInfo
        {
            public bool fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetIconInfo(IntPtr hIcon, ref IconInfo pIconInfo);
        [DllImport("user32.dll")]
        public static extern IntPtr CreateIconIndirect(ref IconInfo icon);




        //A SUPPRIMER SI PAS UTILISATION CURSEUR
        [DllImport("User32.dll")]
        public static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("User32.dll")]
        public static extern void ReleaseDC(IntPtr hwnd, IntPtr dc);

        private Thread threadColorDetection;
        private List<Process> processesToRestore = new List<Process>();
        private Color selectedRectangleColor;
        private object selectedRectangle;




        System.Drawing.Color GetColorAt(System.Drawing.Point location)
        {
            Bitmap screenPixel = new Bitmap(1, 1, PixelFormat.Format32bppArgb);

            using (Graphics gdest = Graphics.FromImage(screenPixel))
            {
                using (Graphics gsrc = Graphics.FromHwnd(IntPtr.Zero))
                {
                    IntPtr hSrcDC = gsrc.GetHdc();
                    IntPtr hDC = gdest.GetHdc();
                    int retval = BitBlt(hDC, 0, 0, 1, 1, hSrcDC, location.X, location.Y, (int)CopyPixelOperation.SourceCopy);
                    gdest.ReleaseHdc();
                    gsrc.ReleaseHdc();
                }
            }

            return screenPixel.GetPixel(0, 0);
        }



        private void StartPickupColor(object sender, MouseButtonEventArgs e)//Si on clique sur le rectangle de sélection de couleur
        {
            if (e.Source == sender)
            {

                selectedRectangle = sender;
                selectedRectangleColor = ((SolidColorBrush)((Rectangle)sender).Fill).Color;

                hookManager.BlockNextLeftMouseDown();
                hookManager.BlockNextLeftMouseUp();
                hookManager.OnLeftMouseButtonDownBlocked += PickupColor;
                hookManager.OnKeyUp += KeyPressedMaybeAbort;

                if(BlockMode.IsChecked == true)
                {
                    processesToRestore.Clear();
                    Process[] processes = Process.GetProcesses();

                    foreach (Process element in processes)
                    {
                        processesToRestore.Add(element);
                        EnableWindow(element.MainWindowHandle, false);
                    }
                }
                
                

                threadColorDetection = new Thread(new ParameterizedThreadStart(ColorDetectionThread));
                threadColorDetection.Start(sender);



                //Bitmap screenPixel = new Bitmap(101, 101, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                //Graphics gdest = Graphics.FromImage(screenPixel);//On crée un graphics pour notre image
                //IntPtr hDC = gdest.GetHdc();//On récupère le pointeur du graphic précédement créé


                //IntPtr desktopHdc = GetDC(IntPtr.Zero);//On récupère le pointeur de l'écran

                //int retval = BitBlt(hDC, 0, 0, 50, 50, desktopHdc, 101, 101, (int)CopyPixelOperation.SourceCopy);//L'écran est copié dans le graphic de l'image

                //gdest.ReleaseHdc();
                //gdest.Dispose();



                //IntPtr ptr = screenPixel.GetHicon();

                //IconInfo tmp = new IconInfo();
                //GetIconInfo(ptr, ref tmp);
                //tmp.xHotspot = 50;
                //tmp.yHotspot = 50;
                //tmp.fIcon = false;
                //ptr = CreateIconIndirect(ref tmp);
                //System.Windows.Forms.Cursor c = new System.Windows.Forms.Cursor(ptr);

                //SafeFileHandle panHandle = new SafeFileHandle(c.Handle, false);
                //this.Cursor = System.Windows.Interop.CursorInteropHelper.Create(panHandle);
                

                //Graphics g = Graphics.FromHdc(desktopHdc);//Il faut recréer un graphics car le précédent est utilisé autre part
                //g.DrawImage(screenPixel, new System.Drawing.Point(200, 200));//On dessine dessus l'image
                //g.Dispose();

                //ReleaseDC(IntPtr.Zero, desktopHdc);//On dessine le g
            }
        }


        


        private void StopColorDetection()//Appelée par clique gauche ou échap
        {
            hookManager.OnLeftMouseButtonDownBlocked -= PickupColor;
            hookManager.OnKeyUp -= KeyPressedMaybeAbort;
            threadColorDetection.Abort();

            if (BlockMode.IsChecked == true)
            {
                foreach (Process element in processesToRestore)
                {
                    //Console.WriteLine(element.MainWindowTitle);
                    EnableWindow(element.MainWindowHandle, true);
                }
            }
        }



        private void KeyPressedMaybeAbort(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                StopColorDetection();

                hookManager.UnblockNextLeftMouseDown();
                hookManager.UnblockNextLeftMouseUp();

                UpdateColorInformations(System.Drawing.Color.FromArgb(
                    selectedRectangleColor.A, 
                    selectedRectangleColor.R, 
                    selectedRectangleColor.G, 
                    selectedRectangleColor.B), selectedRectangle);//On remet la couleur de base
            }
        }


        private void PickupColor(object sender, MouseEventArgs e)
        {
            StopColorDetection();
        }
        

        private void ColorDetectionThread(object sender)
        {
            while (true)
            {
                System.Drawing.Point mouseCoordinates = new System.Drawing.Point();
                GetCursorPos(ref mouseCoordinates);

                System.Drawing.Color color = GetColorAt(mouseCoordinates);

                UpdateColorInformations(color, sender);

                Thread.Sleep(100);
            }
        }


        private void UpdateColorInformations(System.Drawing.Color color, object sender)
        {
            Color color1 = new Color();
            color1.A = color.A;
            color1.R = color.R;
            color1.G = color.G;
            color1.B = color.B;

            if (!((Rectangle)sender).CheckAccess())//Si nous sommes sur le mauvais thread
            {
                ((Rectangle)sender).Dispatcher.Invoke((MethodInvoker)(() => {
                    ((TextBlock)((Grid)((Rectangle)sender).Parent).Children[1]).Text = color1.ToString().Remove(1, 2);
                    ((TextBlock)((Grid)((Rectangle)sender).Parent).Children[2]).Text = color1.R.ToString();
                    ((TextBlock)((Grid)((Rectangle)sender).Parent).Children[3]).Text = color1.G.ToString();
                    ((TextBlock)((Grid)((Rectangle)sender).Parent).Children[4]).Text = color1.B.ToString();
                    ((Rectangle)sender).Fill = new SolidColorBrush(color1);
                }));
            }
            else
            {
                ((TextBlock)((Grid)((Rectangle)sender).Parent).Children[1]).Text = color1.ToString();
                ((TextBlock)((Grid)((Rectangle)sender).Parent).Children[2]).Text = color1.R.ToString();
                ((TextBlock)((Grid)((Rectangle)sender).Parent).Children[3]).Text = color1.G.ToString();
                ((TextBlock)((Grid)((Rectangle)sender).Parent).Children[4]).Text = color1.B.ToString();
                ((Rectangle)sender).Fill = new SolidColorBrush(color1);
            }
            //TextBlock textBlock = ((TextBlock)((Grid)((System.Windows.Shapes.Rectangle)sender).Parent).Children[1]);
            //System.Windows.Shapes.Rectangle rectangle = ((System.Windows.Shapes.Rectangle)sender);

            //if (!textBlock.CheckAccess())
            //{
            //    textBlock.Dispatcher.Invoke((MethodInvoker)(() => { textBlock.Text = color1.ToString(); }));
            //}
            //else
            //{
            //    textBlock.Text = color1.ToString();
            //}

            //if (!rectangle.CheckAccess())
            //{
            //    rectangle.Dispatcher.Invoke((MethodInvoker)(() => { rectangle.Fill = new SolidColorBrush(color1); }));
            //}
            //else
            //{
            //    rectangle.Fill = new SolidColorBrush(color1);
            //}
        }

        #endregion





        //################################################################
        #region Quit and Minimize buttons


        private void Button_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Color color = new Color();

            color.R = 25;
            color.G = 37;
            color.B = 52;
            color.A = 255;

            if(e.LeftButton == MouseButtonState.Pressed)
            {
                color.R = 0;
                color.G = 122;
                color.B = 204;
                color.A = 255;
            }

            ((Grid)sender).Background = new SolidColorBrush(color);
        }

        private void Button_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Color color = new Color();

            color.R = 11;
            color.G = 16;
            color.B = 22;
            color.A = 255;

            ((Grid)sender).Background = new SolidColorBrush(color);
        }

        private void Button_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Color color = new Color();

            color.R = 0;
            color.G = 122;
            color.B = 204;
            color.A = 255;

            ((Grid)sender).Background = new SolidColorBrush(color);
        }





        private void QuitButton_MouseUp(object sender, MouseButtonEventArgs e)
        {
            this.Close();
        }

        private void MinimizeButton_MouseUp(object sender, MouseButtonEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }


        #endregion






        private void CopyHexadecimal(object sender, MouseButtonEventArgs e)
        {
            Clipboard.SetText(((TextBlock)sender).Text.Remove(0,1));
        }

        private void CopyText(object sender, MouseButtonEventArgs e)
        {
            Clipboard.SetText(((TextBlock)sender).Text);
        }
    }
}