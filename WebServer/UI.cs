using System;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;


/************************************\
 * Simple Http Web Server           *
 * Huseyin Atasoy                   *
 * www.atasoyweb.net                *
 * huseyin@atasoyweb.net            *
\************************************/

namespace WebServer
{
    public partial class UI : Form
    {
        Server server = new Server();

        public UI()
        {
            InitializeComponent();            
        }
        
        private void btnStart_Click(object sender, EventArgs e)
        {
            IPAddress ipAddress = IPAddress.Parse(nudIP1.Value.ToString() + "." + 
                nudIP2.Value.ToString() + "." + nudIP3.Value.ToString() + "." + nudIP4.Value.ToString());
            int maxConn = (int)SocketOptionName.MaxConnections;

            // Resolves a host name to an IPHostEntry instance 
            IPHostEntry ipHost = Dns.GetHostEntry("");
            // Gets first IP address associated with a localhost 
            IPAddress ipAddr = ipHost.AddressList[0];
            
            if (server.start(ipAddress/*IPAddress.Any*/, Convert.ToInt32(nudPort.Value), maxConn, txtContentPath.Text))
            {
                btnStart.Enabled = false;
                btnStop.Enabled = true;
                btnTest.Enabled = true;
            }
            else
                MessageBox.Show(this, "Couldn't start the server. Make sure port " + 
                    nudPort.Value.ToString() + " is not being listened by other servers.", 
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            server.stop();
            btnStart.Enabled = true;
            btnStop.Enabled = false;
            btnTest.Enabled = false;
        }

        private bool startedUp = false;
        private void Form1_Shown(object sender, EventArgs e)
        {
            if (startedUp) return;
            startedUp = true;
           

            ipAddresses.Text = null;
            ThreadPool.QueueUserWorkItem((Object arg) =>
            {
                try
                {
                    string externalIP = null;
                    try
                    {
                        externalIP = (new WebClient()).DownloadString("http://imageprocessing.ro/");
                        externalIP = (new Regex(@"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}")).Matches(externalIP)[0].ToString();
                    }
                    catch { }
                    this.Invoke((Action)(() => { ipAddresses.Text = "Internal IP: " + 
                        System.Net.Dns.GetHostByName(System.Net.Dns.GetHostName()).AddressList[0].ToString() + 
                        (externalIP == null ? "" : ", External IP: " + externalIP); }));
                }catch{}
            });
        }

        private void btnTest_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("explorer.exe", "http://" + nudIP1.Value.ToString() + 
                "." + nudIP2.Value.ToString() + "." + nudIP3.Value.ToString() + "." + nudIP4.Value.ToString() +
                (nudPort.Value == 80 ? "" : ":" + nudPort.Value.ToString()));
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            server.stop();
            btnStop.PerformClick();
            //Properties.Settings.Default.Save();
        }

        private void trayIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            Show();
            trayIcon.Visible = false;
            this.WindowState = FormWindowState.Normal;
        }

        private void Form1_SizeChanged(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                trayIcon.Visible = true;
                this.Hide();
            }
        }
    }
}
