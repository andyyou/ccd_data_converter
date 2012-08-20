﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using log4net;
using log4net.Config;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.IO;
using System.Collections;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.XPath;
using System.Diagnostics;

namespace CCDConvert
{
    public partial class FormMain : Form
    {
        #region Local variables

        private static ILog log = LogManager.GetLogger(typeof(Program));
        private static string xmlPath = @"C:\Projects\Github\CCDConvert\CCDConvert\config\config.xml";

        private double _offset_default_y, offset_y, offset_x;
        private Dictionary<string, string> dicRelative = new Dictionary<string, string>();
        #endregion

        public FormMain()
        {
            InitializeComponent();
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            // Initial status image
            tslbStatus.Image = Properties.Resources.Stop;
            tslbHardware.Image = Properties.Resources.Stop;
            tslbSoftware.Image = Properties.Resources.Stop;

            // Load log4net config file
            XmlConfigurator.Configure(new FileInfo("log4netconfig.xml"));
            Thread networkThread = new Thread(new ThreadStart(updateNetworkStatus));
            networkThread.IsBackground = true;
            networkThread.Start();


            // Load xml config , this is first step.
            bool IsConfigured = getXml(xmlPath, dgvRelativeSettings);

            // ** Notice : Before use [converData] need run again. 本來要將Relative的資料綁入converData 但效能差了2倍 
            updateDictionaryRelative();

            if (IsConfigured)
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();
                convertData("DATA,FlawID,0;FlawName,WS;FlawMD,0.804000;FlawCD,0.924000;JobID,231tst-13;", _offset_default_y);
                sw.Stop();
                MessageBox.Show(sw.Elapsed.ToString());
            }
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            // Start TCP Server
        }

        #region Methods

        private void updateNetworkStatus()
        {
            Image imgRun = Properties.Resources.Run;
            Image imgStop = Properties.Resources.Stop;
            while (true)
            {
                if (isNetworkConnected())
                {
                    tslbHardware.Text = "Hardware OK";
                    tslbHardware.Image = imgRun;
                }
                else
                {
                    tslbHardware.Text = "Hardware Error";
                    tslbHardware.Image = imgStop;
                }
            }
        }

        //將DataGridView的對資料轉存成Dictionary提升效能
        private Dictionary<string, string> getRelativeGridViewToDictionary(DataGridView dgv)
        {
            Dictionary<string, string> tmpDict = new Dictionary<string, string>();
           
            lock (dgv)
            {
                dgv.ReadOnly = true;
                for (int i = 0; i < dgv.Rows.Count - 1; i++)
                {
                    if (dgv.Rows[i].Cells[0].Value.ToString().IndexOf(',') > 0)
                    {
                        string[] tmpOriginColumn = dgv.Rows[i].Cells[0].Value.ToString().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string st in tmpOriginColumn)
                        {
                            if (!String.IsNullOrEmpty(st) && !String.IsNullOrEmpty(dgv.Rows[i].Cells[1].Value.ToString()))
                            {
                                tmpDict.Add(st, dgv.Rows[i].Cells[1].Value.ToString());
                            }
                        }
                    }
                    else
                    {
                        if (!String.IsNullOrEmpty(dgv.Rows[i].Cells[0].Value.ToString()) && !String.IsNullOrEmpty(dgv.Rows[i].Cells[1].Value.ToString()))
                            tmpDict.Add(dgv.Rows[i].Cells[0].Value.ToString(), dgv.Rows[i].Cells[1].Value.ToString());
                    }

                }
                dgv.ReadOnly = false;
            }

