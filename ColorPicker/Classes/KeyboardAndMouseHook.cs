using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Windows;
using MessageBox = System.Windows.Forms.MessageBox;


namespace KeyboardAndMouseHook
{
	class KeyboardAndMouseHookObject
	{
		//################################################################
		#region Structures

		[StructLayout(LayoutKind.Sequential)]
		private class POINT
		{
			/// <summary>
			/// Coordonnée X. 
			/// </summary>
			public int x;
			/// <summary>
			/// Coordonnée Y.
			/// </summary>
			public int y;
		}



		[StructLayout(LayoutKind.Sequential)]
		private class MouseHookStruct
		{
			/// <summary>
			/// Structure POINT pour les coordonnée 
			/// </summary>
			public POINT pt;
			/// <summary>
			/// Handle de la window
			/// </summary>
			public int hwnd;
			/// <summary>
			/// Specifies the hit-test value. For a list of hit-test values, see the description of the WM_NCHITTEST message. 
			/// </summary>
			public int wHitTestCode;
			/// <summary>
			/// Specifies extra information associated with the message. 
			/// </summary>
			public int dwExtraInfo;
		}



		[StructLayout(LayoutKind.Sequential)]
		private class MouseLLHookStruct
		{
			/// <summary>
			/// Structure POINT.
			/// </summary>
			public POINT pt;
			public int mouseData;
			public int flags;
			public int time;
			public int dwExtraInfo;
		}



		[StructLayout(LayoutKind.Sequential)]
		private class KeyboardHookStruct
		{
			/// <summary>
			/// Key code virtuel, la valeur doit etre entre 1 et 254. 
			/// </summary>
			public int vkCode;
			public int scanCode;
			public int flags;
			public int time;
			public int dwExtraInfo;
		}

		#endregion

		//################################################################
		#region DLL import
		[DllImport("user32.dll")]
		static extern short GetKeyState(int virtualKeyCode);

		[DllImport("user32.dll")]
		static extern bool GetKeyboardState(byte[] keys);





		[DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
		private static extern int SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, int dwThreadId);


