using KeyModulator;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using WindowsInput;
using WindowsInput.Native;

namespace KeyModulatorWinForm
{
    public partial class MainForm : Form
    {
        [DllImport("user32")]
        public static extern int GetMessageTime();

        [DllImport("user32")]
        public static extern
           bool GetMessage(ref Message lpMsg, IntPtr handle, uint mMsgFilterInMain, uint mMsgFilterMax);

        [DllImport("user32")]
        public static extern
           bool TranslateMessage([In] ref Message lpMsg);

        [DllImport("user32")]
        public static extern
          IntPtr DispatchMessage([In] ref Message lpMsg);

        private KeyboardHook kbh_;
        private int lastStroke_ = 0;
        private DateTime lastStrokeTime_;

        private bool queuedSpaces_ = false;
        private DateTime spaceQueueTime_;
        private AutoResetEvent queuedSpaceEvent_ = new AutoResetEvent(false);

        private Thread KeyProcesingThread_;
        private bool KeyProcesingRunning_;

        private KeyboardSimulator keyboardSim_ = new KeyboardSimulator(new InputSimulator());
        private int simmedCount_ = 0;

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            kbh_ = new KeyboardHook(AllowKey);
            KeyProcesingRunning_ = true;
            KeyProcesingThread_ = new Thread(new ThreadStart(KeyProcessLoop));
            KeyProcesingThread_.Start();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            KeyProcesingRunning_ = false;
            queuedSpaceEvent_.Set();
        }
       
        private void KeyProcessLoop()
        {
            while (KeyProcesingRunning_)
            {
                queuedSpaceEvent_.WaitOne();
                if (queuedSpaces_)
                {
                    Thread.Sleep(10);
                    if (queuedSpaces_)
                    {
                        keyboardSim_.KeyPress(VirtualKeyCode.SPACE);
                        simmedCount_++;
                        queuedSpaces_ = false;
                    }
                }
            }
        }

        private static bool ConsideredSame(int time1, int time2)
        {
            Debug.WriteLine($"{time1} {time2}");
            if (Math.Abs(time1 - time2) < 5) return true;
            // TODO wrap
            return false;
        }

        public bool AllowKey(KeyboardHook.KeyHookEventArgs args)
        {
            var allow = true;
            if (args.KeyCode == 32 && simmedCount_ > 0)
            {
                Debug.WriteLine("allowed");
                simmedCount_--;
            }
            else if (args.KeyCode == 32)
            {
                if (lastStroke_ == 40)
                {
                    var curr = DateTime.UtcNow;
                    var elapsed = (curr - lastStrokeTime_).TotalMilliseconds;
                    if (elapsed < 10)
                    {
                        allow = false;
                    }
                }
                if (allow)
                {
                    allow = false;
                    queuedSpaces_ = true;
                    spaceQueueTime_ = DateTime.UtcNow;
                    queuedSpaceEvent_.Set();
                }
            }
            else if (args.KeyCode == 40)
            {
                queuedSpaces_ = false;
            }

            if (allow)
            {
                lastStroke_ = args.KeyCode;
                lastStrokeTime_ = DateTime.UtcNow;
            }
            return allow;
        }
    }
}