            return tmpDict;
        }

        /// <summary>
        /// Check network status
        /// </summary>
        /// <returns>whether network is alive or died</returns>
        private Boolean isNetworkConnected()
        {
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (NetworkInterface face in interfaces)
            {
                if (face.OperationalStatus == OperationalStatus.Up || face.OperationalStatus == OperationalStatus.Unknown)
                {
                    // Internal network interfaces from VM adapters can still be connected 
                    IPv4InterfaceStatistics statistics = face.GetIPv4Statistics();
                    if (statistics.BytesReceived > 0 && statistics.BytesSent > 0)
                    {
                        // A network interface is up
                        return true;
                    }
                }
            }
            // No Interfaces are up
            return false;
        }

        /// <summary>
        /// Convert data from Source IP to Dest IP 
        /// </summary>
        /// <param name="input">Source Data</param>
        /// <returns></returns>
        private string convertData(string input , double default_offset_y)
        {
            string pattern = @"^DATA*";
            Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);
            Dictionary<string, string> dicOutpout = new Dictionary<string, string>();
           
            if (regex.IsMatch(input))
            {
                string[] tmp = input.Substring(input.IndexOf(',') + 1).Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                
                foreach(string i in tmp)
                {
                    string[] sp = i.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    dicOutpout.Add(sp[0].ToString(), sp[1].ToString());
                }
            }

            // Deal format string
            //double offset_y = double.TryParse(txtY.Text, out offset_y) ? offset_y : 0;
            //double offset_x = double.TryParse(txtX.Text, out offset_x) ? offset_x : 0;
            double y = double.Parse(dicOutpout["FlawMD"]) * 1000 + _offset_default_y + offset_y;
            double x = double.Parse(dicOutpout["FlawCD"]) * 1000 + offset_x;
             string result = "";
             if (dicRelative.ContainsKey(dicOutpout["FlawName"]))
                 result = String.Format("{0};{1};{2}", dicRelative[dicOutpout["FlawName"]], y.ToString(), x.ToString());
             else
                 result = String.Format("{0};{1};{2}", "0", y.ToString(), x.ToString());

            return result;
        }

        private void updateDictionaryRelative()
        {
            // Set relative data and data of convert method.
            dicRelative = getRelativeGridViewToDictionary(dgvRelativeSettings);
            offset_y = double.TryParse(txtY.Text, out offset_y) ? offset_y : 0;
            offset_x = double.TryParse(txtX.Text, out offset_x) ? offset_x : 0;
        }

        private bool getXml(string path, DataGridView dgv)
        {
            FileStream stream = new FileStream(path, FileMode.Open);
            XPathDocument document = new XPathDocument(stream);
            XPathNavigator navigator = document.CreateNavigator();
           
            txtY.Text = navigator.SelectSingleNode("//offset[@name='Y']").Value;
            txtX.Text = navigator.SelectSingleNode("//offset[@name='X']").Value;
            _offset_default_y = navigator.SelectSingleNode("//offset[@name='DefaultOffsetY']").ValueAsDouble;

            XPathNodeIterator node = navigator.Select("//relative_table/column");
            while (node.MoveNext())
            {
                dgv.Rows.Add(node.Current.SelectSingleNode("source").Value, node.Current.SelectSingleNode("target").Value);
            }

            #region 原本要使用DataSet, 因為整個Dataset, BindSource 不熟 暫時還沒用.
            //DataSet ds = new DataSet();
            //DataTable dt = new DataTable("RelativeTable");
            //dt.Columns.Add("Source", typeof(string));
            //dt.Columns.Add("Target", typeof(string));
            //dt.Rows.Add("AAAA", "BBB");
            //ds.Tables.Add(dt);
            //ds.ReadXml(path);
            #endregion

            return true;
        }

        private void saveXml(string path, DataGridView dgv)
        {
            XmlDocument document = new XmlDocument();
            document.Load(path);
            XPathNavigator navigator = document.CreateNavigator();
            navigator.SelectSingleNode("//offset[@name='Y']").SetValue(txtY.Text);
            navigator.SelectSingleNode("//offset[@name='X']").SetValue(txtX.Text);

            // Remove old relative_table for add new record
            if (navigator.Select("//relative_table/*").Count > 0)
            {
                XPathNavigator first = navigator.SelectSingleNode("//relative_table/*[1]");
                XPathNavigator last = navigator.SelectSingleNode("//relative_table/*[last()]");
                navigator.MoveTo(first);
                navigator.DeleteRange(last);
            }

            dgv.EndEdit();
            for (int i = 0; i < dgv.Rows.Count - 1; i++)
            {
                string source = dgv.Rows[i].Cells[0].Value.ToString();
                string target = dgv.Rows[i].Cells[1].Value.ToString();
                navigator.SelectSingleNode("//relative_table").AppendChildElement(string.Empty, "column", string.Empty, null);
                // Move to last column element and add source , target value.
                navigator.SelectSingleNode("//relative_table/column[last()]").AppendChildElement(string.Empty, "source", string.Empty, source.ToUpper());
                navigator.SelectSingleNode("//relative_table/column[last()]").AppendChildElement(string.Empty, "target", string.Empty, target.ToUpper());
               
            }
            document.Save(path); 
        }
        
        #endregion

        #region Event
        private void offset_Validating(object sender, CancelEventArgs e)
        {
            string pattern = @"[0-9]+(?:\.[0-9]*)?";
            Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);
            TextBox txt = (TextBox)sender;
            if (!regex.IsMatch(txt.Text))
            {
                txt.Text = "";
            }
        }

        private void IP_Validating(object sender, CancelEventArgs e)
        {
            string pattern = @"\b((2[0-5]{2}|1[0-9]{2}|[0-9]{1,2})\.){3}(2[0-5]{2}|1[0-9]{2}|[0-9]{1,2})\b";
            Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);
            TextBox txt = (TextBox)sender;
            if (!regex.IsMatch(txt.Text))
            {
                txt.Text = "";
            }
        }

        private void Port_Validating(object sender, CancelEventArgs e)
        {
            TextBox txt = (TextBox)sender;
            int port = int.TryParse(txt.Text, out port) ? port : 0;
            if (port < 0 && port > 65536)
            {
                txt.Text = "";
            }
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            DialogResult drResult = MessageBox.Show("確認是否結束程式?", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (drResult == DialogResult.Yes)
            {
                saveXml(xmlPath, dgvRelativeSettings);
            }
            else if (drResult == DialogResult.No)
            {
                e.Cancel = true;
            }

        }
        #endregion

       



    }
}