		[DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
		private static extern int UnhookWindowsHookEx(int idHook);


		[DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
		private static extern int CallNextHookEx(int idHook, int nCode, int wParam, IntPtr lParam);

		[DllImport("user32")]
		private static extern int ToAscii(int uVirtKey, int uScanCode, byte[] lpbKeyState, byte[] lpwTransKey, int fuState);

		private delegate int HookProc(int nCode, int wParam, IntPtr lParam);

		[DllImport("kernel32.dll")]
		public static extern IntPtr GetModuleHandle(string name);

		#endregion

		//################################################################
		#region Constants
		//Valeurs issues de Winuser.h du SDK de Microsoft.
		/// <summary>
		/// Windows NT/2000/XP: Installe un hook pour la souris
		/// </summary>
		private const int WH_MOUSE_LL = 14;

		/// <summary>
		/// Windows NT/2000/XP: Installe un hook pour le clavier
		/// </summary>
		private const int WH_KEYBOARD_LL = 13;

		private const int WH_MOUSE = 7;

		private const int WH_KEYBOARD = 2;

		/// <summary>
		/// Le message WM_MOUSEMOVE est envoyé quand la souris bouge
		/// </summary>
		private const int WM_MOUSEMOVE = 0x200;
		/// <summary>
		/// Le message WM_LBUTTONDOWN est envoyé lorsque le bouton gauche est pressé
		/// </summary>
		private const int WM_LBUTTONDOWN = 0x201;
		/// <summary>
		/// Le message WM_RBUTTONDOWN est envoyé lorsque le bouton droit est pressé
		/// </summary>
		private const int WM_RBUTTONDOWN = 0x204;
		/// <summary>
		/// Le message WM_MBUTTONDOWN est envoyé lorsque le bouton central est pressé
		/// </summary>
		private const int WM_MBUTTONDOWN = 0x207;
		/// <summary>
		/// Le message WM_LBUTTONUP est envoyé lorsque le bouton gauche est relevé
		/// </summary>
		private const int WM_LBUTTONUP = 0x202;
		/// <summary>
		/// Le message WM_RBUTTONUP est envoyé lorsque le bouton droit est relevé 
		/// </summary>
		private const int WM_RBUTTONUP = 0x205;

		private const int WM_MBUTTONUP = 0x208;

		private const int WM_LBUTTONDBLCLK = 0x203;

		private const int WM_RBUTTONDBLCLK = 0x206;

		private const int WM_MBUTTONDBLCLK = 0x209;

		private const int WM_MOUSEWHEEL = 0x020A;


		private const int WM_KEYDOWN = 0x100;

		private const int WM_KEYUP = 0x101;

		private const int WM_SYSKEYDOWN = 0x104;

		private const int WM_SYSKEYUP = 0x105;

		private const byte VK_SHIFT = 0x10;
		private const byte VK_CAPITAL = 0x14;
		private const byte VK_NUMLOCK = 0x90;

		#endregion

		//################################################################
		#region Variables
		private int mouseHookId;
		private HookProc MouseHookCallbackDelegate;
		public event MouseEventHandler OnMouseActivity;
		public event MouseEventHandler OnMouseButtonDown;
		public event MouseEventHandler OnMouseButtonPress;
		public event MouseEventHandler OnMouseButtonUp;
		public event MouseEventHandler OnLeftMouseButtonDownBlocked;
		public event MouseEventHandler OnRightMouseButtonDownBlocked;
		public event MouseEventHandler OnLeftMouseButtonUpBlocked;
		public event MouseEventHandler OnRightMouseButtonUpBlocked;
		public event MouseEventHandler OnMouseMove;
		public event MouseEventHandler OnMouseWheel;
		public event MouseEventHandler OnLeftMouseButtonDown;
		public event MouseEventHandler OnLeftMouseButtonPress;
		public event MouseEventHandler OnLeftMouseButtonUp;
		public event MouseEventHandler OnRightMouseButtonDown;
		public event MouseEventHandler OnRightMouseButtonPress;
		public event MouseEventHandler OnRightMouseButtonUp;

		private int keyboardHookId;
		private HookProc KeyboardHookCallbackDelegate;
		public event KeyEventHandler OnKeyDown;
		public event KeyPressEventHandler OnKeyPress;
		public event KeyEventHandler OnKeyUp;

		private bool mustBlockNextLeftMouseDown = false;
		private bool mustBlockNextLeftMouseUp = false;
		private bool mustBlockNextRightMouseDown = false;
		private bool mustBlockNextRightMouseUp = false;

		private const int
			MOUSE_BUTTON_DOWN = 0,
			MOUSE_BUTTON_PRESS = 1,
			MOUSE_BUTTON_UP = 2,
			MOUSE_MOVE = 3,
			MOUSE_WHEEL = 4;




		#endregion









		public KeyboardAndMouseHookObject()//Constructeur
		{
			#region Mouse Hook

			MouseHookCallbackDelegate = new HookProc(MouseHookCallback);//On utilise un délégué sinon le GC supprime le délégué et ça plante


			mouseHookId = SetWindowsHookEx(WH_MOUSE_LL, MouseHookCallbackDelegate, GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName), 0);

			if (mouseHookId == 0)
			{
				int errorCode = Marshal.GetLastWin32Error();
				MessageBox.Show("Erreur initialisation hook souris: " + errorCode);
				//throw new Win32Exception(errorCode);
			}





			#endregion

			#region Keyboard Hook
			KeyboardHookCallbackDelegate = new HookProc(KeyboardHookCallback);//On utilise un délégué sinon le GC supprime le délégué et ça plante

			keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, KeyboardHookCallbackDelegate, GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName), 0);

