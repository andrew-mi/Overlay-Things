using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OverlayThings
{
    public class MetricForm : Form
    {

        private const int WM_NCHITTEST = 0x84;
        private const int HTCLIENT = 0x1;
        private const int HTCAPTION = 0x2;

        ///
        /// Handling the window messages 
        ///
        protected override void WndProc(ref Message message)
        {
            base.WndProc(ref message);

            if (message.Msg == WM_NCHITTEST && (int)message.Result == HTCLIENT)
                message.Result = (IntPtr)HTCAPTION;
        }

        private Label headerLabel;
        private Label contentLabel;
        private Label lastUpdatedLabel;

        public MetricForm()
        {
            InitializeComponent();
        }

        [DllImport("user32.dll")]
        public static extern void ReleaseCapture();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        private const int WM_NCLBUTTONDOWN = 0x00A1;

        private void InitializeComponent()
        {
            this.headerLabel = new Label();
            this.contentLabel = new Label();
            this.lastUpdatedLabel = new Label();

            this.SuspendLayout();

            // Header Label
            this.headerLabel.AutoSize = true;
            this.headerLabel.Font = new Font("Arial", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.headerLabel.Location = new Point(10, 10);
            this.headerLabel.MouseDown += (sender, e) => {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
                }
            };
            this.Controls.Add(this.headerLabel);

            // Content Label
            this.contentLabel.AutoSize = true;
            this.contentLabel.Font = new Font("Arial", 16F, FontStyle.Bold, GraphicsUnit.Point);
            this.contentLabel.Location = new Point(10, 40);
            this.contentLabel.MouseDown += (sender, e) => {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
                }
            };
            this.Controls.Add(this.contentLabel);

            // Last Updated Label
            this.lastUpdatedLabel.AutoSize = true;
            this.lastUpdatedLabel.Font = new Font("Arial", 8F, FontStyle.Italic, GraphicsUnit.Point);
            this.lastUpdatedLabel.Location = new Point(10, 80);
            this.lastUpdatedLabel.MouseDown += (sender, e) => {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
                }
            };
            this.Controls.Add(this.lastUpdatedLabel);

            // MetricForm
            this.FormBorderStyle = FormBorderStyle.None;
            this.MaximizeBox = false;
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.ClientSize = new Size(300, 120);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80; // WS_EX_TOOLWINDOW: Prevent window from appearing in Alt-Tab.
                cp.ExStyle |= 0x8;  // WS_EX_TOPMOST: Make the window topmost.
                return cp;
            }
        }


        public void UpdateMetric(string header, string content, string lastUpdated, Color bgColor)
        {
            this.headerLabel.Text = header;
            this.contentLabel.Text = content;
            this.lastUpdatedLabel.Text = lastUpdated;
            this.BackColor = bgColor;
        }
    }
}
