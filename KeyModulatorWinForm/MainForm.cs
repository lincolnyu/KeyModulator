using KeyModulator;
using System;
using System.Windows.Forms;

namespace KeyModulatorWinForm
{
    public partial class MainForm : Form
    {
        public const int IntervalThresholdMs = 100;

        private KeyboardHook kbh_;
        private int lastStrokeKeyCode = 0;
        private DateTime lastStrokeTime_;

        public MainForm()
        {
            InitializeComponent();

            kbh_ = new KeyboardHook(AllowKey);
        }
        
        public bool AllowKey(KeyboardHook.KeyHookEventArgs args)
        {
            var allow = true;
            if (args.KeyCode == 32)
            {
                if (lastStrokeKeyCode == 40)
                {
                    var curr = DateTime.UtcNow;
                    var elapsed = (curr - lastStrokeTime_).TotalMilliseconds;
                    if (elapsed < IntervalThresholdMs)
                    {
                        allow = false;
                    }
                }
            }

            if (allow)
            {
                lastStrokeKeyCode = args.KeyCode;
                lastStrokeTime_ = DateTime.UtcNow;
            }
            return allow;
        }
    }
}