			if (keyboardHookId == 0)
			{
				int errorCode = Marshal.GetLastWin32Error();
				MessageBox.Show("Erreur initialisation hook clavier: " + errorCode);
				//throw new Win32Exception(errorCode);
			}




			#endregion
		}



		public void BlockNextLeftMouseDown()
		{
			mustBlockNextLeftMouseDown = true;
		}

		public void BlockNextLeftMouseUp()
		{
			mustBlockNextLeftMouseUp = true;
		}

		public void BlockNextRightMouseDown()
		{
			mustBlockNextRightMouseDown = true;
		}

		public void BlockNextRightMouseUp()
		{
			mustBlockNextRightMouseUp = true;
		}


		public void UnblockNextLeftMouseDown()
		{
			mustBlockNextLeftMouseDown = false;
		}

		public void UnblockNextLeftMouseUp()
		{
			mustBlockNextLeftMouseUp = false;
		}

		public void UnblockNextRightMouseDown()
		{
			mustBlockNextRightMouseDown = false;
		}

		public void UnblockNextRightMouseUp()
		{
			mustBlockNextRightMouseUp = false;
		}




		private int MouseHookCallback(int nCode, int wParam, IntPtr lParam)
		{
			int actionType = -1;

			if ((nCode >= 0))
			{
				//Marshall the data from callback.
				MouseLLHookStruct mouseHookStruct = (MouseLLHookStruct)Marshal.PtrToStructure(lParam, typeof(MouseLLHookStruct));


				//detect button clicked
				MouseButtons button = MouseButtons.None;
				short mouseDelta = 0;


				switch (wParam)//Le message renvoyé par le hook
				{
					case WM_MOUSEMOVE:
						actionType = MOUSE_MOVE;
						break;
					case WM_LBUTTONDOWN:
						button = MouseButtons.Left;
						actionType = MOUSE_BUTTON_DOWN;
						break;
					case WM_LBUTTONDBLCLK:
						button = MouseButtons.Left;
						actionType = MOUSE_BUTTON_PRESS;
						break;
					case WM_LBUTTONUP:
						button = MouseButtons.Left;
						actionType = MOUSE_BUTTON_UP;
						break;
					case WM_RBUTTONDOWN:
						button = MouseButtons.Right;
						actionType = MOUSE_BUTTON_DOWN;
						break;
					case WM_RBUTTONUP:
						button = MouseButtons.Right;
						actionType = MOUSE_BUTTON_PRESS;
						break;
					case WM_RBUTTONDBLCLK:
						button = MouseButtons.Right;
						actionType = MOUSE_BUTTON_UP;
						break;
					case WM_MOUSEWHEEL:
						actionType = MOUSE_WHEEL;
						//If the message is WM_MOUSEWHEEL, the high-order word of mouseData member is the wheel delta. 
						//One wheel click is defined as WHEEL_DELTA, which is 120. 
						//(value >> 16) & 0xffff; retrieves the high-order word from the given 32-bit value
						mouseDelta = (short)((mouseHookStruct.mouseData >> 16) & 0xffff);
						//TODO: X BUTTONS (I havent them so was unable to test)
						//If the message is WM_XBUTTONDOWN, WM_XBUTTONUP, WM_XBUTTONDBLCLK, WM_NCXBUTTONDOWN, WM_NCXBUTTONUP, 
						//or WM_NCXBUTTONDBLCLK, the high-order word specifies which X button was pressed or released, 
						//and the low-order word is reserved. This value can be one or more of the following values. 
						//Otherwise, mouseData is not used. 
						break;
				}


				int clickCount = 0;
				if (button != MouseButtons.None)//double clicks
				{
					if (wParam == WM_LBUTTONDBLCLK || wParam == WM_RBUTTONDBLCLK) clickCount = 2;
					else clickCount = 1;
				}



				if (actionType != -1)//Si c'est une action connue
				{
					if (actionType == MOUSE_BUTTON_DOWN)//MOUSE BUTTON DOWN
					{
						if (OnMouseButtonDown != null && !mustBlockNextLeftMouseDown && !mustBlockNextRightMouseDown)
						{
							OnMouseButtonDown(this, new MouseEventArgs(button, clickCount, mouseHookStruct.pt.x, mouseHookStruct.pt.y, mouseDelta));
						}

						if (button == MouseButtons.Left)//Si c'est le clic gauche
						{
							if (mustBlockNextLeftMouseDown)//S'il faut le bloquer
							{
								mustBlockNextLeftMouseDown = false;

								if (OnLeftMouseButtonDownBlocked != null)
								{
									OnLeftMouseButtonDownBlocked(this, new MouseEventArgs(button, clickCount, mouseHookStruct.pt.x, mouseHookStruct.pt.y, mouseDelta));
								}

								return 1;
							}

							if (OnLeftMouseButtonDown != null)
								OnLeftMouseButtonDown(this, new MouseEventArgs(button, clickCount, mouseHookStruct.pt.x, mouseHookStruct.pt.y, mouseDelta));
						}
						else if (button == MouseButtons.Right)//Si c'est le clic droit
						{
							if (mustBlockNextRightMouseDown)//S'il faut le bloquer
							{
								mustBlockNextRightMouseDown = false;

								if (OnRightMouseButtonDownBlocked != null)
								{
									OnRightMouseButtonDownBlocked(this, new MouseEventArgs(button, clickCount, mouseHookStruct.pt.x, mouseHookStruct.pt.y, mouseDelta));
								}

								return 1;
							}

							if (OnRightMouseButtonDown != null)
								OnRightMouseButtonDown(this, new MouseEventArgs(button, clickCount, mouseHookStruct.pt.x, mouseHookStruct.pt.y, mouseDelta));
						}
					}
					else if (actionType == MOUSE_BUTTON_PRESS)//MOUSE BUTTON PRESS
					{
						if (OnMouseButtonPress != null)
						{
							OnMouseButtonPress(this, new MouseEventArgs(button, clickCount, mouseHookStruct.pt.x, mouseHookStruct.pt.y, mouseDelta));
						}


						if (button == MouseButtons.Left && OnLeftMouseButtonPress != null)
						{
							OnLeftMouseButtonPress(this, new MouseEventArgs(button, clickCount, mouseHookStruct.pt.x, mouseHookStruct.pt.y, mouseDelta));
						}
						else if (button == MouseButtons.Right && OnRightMouseButtonPress != null)
						{
							OnRightMouseButtonPress(this, new MouseEventArgs(button, clickCount, mouseHookStruct.pt.x, mouseHookStruct.pt.y, mouseDelta));
						}
					}
					else if (actionType == MOUSE_BUTTON_UP)//MOUSE BUTTON UP
					{
						if (OnMouseButtonUp != null && !mustBlockNextLeftMouseUp && !mustBlockNextRightMouseUp)
						{
							OnMouseButtonUp(this, new MouseEventArgs(button, clickCount, mouseHookStruct.pt.x, mouseHookStruct.pt.y, mouseDelta));
						}

						if (button == MouseButtons.Left)//Si c'est le clic gauche
						{
							if (mustBlockNextLeftMouseUp)//S'il faut le bloquer
							{
								mustBlockNextLeftMouseUp = false;

								if (OnLeftMouseButtonUpBlocked != null)
								{
									OnLeftMouseButtonUpBlocked(this, new MouseEventArgs(button, clickCount, mouseHookStruct.pt.x, mouseHookStruct.pt.y, mouseDelta));
								}

								return 1;
							}

							if (OnLeftMouseButtonUp != null)
								OnLeftMouseButtonUp(this, new MouseEventArgs(button, clickCount, mouseHookStruct.pt.x, mouseHookStruct.pt.y, mouseDelta));
						}
						else if (button == MouseButtons.Right)//Si c'est le clic droit
						{
							if (mustBlockNextRightMouseUp)//S'il faut le bloquer
							{
								mustBlockNextRightMouseUp = false;

								if (OnRightMouseButtonUpBlocked != null)
								{
									OnRightMouseButtonUpBlocked(this, new MouseEventArgs(button, clickCount, mouseHookStruct.pt.x, mouseHookStruct.pt.y, mouseDelta));
								}

								return 1;
							}

							if (OnRightMouseButtonUp != null)
								OnRightMouseButtonUp(this, new MouseEventArgs(button, clickCount, mouseHookStruct.pt.x, mouseHookStruct.pt.y, mouseDelta));
						}
					}
					else if (actionType == MOUSE_MOVE)//MOUSE MOVE
					{
						if (OnMouseMove != null)
							OnMouseMove(this, new MouseEventArgs(button, clickCount, mouseHookStruct.pt.x, mouseHookStruct.pt.y, mouseDelta));
					}
					else if (actionType == MOUSE_WHEEL)//MOUSE WHEEL
					{
						if (OnMouseWheel != null)
							OnMouseWheel(this, new MouseEventArgs(button, clickCount, mouseHookStruct.pt.x, mouseHookStruct.pt.y, mouseDelta));
					}
				}
				else//AUTRE ACTION NON-LISTEE
				{
					if (OnMouseActivity != null)
					{
						OnMouseActivity(this, new MouseEventArgs(button, clickCount, mouseHookStruct.pt.x, mouseHookStruct.pt.y, mouseDelta));
					}
				}
			}



			return CallNextHookEx(mouseHookId, nCode, wParam, lParam);
		}



