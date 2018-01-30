/// KEYBOARD.CS
/// (c) 2006 by Emma Burrows
/// This file contains the following items:
///  - KeyboardHook: class to enable low-level keyboard hook using
///    the Windows API.
///  - KeyboardHookEventHandler: delegate to handle the KeyIntercepted
///    event raised by the KeyboardHook class.
///  - KeyboardHookEventArgs: EventArgs class to contain the information
///    returned by the KeyIntercepted event.
///    
/// Change history:
/// 17/06/06: 1.0 - First version.
/// 18/06/06: 1.1 - Modified proc assignment in constructor to make class backward 
///                 compatible with 2003.
/// 10/07/06: 1.2 - Added support for modifier keys:
///                 -Changed filter in HookCallback to WM_KEYUP instead of WM_KEYDOWN
///                 -Imported GetKeyState from user32.dll
///                 -Moved native DLL imports to a separate internal class as this 
///                  is a Good Idea according to Microsoft's guidelines
/// 13/02/07: 1.3 - Improved modifier key support:
///                 -Added CheckModifiers() method
///                 -Deleted LoWord/HiWord methods as they weren't necessary
///                 -Implemented Barry Dorman's suggestion to AND GetKeyState
///                  values with 0x8000 to get their result
/// 23/03/07: 1.4 - Fixed bug which made the Alt key appear stuck
///                 - Changed the line
///                     if (nCode >= 0 && (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP))
///                   to
///                     if (nCode >= 0)
///                     {
///                        if (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP)
///                        ...
///                   Many thanks to "Scottie Numbnuts" for the solution.


using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KeyModulator
{
    /// <summary>
    /// Low-level keyboard intercept class to trap and suppress system keys.
    /// </summary>
    public class KeyboardHook : IDisposable
    {
        public delegate IntPtr HookHandlerDelegate(
            int nCode, IntPtr wParam, ref KBDLLHOOKSTRUCT lParam);

        //Keyboard API constants
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        //Modifier key constants
        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;
        private const int VK_CAPITAL = 0x14;

        //Variables used in the call to SetWindowsHookEx
        private IntPtr hookID_ = IntPtr.Zero;

        /// <summary>
        ///  Keep it so the managed object remains the entire app life cycle
        /// </summary>
        /// <remarks>
        ///  https://stackoverflow.com/questions/9957544/callbackoncollecteddelegate-in-globalkeyboardhook-was-detected
        /// </remarks>
        private HookHandlerDelegate hookHandler_;

        public class KeyHookEventArgs
        {
            public IntPtr wParam_;
            public KBDLLHOOKSTRUCT lParam_;

            public int KeyCode => lParam_.vkCode;

            public KeyHookEventArgs(IntPtr wParam, KBDLLHOOKSTRUCT lParam)
            {
                wParam_ = wParam;
                lParam_ = lParam;
            }
        }

        // User defined key permission
        public delegate bool AllowKeyDelegate(KeyHookEventArgs args);

        /// <summary>
        /// Delegate for KeyboardHook event handling.
        /// </summary>
        /// <param name="e">An instance of InterceptKeysEventArgs.</param>
        public delegate void KeyboardHookEventHandler(KeyHookEventArgs args, bool allowed);


        /// <summary>
        /// Event triggered when a keystroke is intercepted by the 
        /// low-level hook.
        /// </summary>
        public event KeyboardHookEventHandler KeyIntercepted;

        public AllowKeyDelegate AllowKey { get; }

        // Structure returned by the hook whenever a key is pressed
        public struct KBDLLHOOKSTRUCT
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        #region Constructors

        /// <summary>
        /// Sets up a keyboard hook to trap all keystrokes without 
        /// passing any to other applications.
        /// </summary>
        public KeyboardHook(AllowKeyDelegate allowKey)
        {
            AllowKey = allowKey;
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                hookHandler_ = new HookHandlerDelegate(HookCallback);
                hookID_ = NativeMethods.SetWindowsHookEx(WH_KEYBOARD_LL, hookHandler_,
                    NativeMethods.GetModuleHandle(curModule.ModuleName), 0);
                if (hookID_ == IntPtr.Zero)
                {
                    throw new Win32Exception("Failed to hook");
                }
            }
        }

        #endregion

        #region Check Modifier keys

        public static bool CapsLockOn()
            => (NativeMethods.GetKeyState(VK_CAPITAL) & 0x0001) != 0;

        public static bool ShiftPressed()
            => (NativeMethods.GetKeyState(VK_SHIFT) & 0x8000) != 0;

        public static bool CtrlPressed()
            => (NativeMethods.GetKeyState(VK_CONTROL) & 0x8000) != 0;

        public static bool AltPressed()
            => (NativeMethods.GetKeyState(VK_MENU) & 0x8000) != 0;
        
        #endregion Check Modifier keys

        #region Hook Callback Method

        /// <summary>
        /// Processes the key event captured by the hook.
        /// </summary>
        private IntPtr HookCallback(
            int nCode, IntPtr wParam, ref KBDLLHOOKSTRUCT lParam)
        {
            //Filter wParam for KeyUp events only
            if (nCode >= 0)
            {
                var khargs = new KeyHookEventArgs(wParam, lParam);
                var allow = AllowKey?.Invoke(khargs) != false;

                KeyIntercepted?.Invoke(khargs, allow);

                //If this key is being suppressed, return a dummy value
                if (!allow)
                {
                    return (IntPtr)1;
                }
            }
            //Pass key to next application
            return NativeMethods.CallNextHookEx(hookID_, nCode, wParam, ref lParam);
        }

        #endregion

        #region IDisposable Members
        /// <summary>
        /// Releases the keyboard hook.
        /// </summary>
        public void Dispose()
        {
            if (hookHandler_ != null)
            {
                var res = NativeMethods.UnhookWindowsHookEx(hookID_);
                if (!res)
                {
                    throw new Win32Exception("Failed to unhook");
                }
                hookHandler_ = null;
            }
        }

        #endregion
        
        #region Native methods

        [ComVisibleAttribute(false),
         System.Security.SuppressUnmanagedCodeSecurity()]
        public class NativeMethods
        {
            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr GetModuleHandle(string lpModuleName);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr SetWindowsHookEx(int idHook,
                HookHandlerDelegate lpfn, IntPtr hMod, uint dwThreadId);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool UnhookWindowsHookEx(IntPtr hhk);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
                IntPtr wParam, ref KBDLLHOOKSTRUCT lParam);

            [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true, CallingConvention = CallingConvention.Winapi)]
            public static extern short GetKeyState(int keyCode);
        }

        #endregion
    }
}