		private int KeyboardHookCallback(int nCode, int wParam, IntPtr lParam)
		{
			if ((nCode >= 0) && (OnKeyDown != null || OnKeyPress != null || OnKeyUp != null))
			{
				//Remplissage de la structure KeyboardHookStruct a partir d'un pointeur
				KeyboardHookStruct MyKeyboardHookStruct = (KeyboardHookStruct)Marshal.PtrToStructure(lParam, typeof(KeyboardHookStruct));
				//KeyDown
				if (OnKeyDown != null && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
				{
					Keys keyData = (Keys)MyKeyboardHookStruct.vkCode;
					KeyEventArgs e = new KeyEventArgs(keyData);
					OnKeyDown(this, e);
				}

				// KeyPress
				if (OnKeyPress != null && wParam == WM_KEYDOWN)
				{
					// Si la touche Shift est appuyée
					bool isShift = ((GetKeyState(VK_SHIFT) & 0x80) == 0x80 ? true : false);
					// Si la touche CapsLock est appuyée
					bool isCapslock = (GetKeyState(VK_CAPITAL) != 0 ? true : false);

					byte[] keyState = new byte[256];
					GetKeyboardState(keyState);
					byte[] inBuffer = new byte[2];
					if (ToAscii(MyKeyboardHookStruct.vkCode,
							  MyKeyboardHookStruct.scanCode,
							  keyState,
							  inBuffer,
							  MyKeyboardHookStruct.flags) == 1)
					{
						char key = (char)inBuffer[0];
						if ((isCapslock ^ isShift) && Char.IsLetter(key))
							key = Char.ToUpper(key);
						KeyPressEventArgs e = new KeyPressEventArgs(key);
						OnKeyPress(this, e);
					}
				}

				// KeyUp
				if (OnKeyUp != null && (wParam == WM_KEYUP || wParam == WM_SYSKEYUP))
				{
					Keys keyData = (Keys)MyKeyboardHookStruct.vkCode;
					KeyEventArgs e = new KeyEventArgs(keyData);
					OnKeyUp(this, e);
				}

			}


			return CallNextHookEx(keyboardHookId, nCode, wParam, lParam);
		}
	}
}
